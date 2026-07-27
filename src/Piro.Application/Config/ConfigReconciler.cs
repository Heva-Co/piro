using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Application.Services;
using Piro.Domain.Entities;
using Piro.Domain.Enums;

namespace Piro.Application.Config;

/// <summary>
/// Parses, validates, diffs and applies <c>piro.yaml</c> documents (RFC 0019 §4.3). The single place
/// reconciliation lives, so the CLI, the API and a future in-browser editor share one implementation.
/// </summary>
/// <remarks>
/// Writes go through <see cref="ServiceAppService"/> and <see cref="CheckAppService"/> rather than
/// straight to repositories. That is deliberate and load-bearing: those services also create alert
/// configs, reconcile <c>piro:*</c> system tags and mint inbound tokens, so a parallel write path
/// would produce checks subtly unlike admin-panel ones (§4.12). Their internal transactions nest
/// inside the one opened here, which is what keeps an apply all-or-nothing.
/// </remarks>
public sealed class ConfigReconciler(
    IServiceRepository serviceRepository,
    ICheckRepository checkRepository,
    IAlertConfigRepository alertConfigRepository,
    ServiceAppService services,
    CheckAppService checks,
    TagAppService tags,
    ConfigValidator validator,
    ICheckSchedulerService scheduler,
    IUnitOfWork unitOfWork)
{
    /// <summary>Computes what would change, writing nothing.</summary>
    public async Task<ConfigPlanDto> PlanAsync(ConfigApplyRequest request, CancellationToken ct = default)
    {
        var (plan, _) = await BuildPlanAsync(request, ct);
        return plan;
    }

    /// <summary>
    /// Applies the documents in one transaction. Returns the plan that was carried out, or the plan's
    /// validation errors with nothing written.
    /// </summary>
    public async Task<ConfigPlanDto> ApplyAsync(ConfigApplyRequest request, CancellationToken ct = default)
    {
        var (plan, work) = await BuildPlanAsync(request, ct);
        if (plan.Errors.Count > 0 || work is null) return plan;

        var touched = new List<Check>();

        await unitOfWork.BeginAsync(ct);
        try
        {
            await ExecuteAsync(work, touched, ct);
            await unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await unitOfWork.RollbackAsync(ct);
            throw;
        }

        // Scheduling happens after the commit, following the existing write path. A failure here
        // leaves the database correct and the scheduler stale, so it is reported rather than
        // swallowed — a bulk apply widens that window from one check to many (§8).
        var schedulingErrors = await ReconcileSchedulesAsync(touched, ct);

        return plan with { Applied = true, SchedulingErrors = schedulingErrors };
    }

    // ── Planning ────────────────────────────────────────────────────────────

    private async Task<(ConfigPlanDto Plan, PlannedWork? Work)> BuildPlanAsync(
        ConfigApplyRequest request, CancellationToken ct)
    {
        var errors = new List<ConfigValidationError>();

        if (request.Documents.Count == 0)
        {
            // Refusing an empty payload matters most under prune, where "no documents" would otherwise
            // read as "delete everything" — the exact shape of an unmatched glob (§4.6).
            errors.Add(new ConfigValidationError("No configuration documents were supplied."));
            return (EmptyPlan(errors), null);
        }

        var parsed = new List<(ConfigDocumentSource Source, ConfigDocument Document)>();
        foreach (var source in request.Documents)
            if (ConfigYamlParser.Parse(source, errors) is { } document)
                parsed.Add((source, document));

        var declared = validator.Validate(parsed, errors);
        if (errors.Count > 0) return (EmptyPlan(errors), null);

        var work = new PlannedWork();
        var changes = new List<ConfigResourceChange>();
        var untouched = new List<string>();

        var existingServices = (await serviceRepository.GetAllAsync(ct)).ToList();
        var byslug = existingServices.ToDictionary(s => s.Slug, StringComparer.OrdinalIgnoreCase);

        foreach (var service in declared)
        {
            if (byslug.TryGetValue(service.Slug, out var existing))
                await PlanExistingServiceAsync(service, existing, work, changes, untouched, request.Prune, ct);
            else
                PlanNewService(service, work, changes);
        }

        if (request.Prune)
            await PlanPrunedServicesAsync(declared, existingServices, work, changes, ct);
        else
            untouched.AddRange(existingServices
                .Where(s => !declared.Any(d => string.Equals(d.Slug, s.Slug, StringComparison.OrdinalIgnoreCase)))
                .Select(s => s.Slug));

        var plan = new ConfigPlanDto(
            Applied: false,
            Summary: Summarize(changes, untouched.Count),
            Changes: changes,
            Errors: [],
            Untouched: untouched,
            SchedulingErrors: []);

        return (plan, work);
    }

    private static void PlanNewService(ValidatedService service, PlannedWork work, List<ConfigResourceChange> changes)
    {
        work.CreateServices.Add(service);
        changes.Add(new ConfigResourceChange(
            ConfigResourceKind.Service, ConfigChangeAction.Create, service.Slug,
            Path: service.Path, Line: service.Node.Line));

        foreach (var check in service.Checks)
        {
            changes.Add(new ConfigResourceChange(
                ConfigResourceKind.Check, ConfigChangeAction.Create, check.Slug, service.Slug,
                service.Path, check.Node.Line));

            foreach (var alert in check.AlertConfigs)
                changes.Add(new ConfigResourceChange(
                    ConfigResourceKind.AlertConfig, ConfigChangeAction.Create, alert.Spec.Name,
                    $"{service.Slug}/{check.Slug}", service.Path, alert.Node.Line));
        }
    }

    private async Task PlanExistingServiceAsync(
        ValidatedService service, Service existing, PlannedWork work,
        List<ConfigResourceChange> changes, List<string> untouched, bool prune, CancellationToken ct)
    {
        var fields = DiffService(service, existing);
        work.UpdateServices.Add((service, existing));

        changes.Add(new ConfigResourceChange(
            ConfigResourceKind.Service,
            fields.Count > 0 ? ConfigChangeAction.Update : ConfigChangeAction.NoOp,
            service.Slug, Path: service.Path, Line: service.Node.Line, Fields: fields));

        var existingChecks = (await checkRepository.GetByServiceIdAsync(existing.Id, ct)).ToList();
        var checksBySlug = existingChecks.ToDictionary(c => c.Slug, StringComparer.OrdinalIgnoreCase);

        foreach (var check in service.Checks)
        {
            if (!checksBySlug.TryGetValue(check.Slug, out var existingCheck))
            {
                work.CreateChecks.Add((service, check));
                changes.Add(new ConfigResourceChange(
                    ConfigResourceKind.Check, ConfigChangeAction.Create, check.Slug, service.Slug,
                    service.Path, check.Node.Line));
                foreach (var alert in check.AlertConfigs)
                    changes.Add(new ConfigResourceChange(
                        ConfigResourceKind.AlertConfig, ConfigChangeAction.Create, alert.Spec.Name,
                        $"{service.Slug}/{check.Slug}", service.Path, alert.Node.Line));
                continue;
            }

            // Type is immutable, so changing it is a replace that destroys the check's history. The
            // plan has to say so — a user who edits one word in a file is not expecting data loss.
            if (existingCheck.Type != check.Type)
            {
                work.ReplaceChecks.Add((service, check, existingCheck));
                changes.Add(new ConfigResourceChange(
                    ConfigResourceKind.Check, ConfigChangeAction.Delete, check.Slug, service.Slug,
                    service.Path, check.Node.Line,
                    Warnings:
                    [
                        $"Type changes from {existingCheck.Type} to {check.Type}. A check's type is "
                        + "immutable, so this is a delete and a create.",
                        HistoryLossWarning,
                    ]));
                changes.Add(new ConfigResourceChange(
                    ConfigResourceKind.Check, ConfigChangeAction.Create, check.Slug, service.Slug,
                    service.Path, check.Node.Line));
                continue;
            }

            var checkFields = DiffCheck(check, existingCheck);
            work.UpdateChecks.Add((service, check, existingCheck));
            changes.Add(new ConfigResourceChange(
                ConfigResourceKind.Check,
                checkFields.Count > 0 ? ConfigChangeAction.Update : ConfigChangeAction.NoOp,
                check.Slug, service.Slug, service.Path, check.Node.Line, checkFields));

            await PlanAlertConfigsAsync(service, check, existingCheck, changes, prune, ct);
        }

        foreach (var orphan in existingChecks.Where(
                     c => !service.Checks.Any(d => string.Equals(d.Slug, c.Slug, StringComparison.OrdinalIgnoreCase))))
        {
            if (prune)
            {
                work.DeleteChecks.Add((existing, orphan));
                changes.Add(new ConfigResourceChange(
                    ConfigResourceKind.Check, ConfigChangeAction.Delete, orphan.Slug, service.Slug,
                    Warnings: [HistoryLossWarning]));
            }
            else
            {
                untouched.Add($"{service.Slug}/{orphan.Slug}");
            }
        }
    }

    private async Task PlanAlertConfigsAsync(
        ValidatedService service, ValidatedCheck check, Check existingCheck,
        List<ConfigResourceChange> changes, bool prune, CancellationToken ct)
    {
        // Absent alert_configs means "the file says nothing about alerts", which under patch
        // semantics leaves them alone entirely — including under prune.
        if (check.Node.AlertConfigs is null) return;

        var existing = (await alertConfigRepository.GetByCheckIdAsync(existingCheck.Id, ct)).ToList();
        var byDimension = existing.ToDictionary(a => a.Dimension, StringComparer.OrdinalIgnoreCase);
        var parent = $"{service.Slug}/{check.Slug}";

        foreach (var alert in check.AlertConfigs)
        {
            if (byDimension.TryGetValue(alert.Spec.Name, out var row))
            {
                var fields = DiffAlertConfig(alert, row);
                changes.Add(new ConfigResourceChange(
                    ConfigResourceKind.AlertConfig,
                    fields.Count > 0 ? ConfigChangeAction.Update : ConfigChangeAction.NoOp,
                    alert.Spec.Name, parent, service.Path, alert.Node.Line, fields));
            }
            else
            {
                changes.Add(new ConfigResourceChange(
                    ConfigResourceKind.AlertConfig, ConfigChangeAction.Create, alert.Spec.Name,
                    parent, service.Path, alert.Node.Line));
            }
        }

        // A declared alert_configs list is a complete statement about that check's rules, so a rule
        // it omits is removed even without --prune. Deleting an alert rule loses no measurement
        // history, unlike deleting a check.
        foreach (var orphan in existing.Where(
                     a => !check.AlertConfigs.Any(d => string.Equals(d.Spec.Name, a.Dimension, StringComparison.OrdinalIgnoreCase))))
            changes.Add(new ConfigResourceChange(
                ConfigResourceKind.AlertConfig, ConfigChangeAction.Delete, orphan.Dimension, parent));
    }

    private async Task PlanPrunedServicesAsync(
        IReadOnlyList<ValidatedService> declared, List<Service> existingServices,
        PlannedWork work, List<ConfigResourceChange> changes, CancellationToken ct)
    {
        foreach (var orphan in existingServices.Where(
                     s => !declared.Any(d => string.Equals(d.Slug, s.Slug, StringComparison.OrdinalIgnoreCase))))
        {
            work.DeleteServices.Add(orphan);

            var orphanChecks = (await checkRepository.GetByServiceIdAsync(orphan.Id, ct)).ToList();
            changes.Add(new ConfigResourceChange(
                ConfigResourceKind.Service, ConfigChangeAction.Delete, orphan.Slug,
                Warnings: orphanChecks.Count > 0
                    ? [$"Deletes {orphanChecks.Count} check(s) with it. {HistoryLossWarning}"]
                    : null));

            foreach (var check in orphanChecks)
                changes.Add(new ConfigResourceChange(
                    ConfigResourceKind.Check, ConfigChangeAction.Delete, check.Slug, orphan.Slug,
                    Warnings: [HistoryLossWarning]));
        }
    }

    // ── Diffing ─────────────────────────────────────────────────────────────

    private static List<ConfigFieldChange> DiffService(ValidatedService service, Service existing)
    {
        var fields = new List<ConfigFieldChange>();
        var node = service.Node;

        Compare(fields, "name", existing.Name, node.Name);
        Compare(fields, "description", existing.Description, node.Description);
        Compare(fields, "is_hidden", existing.IsHidden, node.IsHidden);
        Compare(fields, "display_order", existing.DisplayOrder, node.DisplayOrder);
        Compare(fields, "image_url", existing.ImageUrl, node.ImageUrl);
        Compare(fields, "default_status", existing.DefaultStatus, service.DefaultStatus);

        return fields;
    }

    private static List<ConfigFieldChange> DiffCheck(ValidatedCheck check, Check existing)
    {
        var fields = new List<ConfigFieldChange>();
        var node = check.Node;

        Compare(fields, "name", existing.Name, node.Name);
        Compare(fields, "description", existing.Description, node.Description);
        Compare(fields, "cron", existing.Cron, node.Cron);
        Compare(fields, "is_active", existing.IsActive, node.IsActive);

        // type_data is compared as canonical JSON so key order and whitespace do not read as a change.
        if (node.TypeData is not null)
        {
            var before = CanonicalJson(existing.TypeDataJson);
            var after = CanonicalJson(check.TypeDataJson);
            if (before != after) fields.Add(new ConfigFieldChange("type_data", before, after));
        }

        return fields;
    }

    private static List<ConfigFieldChange> DiffAlertConfig(ValidatedAlertConfig alert, AlertConfig existing)
    {
        var fields = new List<ConfigFieldChange>();
        var node = alert.Node;

        Compare(fields, "alert_value", existing.AlertValue, node.AlertValue);
        Compare(fields, "failure_threshold", existing.FailureThreshold, node.FailureThreshold);
        Compare(fields, "success_threshold", existing.SuccessThreshold, node.SuccessThreshold);
        Compare(fields, "min_failing_regions", existing.MinFailingRegions, node.MinFailingRegions);
        Compare(fields, "description", existing.Description, node.Description);
        Compare(fields, "is_active", existing.IsActive, node.IsActive);
        Compare(fields, "severity", existing.Severity, alert.Severity);

        return fields;
    }

    /// <summary>
    /// Records a field change only when the document declared it. A null <paramref name="declared"/>
    /// means the file was silent, which is never a change — the whole patch model in one method.
    /// </summary>
    private static void Compare<T>(List<ConfigFieldChange> fields, string name, T? current, T? declared)
    {
        if (declared is null) return;
        if (EqualityComparer<T>.Default.Equals(current, declared)) return;
        fields.Add(new ConfigFieldChange(name, Render(current), Render(declared)));
    }

    private static string? Render<T>(T? value) => value switch
    {
        null => null,
        bool b => b ? "true" : "false",
        _ => value.ToString(),
    };

    /// <summary>Reserializes JSON with sorted keys so an equivalent config never reads as a diff.</summary>
    private static string CanonicalJson(string json)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);
            return Canonicalize(document.RootElement);
        }
        catch (System.Text.Json.JsonException)
        {
            return json;
        }
    }

    private static string Canonicalize(System.Text.Json.JsonElement element)
    {
        switch (element.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Object:
                var members = element.EnumerateObject()
                    .OrderBy(p => p.Name, StringComparer.Ordinal)
                    .Select(p => $"{System.Text.Json.JsonSerializer.Serialize(p.Name)}:{Canonicalize(p.Value)}");
                return $"{{{string.Join(",", members)}}}";
            case System.Text.Json.JsonValueKind.Array:
                return $"[{string.Join(",", element.EnumerateArray().Select(Canonicalize))}]";
            default:
                return element.GetRawText();
        }
    }

    // ── Applying ────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs the planned writes in the order §4.3 prescribes: create services, then create and update
    /// checks, then delete checks, then delete services — so a pruned service's checks are removed
    /// before the service itself.
    /// </summary>
    private async Task ExecuteAsync(PlannedWork work, List<Check> touched, CancellationToken ct)
    {
        foreach (var service in work.CreateServices)
        {
            await services.CreateAsync(new CreateServiceRequest(
                service.Slug,
                service.Node.Name!,
                service.Node.Description,
                service.Node.ImageUrl,
                service.DefaultStatus ?? ServiceStatus.NO_DATA,
                service.Node.IsHidden ?? false,
                service.Node.DisplayOrder ?? 0), ct);

            foreach (var check in service.Checks)
                touched.Add(await CreateCheckAsync(service, check, ct));
        }

        foreach (var (service, _) in work.UpdateServices)
            await services.UpdateAsync(service.Slug, new UpdateServiceRequest(
                service.Node.Name,
                service.Node.Description,
                service.Node.ImageUrl,
                service.DefaultStatus,
                service.Node.IsHidden,
                service.Node.DisplayOrder,
                // Never touched: escalation policies are outside the config surface, and omitting the
                // patch is what keeps an apply from detaching them (§4.4).
                EscalationPolicyId: null), ct);

        foreach (var (service, check) in work.CreateChecks)
            touched.Add(await CreateCheckAsync(service, check, ct));

        foreach (var (service, check, existing) in work.ReplaceChecks)
        {
            await checks.DeleteAsync(service.Slug, existing.Slug, ct);
            touched.Add(await CreateCheckAsync(service, check, ct));
        }

        foreach (var (service, check, existing) in work.UpdateChecks)
        {
            var updated = await checks.UpdateAsync(service.Slug, check.Slug, new UpdateCheckRequest(
                check.Node.Name,
                check.Node.Description,
                check.Node.Cron,
                check.Node.TypeData is null ? null : check.TypeDataJson,
                check.Node.IsActive), ct);

            await ApplyWorkerTagsAsync(existing.Id, check, ct);
            await ApplyAlertConfigsAsync(existing.Id, check, ct);

            touched.Add(await checkRepository.GetByIdAsync(updated.Id, ct) ?? existing);
        }

        foreach (var (service, check) in work.DeleteChecks)
            await checks.DeleteAsync(service.Slug, check.Slug, ct);

        foreach (var service in work.DeleteServices)
            await services.DeleteAsync(service.Slug, ct);
    }

    private async Task<Check> CreateCheckAsync(ValidatedService service, ValidatedCheck check, CancellationToken ct)
    {
        // Alert configs ride along on create, so CheckAppService builds them from the dimension spec
        // in the same transaction rather than us reimplementing that mapping.
        var created = await checks.CreateAsync(service.Slug, new CreateCheckRequest(
            check.Slug,
            check.Node.Name!,
            check.Node.Description,
            check.Type,
            check.Node.Cron!,
            check.TypeDataJson,
            check.Node.IsActive ?? true,
            IntegrationId: null,
            AlertConfigs: [.. check.AlertConfigs.Select(ToCreateRequest)]), ct);

        await ApplyWorkerTagsAsync(created.Id, check, ct);

        return await checkRepository.GetByIdAsync(created.Id, ct)
               ?? throw new InvalidOperationException($"Check '{check.Slug}' vanished after creation.");
    }

    private static CreateAlertConfigRequest ToCreateRequest(ValidatedAlertConfig alert) =>
        new(alert.Spec.Name,
            alert.Node.AlertValue!,
            alert.Node.FailureThreshold ?? 1,
            alert.Node.SuccessThreshold ?? 1,
            alert.Node.MinFailingRegions ?? 1,
            alert.Node.Description,
            alert.Node.IsActive ?? true,
            alert.Severity ?? AlertSeverity.Warning);

    private async Task ApplyWorkerTagsAsync(int checkId, ValidatedCheck check, CancellationToken ct)
    {
        if (check.Node.RequiredWorkerTags is not { } declared) return;

        await tags.ReplaceRequiredWorkerTagsAsync(checkId,
            new ReplaceTagsRequest([.. declared.Select(t => new TagDto(t.Key, t.Value))]), ct);
    }

    /// <summary>
    /// Reconciles an existing check's alert rules by dimension. Matching in place is what preserves
    /// <see cref="AlertConfig.IsAlerting"/>, so editing a threshold does not re-notify an alert that
    /// was already firing — the reason dimension is used as identity at all.
    /// </summary>
    private async Task ApplyAlertConfigsAsync(int checkId, ValidatedCheck check, CancellationToken ct)
    {
        if (check.Node.AlertConfigs is null) return;

        var existing = (await alertConfigRepository.GetByCheckIdAsync(checkId, ct)).ToList();
        var byDimension = existing.ToDictionary(a => a.Dimension, StringComparer.OrdinalIgnoreCase);

        foreach (var alert in check.AlertConfigs)
        {
            if (byDimension.TryGetValue(alert.Spec.Name, out var row))
            {
                if (alert.Node.AlertValue is not null) row.AlertValue = alert.Node.AlertValue;
                if (alert.Node.FailureThreshold is { } f) row.FailureThreshold = f;
                if (alert.Node.SuccessThreshold is { } s) row.SuccessThreshold = s;
                if (alert.Node.MinFailingRegions is { } m) row.MinFailingRegions = m;
                if (alert.Node.Description is not null) row.Description = alert.Node.Description;
                if (alert.Node.IsActive is { } active) row.IsActive = active;
                if (alert.Severity is { } severity) row.Severity = severity;

                // Comparison and Direction are re-copied from the spec rather than left as stored, so a
                // rule written before the check changed its dimension's semantics is corrected.
                row.Comparison = alert.Spec.Comparison;
                row.Direction = alert.Spec.Direction;

                await alertConfigRepository.UpdateAsync(row, ct);
            }
            else
            {
                await alertConfigRepository.CreateAsync(new AlertConfig
                {
                    CheckId = checkId,
                    Dimension = alert.Spec.Name,
                    Comparison = alert.Spec.Comparison,
                    Direction = alert.Spec.Direction,
                    AlertValue = alert.Node.AlertValue!,
                    FailureThreshold = alert.Node.FailureThreshold ?? 1,
                    SuccessThreshold = alert.Node.SuccessThreshold ?? 1,
                    MinFailingRegions = alert.Node.MinFailingRegions ?? 1,
                    Description = alert.Node.Description,
                    IsActive = alert.Node.IsActive ?? true,
                    Severity = alert.Severity ?? AlertSeverity.Warning,
                }, ct);
            }
        }

        foreach (var orphan in existing.Where(
                     a => !check.AlertConfigs.Any(d => string.Equals(d.Spec.Name, a.Dimension, StringComparison.OrdinalIgnoreCase))))
            await alertConfigRepository.DeleteAsync(orphan, ct);
    }

    /// <summary>
    /// Re-asserts the Quartz trigger for every check the apply touched, reading each back so the
    /// trigger matches what was actually committed rather than what was planned.
    /// </summary>
    /// <remarks>
    /// The application services already schedule on create and update, so this is normally a no-op
    /// reassertion. It exists because scheduling runs outside the transaction: a bulk apply widens
    /// the failure window from one check to many, and a scheduler error must surface in the response
    /// so the CLI can exit non-zero rather than report a success the scheduler never enacted (§8).
    /// </remarks>
    private async Task<IReadOnlyList<string>> ReconcileSchedulesAsync(List<Check> touched, CancellationToken ct)
    {
        var errors = new List<string>();

        foreach (var check in touched.DistinctBy(c => c.Id))
        {
            try
            {
                // Deleted between commit and here, or replaced — nothing left to schedule.
                if (await checkRepository.GetByIdAsync(check.Id, ct) is not { } current) continue;
                await scheduler.ScheduleAsync(current, ct);
            }
            catch (Exception ex)
            {
                errors.Add($"Check '{check.Slug}' was saved but could not be scheduled: {ex.Message}");
            }
        }

        return errors;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private const string HistoryLossWarning =
        "Deleting a check permanently discards its measurement history.";

    private static ConfigPlanDto EmptyPlan(IReadOnlyList<ConfigValidationError> errors) =>
        new(false, new ConfigPlanSummary(0, 0, 0, 0, 0), [], errors, [], []);

    private static ConfigPlanSummary Summarize(List<ConfigResourceChange> changes, int untouched) =>
        new(changes.Count(c => c.Action == ConfigChangeAction.Create),
            changes.Count(c => c.Action == ConfigChangeAction.Update),
            changes.Count(c => c.Action == ConfigChangeAction.Delete),
            changes.Count(c => c.Action == ConfigChangeAction.NoOp),
            untouched);

    /// <summary>The writes a plan resolved to, held between planning and applying.</summary>
    private sealed class PlannedWork
    {
        public List<ValidatedService> CreateServices { get; } = [];
        public List<(ValidatedService Service, Service Existing)> UpdateServices { get; } = [];
        public List<Service> DeleteServices { get; } = [];
        public List<(ValidatedService Service, ValidatedCheck Check)> CreateChecks { get; } = [];
        public List<(ValidatedService Service, ValidatedCheck Check, Check Existing)> UpdateChecks { get; } = [];
        public List<(ValidatedService Service, ValidatedCheck Check, Check Existing)> ReplaceChecks { get; } = [];
        public List<(Service Service, Check Check)> DeleteChecks { get; } = [];
    }
}
