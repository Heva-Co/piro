using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.Interfaces;
using Piro.Domain.Entities;

namespace Piro.Api.Controllers;

/// <summary>Manages email provider configuration (SMTP or Resend).</summary>
[ApiController]
[Route("api/v1/email")]
[Produces("application/json")]
[Authorize(Roles = "Owner,Admin")]
public class EmailConfigController(
    IEmailConfigRepository emailConfig,
    IEmailService emailService,
    UserManager<AppUser> userManager) : ControllerBase
{
    /// <summary>Returns current email configuration. Passwords are masked.</summary>
    [HttpGet("config")]
    [ProducesResponseType<EmailConfigResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConfig(CancellationToken ct)
    {
        var config = await emailConfig.GetAsync(ct);
        return Ok(new EmailConfigResponse(
            Provider:        config.Provider ?? "smtp",
            SmtpHost:        config.SmtpHost,
            SmtpPort:        config.SmtpPort,
            SmtpUsername:    config.SmtpUsername,
            HasSmtpPassword: !string.IsNullOrEmpty(config.SmtpPassword),
            SmtpFrom:        config.SmtpFrom,
            SmtpUseTls:      config.SmtpUseTls,
            HasResendApiKey: !string.IsNullOrEmpty(config.ResendApiKey),
            ResendFrom:      config.ResendFrom
        ));
    }

    /// <summary>Saves email provider configuration. Leave password fields null/empty to keep existing values.</summary>
    [HttpPut("config")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> PutConfig([FromBody] UpdateEmailConfigRequest request, CancellationToken ct)
    {
        var existing = await emailConfig.GetAsync(ct);

        var updated = new EmailProviderConfig(
            Provider:     request.Provider,
            SmtpHost:     request.SmtpHost,
            SmtpPort:     request.SmtpPort,
            SmtpUsername: request.SmtpUsername,
            SmtpPassword: string.IsNullOrEmpty(request.SmtpPassword) ? existing.SmtpPassword : request.SmtpPassword,
            SmtpFrom:     request.SmtpFrom,
            SmtpUseTls:   request.SmtpUseTls,
            ResendApiKey: string.IsNullOrEmpty(request.ResendApiKey) ? existing.ResendApiKey : request.ResendApiKey,
            ResendFrom:   request.ResendFrom
        );

        await emailConfig.SetAsync(updated, ct);
        return NoContent();
    }

    /// <summary>Sends a test email to the currently authenticated user.</summary>
    [HttpPost("config/test")]
    [ProducesResponseType<TestEmailResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> TestConfig(CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user?.Email is null) return BadRequest(new { title = "Could not resolve current user email.", status = 400 });

        await emailService.SendAsync(
            user.Email,
            "Piro — Test Email",
            "<p>This is a test email from <strong>Piro</strong>. If you received this, your email configuration is working correctly.</p>",
            ct);

        return Ok(new TestEmailResponse($"Test email sent to {user.Email}."));
    }
}

public record EmailConfigResponse(
    string  Provider,
    string? SmtpHost,
    int?    SmtpPort,
    string? SmtpUsername,
    bool    HasSmtpPassword,
    string? SmtpFrom,
    bool?   SmtpUseTls,
    bool    HasResendApiKey,
    string? ResendFrom);

public record UpdateEmailConfigRequest(
    string? Provider,
    string? SmtpHost,
    int?    SmtpPort,
    string? SmtpUsername,
    string? SmtpPassword,
    string? SmtpFrom,
    bool?   SmtpUseTls,
    string? ResendApiKey,
    string? ResendFrom);

public record TestEmailResponse(string Message);
