using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.Constants;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Domain.Extensions;

namespace Piro.Api.Controllers;

/// <summary>First-run setup wizard. Accessible only when no Owner account exists yet.</summary>
[ApiController]
[Route("api/v1/setup")]
[Produces("application/json")]
public class SetupController(
    UserManager<AppUser> userManager,
    RoleManager<AppRole> roleManager,
    ISiteConfigRepository siteConfigRepo,
    IEmailConfigRepository emailConfigRepo,
    IWorkerRegistrationRepository workerRepo,
    IUnitOfWork uow) : ControllerBase
{
    private const string OwnerRole = "Owner";

    /// <summary>Returns whether initial setup is still required.</summary>
    [HttpGet("status")]
    [ProducesResponseType<SetupStatusResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var isComplete = await HasOwnerAsync();
        return Ok(new SetupStatusResponse(isComplete));
    }

    /// <summary>
    /// Completes first-run setup: creates Owner account, seeds roles,
    /// and saves site + email configuration atomically.
    /// Can only be called once — subsequent calls are rejected if an Owner already exists.
    /// </summary>
    [HttpPost("complete")]
    [ProducesResponseType<SetupStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Complete([FromBody] CompleteSetupRequest request, CancellationToken ct)
    {
        if (await HasOwnerAsync())
            return Conflict(new { title = "Setup already completed.", status = 409 });

        await uow.BeginAsync(ct);
        try
        {
            // Seed built-in roles
            await SeedRolesAsync();

            // Create owner user
            var user = new AppUser
            {
                UserName = request.Email,
                Email = request.Email,
                Name = request.Name,
                IsActive = true,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                await uow.RollbackAsync(ct);
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                return BadRequest(new { title = "Failed to create owner account.", detail = errors, status = 400 });
            }

            await userManager.AddToRoleAsync(user, OwnerRole);

            // Save site config
            if (!string.IsNullOrWhiteSpace(request.SiteTitle))
                await siteConfigRepo.SetAsync(SiteDataKeys.SiteName, request.SiteTitle, ct);
            if (!string.IsNullOrWhiteSpace(request.SiteUrl))
                await siteConfigRepo.SetAsync(SiteDataKeys.SiteUrl, request.SiteUrl, ct);

            // Save email config
            if (!string.IsNullOrWhiteSpace(request.EmailHost) || !string.IsNullOrWhiteSpace(request.ResendApiKey))
            {
                var provider = string.IsNullOrWhiteSpace(request.ResendApiKey) ? EmailProvider.Smtp : EmailProvider.Resend;
                var isResend = provider == EmailProvider.Resend;
                var cfg = new EmailProviderConfig(
                    Provider:     provider.ToStorageString(),
                    SmtpHost:     isResend ? null : request.EmailHost,
                    SmtpPort:     isResend ? null : request.EmailPort ?? 587,
                    SmtpUsername: isResend ? null : request.EmailUsername,
                    SmtpPassword: isResend ? null : request.EmailPassword,
                    SmtpFrom:     isResend ? null : request.EmailFrom,
                    SmtpUseTls:   isResend ? null : request.EmailUseSsl ?? true,
                    ResendApiKey: isResend ? request.ResendApiKey : null,
                    ResendFrom:   isResend ? request.EmailFrom : null
                );
                await emailConfigRepo.SetAsync(cfg, ct);
            }

            // Seed built-in API worker so it always appears in the Workers UI
            await SeedBuiltInWorkerAsync(ct);

            await uow.CommitAsync(ct);
            return Ok(new SetupStatusResponse(false));
        }
        catch
        {
            await uow.RollbackAsync(ct);
            throw;
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<bool> HasOwnerAsync()
    {
        var owners = await userManager.GetUsersInRoleAsync(OwnerRole);
        return owners.Count > 0;
    }

    // Well-known ID for the built-in API worker — must match ApiWorkerHostedService.ApiWorkerId
    private static readonly Guid BuiltInWorkerId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private async Task SeedBuiltInWorkerAsync(CancellationToken ct)
    {
        var existing = await workerRepo.GetByIdAsync(BuiltInWorkerId, ct);
        if (existing is null)
        {
            await workerRepo.CreateAsync(new WorkerRegistration
            {
                Id = BuiltInWorkerId,
                Name = "API (built-in)",
                Region = "default",
                WorkerTokenHash = "builtin",
                IsActive = true,
                IsBuiltIn = true,
                IsDefault = true,
                CreatedAt = DateTime.UtcNow,
            }, ct);
        }
    }

    private async Task SeedRolesAsync()
    {
        string[] roles = [OwnerRole, "Admin", "Member", "Viewer"];
        foreach (var name in roles)
        {
            if (!await roleManager.RoleExistsAsync(name))
                await roleManager.CreateAsync(new AppRole { Name = name, IsReadonly = true });
        }
    }
}

public record SetupStatusResponse(bool IsComplete);

public record CompleteSetupRequest(
    // User
    string Email,
    string Password,
    string Name,
    // Site
    string? SiteTitle,
    string? SiteUrl,
    // Email (SMTP)
    string? EmailHost,
    int? EmailPort,
    string? EmailUsername,
    string? EmailPassword,
    string? EmailFrom,
    bool? EmailUseSsl,
    // Email (Resend)
    string? ResendApiKey
);
