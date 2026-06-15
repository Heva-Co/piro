using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.Services;

namespace Piro.Api.Controllers;

/// <summary>Config-as-code import endpoint.</summary>
[Authorize]
[ApiController]
[Route("api/v1/config")]
[Produces("application/json")]
public class ConfigController(YamlImportService yamlImport) : ControllerBase
{
    /// <summary>
    /// Parses a piro.yaml and returns the import plan without applying any changes (dry-run).
    /// Pass <c>apply=true</c> to commit the changes.
    /// </summary>
    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] ImportRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Yaml))
            return BadRequest(new { error = "YAML content is required." });

        var result = request.Apply
            ? await yamlImport.ApplyAsync(request.Yaml, ct)
            : await yamlImport.PlanAsync(request.Yaml, ct);

        return Ok(result);
    }
}

public record ImportRequest(string Yaml, bool Apply = false);
