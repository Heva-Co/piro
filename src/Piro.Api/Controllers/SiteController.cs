using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.Constants;
using Piro.Application.Interfaces;

namespace Piro.Api.Controllers;

/// <summary>Manages site-level configuration: name, URL, logo, favicon, and SEO meta tags.</summary>
[ApiController]
[Route("api/v1/site")]
[Produces("application/json")]
public class SiteController(ISiteConfigRepository siteConfig, IWebHostEnvironment env) : ControllerBase
{
    private static readonly string[] AllowedExtensions = [".png", ".jpg", ".jpeg", ".svg", ".webp"];

    /// <summary>Returns current site configuration. Public — used by the status page and sign-in page.</summary>
    [HttpGet("config")]
    [AllowAnonymous]
    [ProducesResponseType<SiteConfigResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConfig(CancellationToken ct)
    {
        var cfg = await siteConfig.GetAsync(ct);
        return Ok(new SiteConfigResponse(cfg.Name, cfg.Url, cfg.LogoUrl, cfg.FaviconUrl,
            cfg.MetaTitle, cfg.MetaDescription, cfg.OgImageUrl));
    }

    /// <summary>Updates site text configuration fields.</summary>
    [HttpPut("config")]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PutConfig([FromBody] UpdateSiteConfigRequest request, CancellationToken ct)
    {
        await siteConfig.SetManyAsync(new Dictionary<string, string?>
        {
            [SiteDataKeys.SiteName] = request.Name,
            [SiteDataKeys.SiteUrl] = request.Url,
            [SiteDataKeys.SiteMetaTitle] = request.MetaTitle,
            [SiteDataKeys.SiteMetaDescription] = request.MetaDescription,
        }, ct);
        return NoContent();
    }

    /// <summary>Returns current incident automation configuration.</summary>
    [HttpGet("incidents-config")]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType<IncidentsConfigResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIncidentsConfig(CancellationToken ct)
    {
        var cfg = await siteConfig.GetAsync(ct);
        return Ok(new IncidentsConfigResponse(
            cfg.IncidentCorrelationMode.ToString(),
            cfg.GlobalIncidentThreshold,
            cfg.GlobalIncidentCorrelationWindowMinutes));
    }

    /// <summary>Updates incident automation settings.</summary>
    [HttpPut("incidents-config")]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PutIncidentsConfig([FromBody] UpdateIncidentsConfigRequest request, CancellationToken ct)
    {
        await siteConfig.SetManyAsync(new Dictionary<string, string?>
        {
            [SiteDataKeys.IncidentCorrelationMode] = request.CorrelationMode,
            [SiteDataKeys.IncidentGlobalThreshold] = request.GlobalThreshold?.ToString(),
            [SiteDataKeys.IncidentGlobalCorrelationWindowMinutes] = request.GlobalCorrelationWindowMinutes?.ToString(),
        }, ct);
        return NoContent();
    }

    /// <summary>Uploads a site asset (logo | favicon | og-image). Stores in wwwroot/uploads/.</summary>
    [HttpPost("upload/{type}")]
    [Authorize(Roles = "Owner,Admin")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<UploadResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload(string type, IFormFile file, CancellationToken ct)
    {
        if (type is not ("logo" or "favicon" or "og-image"))
            return BadRequest(new { title = "Invalid upload type. Use logo, favicon, or og-image.", status = 400 });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(new { title = $"Unsupported file type '{ext}'. Allowed: PNG, JPG, SVG, WebP.", status = 400 });

        var maxBytes = type == "og-image" ? 2 * 1024 * 1024 : 512 * 1024;
        if (file.Length > maxBytes)
            return BadRequest(new { title = $"File exceeds maximum size ({maxBytes / 1024} KB).", status = 400 });

        var webRoot = env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadsDir = Path.Combine(webRoot, "uploads");
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"{type}-{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        await using (var stream = System.IO.File.Create(filePath))
            await file.CopyToAsync(stream, ct);

        var url = $"/uploads/{fileName}";
        await siteConfig.SetAsync(UploadKeyFor(type)!, url, ct);

        return Ok(new UploadResponse(url));
    }

    /// <summary>Removes a site asset (logo | favicon | og-image).</summary>
    [HttpDelete("upload/{type}")]
    [Authorize(Roles = "Owner,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteUpload(string type, CancellationToken ct)
    {
        var key = UploadKeyFor(type);
        if (key is null) return BadRequest();
        await siteConfig.SetAsync(key, null, ct);
        return NoContent();
    }

    private static string? UploadKeyFor(string type) => type switch
    {
        "logo"     => SiteDataKeys.SiteLogoUrl,
        "favicon"  => SiteDataKeys.SiteFaviconUrl,
        "og-image" => SiteDataKeys.SiteOgImageUrl,
        _          => null,
    };
}

public record SiteConfigResponse(
    string? Name, string? Url, string? LogoUrl, string? FaviconUrl,
    string? MetaTitle, string? MetaDescription, string? OgImageUrl);

public record UpdateSiteConfigRequest(
    string? Name,
    [property: Url] string? Url,
    string? MetaTitle,
    string? MetaDescription);

public record IncidentsConfigResponse(
    string CorrelationMode,
    int GlobalThreshold, int GlobalCorrelationWindowMinutes);

public record UpdateIncidentsConfigRequest(
    [property: EnumDataType(typeof(Piro.Application.Interfaces.IncidentCorrelationMode))] string? CorrelationMode,
    [property: Range(1, int.MaxValue)] int? GlobalThreshold,
    [property: Range(1, int.MaxValue)] int? GlobalCorrelationWindowMinutes);

public record UploadResponse(string Url);
