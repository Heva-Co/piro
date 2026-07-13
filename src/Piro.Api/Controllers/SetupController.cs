using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.Constants;
using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;
using Piro.Domain.Enums;
using Piro.Domain.Extensions;
using Piro.Infrastructure.Email;

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
    IUserNotificationPreferenceRepository prefRepo,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IUnitOfWork uow) : ControllerBase
{
    private const string OwnerRole = "Owner";
    private const int CodeWindowSeconds = 600; // 10 minutes

    /// <summary>
    /// Sends a verification code to the given address using not-yet-saved email settings,
    /// proving the config actually works before anything is persisted. Stateless — the code is
    /// an HMAC of the address, the exact config, and the current time window, verified the same
    /// way on confirm. No account or config exists yet at this point in the wizard.
    /// </summary>
    [HttpPost("email/test")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SendEmailVerificationCode([FromBody] SendSetupEmailCodeRequest request, CancellationToken ct)
    {
        if (await HasOwnerAsync())
            return Conflict(new { title = "Setup already completed.", status = 409 });

        var code = ComputeCode(request.Email, request.ToConfig(), DateTimeOffset.UtcNow);
        var subject = "Piro — Verify your email configuration";
        var html = $"<p>Your Piro setup verification code is:</p><h2 style=\"letter-spacing:4px\">{code}</h2>" +
                   "<p>This code expires in 10 minutes.</p>";

        try
        {
            if (request.Provider == "resend")
            {
                if (string.IsNullOrWhiteSpace(request.ResendApiKey) || string.IsNullOrWhiteSpace(request.ResendFrom))
                    return BadRequest(new { title = "Resend API key and from address are required.", status = 400 });

                var http = httpClientFactory.CreateClient();
                await ResendEmailService.SendWithConfigAsync(http, request.ResendApiKey, request.ResendFrom, request.Email, subject, html, ct);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(request.SmtpHost) || string.IsNullOrWhiteSpace(request.SmtpFrom))
                    return BadRequest(new { title = "SMTP host and from address are required.", status = 400 });

                await SmtpEmailService.SendWithConfigAsync(
                    request.SmtpHost, request.SmtpPort ?? 587, request.SmtpUseSsl ?? true,
                    request.SmtpUsername ?? string.Empty, request.SmtpPassword ?? string.Empty,
                    request.SmtpFrom, request.Email, subject, html, ct);
            }
        }
        catch (Exception ex)
        {
            return BadRequest(new { title = "Failed to send verification email.", detail = ex.Message, status = 400 });
        }

        return NoContent();
    }

    /// <summary>Confirms a verification code sent by <see cref="SendEmailVerificationCode"/>.</summary>
    [HttpPost("email/confirm")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult ConfirmEmailVerificationCode([FromBody] ConfirmSetupEmailCodeRequest request)
    {
        var now = DateTimeOffset.UtcNow;
        var matchesCurrentWindow = ComputeCode(request.Email, request.Config, now) == request.Code;
        var matchesPreviousWindow = ComputeCode(request.Email, request.Config, now.AddSeconds(-CodeWindowSeconds)) == request.Code;

        if (!matchesCurrentWindow && !matchesPreviousWindow)
            return BadRequest(new { title = "Invalid or expired code.", status = 400 });

        return NoContent();
    }

    /// <summary>
    /// HMAC-SHA256 of the address + a stable serialization of the email config + the current
    /// 10-minute time window, truncated to a 6-digit code. Binding the config in means changing
    /// any SMTP/Resend field invalidates a previously-issued code, so confirming a code actually
    /// proves the config the user is about to save works — not just that they own the mailbox.
    /// </summary>
    private string ComputeCode(string email, SetupEmailConfigPayload config, DateTimeOffset at)
    {
        var jwtSecret = configuration["Auth:JwtSecret"]
            ?? throw new InvalidOperationException("Auth:JwtSecret is required.");

        var configJson = JsonSerializer.Serialize(config);
        var window = at.ToUnixTimeSeconds() / CodeWindowSeconds;
        var message = $"{email}|{configJson}|{window}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(jwtSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));

        // Take the first 4 bytes as a uint and reduce to 6 digits — same technique as HOTP/TOTP (RFC 4226 §5.3).
        var truncated = BitConverter.ToUInt32(hash, 0) % 1_000_000;
        return truncated.ToString("D6");
    }

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
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Complete([FromBody] CompleteSetupRequest request, CancellationToken ct)
    {
        if (await HasOwnerAsync())
            return Conflict(new { title = "Setup already completed.", status = 409 });

        var emailConfig = new SetupEmailConfigPayload(
            request.EmailProvider, request.EmailHost, request.EmailPort, request.EmailUsername,
            request.EmailPassword, request.EmailFrom, request.EmailUseSsl, request.ResendApiKey, request.ResendFrom);
        var now = DateTimeOffset.UtcNow;
        var codeValid =
            ComputeCode(request.Email, emailConfig, now) == request.EmailVerificationCode ||
            ComputeCode(request.Email, emailConfig, now.AddSeconds(-CodeWindowSeconds)) == request.EmailVerificationCode;
        if (!codeValid)
            return BadRequest(new { title = "Email configuration was not verified, or the config changed since verifying.", status = 400 });

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
                EmailConfirmed = true,
            };
            if (!string.IsNullOrWhiteSpace(request.TimeZone) &&
                TimeZoneInfo.TryFindSystemTimeZoneById(request.TimeZone, out _))
            {
                user.TimeZone = request.TimeZone;
            }

            var result = await userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                await uow.RollbackAsync(ct);
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                return BadRequest(new { title = "Failed to create owner account.", detail = errors, status = 400 });
            }

            await userManager.AddToRoleAsync(user, OwnerRole);

            // Every user gets one auto-created, always-present Email preference mirroring their
            // account address — see UserManagementService.AcceptInviteAsync for the same seeding
            // on the normal invite path; the Owner created here skips that path entirely.
            await prefRepo.CreateAsync(new UserNotificationPreference
            {
                UserId = user.Id,
                Channel = PersonalNotificationChannel.Email,
                Handle = user.Email!,
                Priority = 0,
                VerifiedAt = DateTimeOffset.UtcNow,
                IsAccountFallback = true,
            }, ct);

            // Save site config
            if (!string.IsNullOrWhiteSpace(request.SiteTitle))
                await siteConfigRepo.SetAsync(SiteDataKeys.SiteName, request.SiteTitle, ct);
            if (!string.IsNullOrWhiteSpace(request.SiteUrl))
                await siteConfigRepo.SetAsync(SiteDataKeys.SiteUrl, request.SiteUrl, ct);

            // Save email config
            {
                var isResend = request.EmailProvider == "resend";
                var cfg = new EmailProviderConfig(
                    Provider:     (isResend ? EmailProvider.Resend : EmailProvider.Smtp).ToStorageString(),
                    SmtpHost:     isResend ? null : request.EmailHost,
                    SmtpPort:     isResend ? null : request.EmailPort ?? 587,
                    SmtpUsername: isResend ? null : request.EmailUsername,
                    SmtpPassword: isResend ? null : request.EmailPassword,
                    SmtpFrom:     isResend ? null : request.EmailFrom,
                    SmtpUseTls:   isResend ? null : request.EmailUseSsl ?? true,
                    ResendApiKey: isResend ? request.ResendApiKey : null,
                    ResendFrom:   isResend ? request.ResendFrom : null
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
    string? TimeZone,
    // Site
    string? SiteTitle,
    string? SiteUrl,
    // Email — provider and verification code from the /email/test + /email/confirm step
    string EmailProvider,
    string EmailVerificationCode,
    // Email (SMTP)
    string? EmailHost,
    int? EmailPort,
    string? EmailUsername,
    string? EmailPassword,
    string? EmailFrom,
    bool? EmailUseSsl,
    // Email (Resend)
    string? ResendApiKey,
    string? ResendFrom
);

/// <summary>The email config fields the verification code's HMAC is bound to — changing any of
/// these after requesting a code invalidates it, so confirming proves this exact config works.</summary>
public record SetupEmailConfigPayload(
    string Provider,
    string? SmtpHost,
    int? SmtpPort,
    string? SmtpUsername,
    string? SmtpPassword,
    string? SmtpFrom,
    bool? SmtpUseSsl,
    string? ResendApiKey,
    string? ResendFrom
);

public record SendSetupEmailCodeRequest(
    string Email,
    string Provider,
    string? SmtpHost,
    int? SmtpPort,
    string? SmtpUsername,
    string? SmtpPassword,
    string? SmtpFrom,
    bool? SmtpUseSsl,
    string? ResendApiKey,
    string? ResendFrom
)
{
    public SetupEmailConfigPayload ToConfig() => new(
        Provider, SmtpHost, SmtpPort, SmtpUsername, SmtpPassword, SmtpFrom, SmtpUseSsl, ResendApiKey, ResendFrom);
}

public record ConfirmSetupEmailCodeRequest(string Email, string Code, SetupEmailConfigPayload Config);
