using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Piro.Application.Config;
using Piro.Application.DTOs;

namespace Piro.Api.Controllers;

/// <summary>
/// Config as code (RFC 0019): plan, apply and export the instance's services and checks as
/// <c>piro.yaml</c> documents.
/// </summary>
/// <remarks>
/// Restricted to Owner and Admin, matching API-key management. That makes these endpoints stricter
/// than the service and check CRUD they wrap, which currently carry no authorization at all — a
/// deliberate asymmetry, since a bulk mutation endpoint should not be the loosest door in the
/// building (§4.7).
/// </remarks>
[Authorize(Roles = "Owner,Admin")]
[ApiController]
[Route("api/v1/config")]
[Produces("application/json")]
public class ConfigController(
    ConfigReconciler reconciler,
    ConfigExporter exporter,
    ConfigSchemaGenerator schemaGenerator) : ControllerBase
{
    /// <summary>
    /// Computes what applying the supplied documents would change, writing nothing. This is the
    /// endpoint CI calls on every pull request.
    /// </summary>
    /// <remarks>
    /// A separate route rather than <c>apply?dryRun=true</c>: the read-only operation sits on the
    /// other side of a review boundary and can be authorized and reasoned about independently, and a
    /// query parameter that turns a read into a write is exactly the kind of switch that gets omitted.
    /// </remarks>
    /// <response code="200">The plan. Check <c>errors</c> — a plan with errors describes nothing.</response>
    [HttpPost("plan")]
    [ProducesResponseType<ConfigPlanDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Plan([FromBody] ConfigApplyRequest request, CancellationToken ct)
    {
        var plan = await reconciler.PlanAsync(request, ct);

        // Validation failures are the document's problem, not the request's, so they come back as a
        // 400 carrying every located error rather than a bare rejection.
        return plan.Errors.Count > 0 ? BadRequest(plan) : Ok(plan);
    }

    /// <summary>Applies the supplied documents in a single transaction.</summary>
    /// <response code="200">The applied plan. <c>schedulingErrors</c> is non-empty if the write
    /// succeeded but a Quartz trigger could not be reconciled.</response>
    [HttpPost("apply")]
    [ProducesResponseType<ConfigPlanDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Apply([FromBody] ConfigApplyRequest request, CancellationToken ct)
    {
        var plan = await reconciler.ApplyAsync(request, ct);
        return plan.Errors.Count > 0 ? BadRequest(plan) : Ok(plan);
    }

    /// <summary>
    /// Serializes the current services and checks as a v1 <c>piro.yaml</c>. Lossy by design: fields
    /// outside the schema and checks bound to an integration are commented, not silently dropped.
    /// </summary>
    /// <summary>
    /// The JSON Schema for <c>piro.yaml</c>, generated from this instance's check registry so it
    /// describes the check types this instance actually has, including any beyond the built-in set.
    /// </summary>
    /// <remarks>
    /// Anonymous: a schema is public documentation of a file format, carries no data about the
    /// instance beyond which check types exist, and an editor fetching it has no credential to send.
    /// </remarks>
    [HttpGet("schema")]
    [AllowAnonymous]
    [Produces("application/schema+json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Schema()
    {
        Response.Headers.CacheControl = "public, max-age=300";
        return Content(schemaGenerator.Generate(), "application/schema+json; charset=utf-8");
    }

    [HttpGet("export")]
    [Produces("text/yaml")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var yaml = await exporter.ExportAsync(ct);
        return File(System.Text.Encoding.UTF8.GetBytes(yaml), "text/yaml; charset=utf-8", "piro.yaml");
    }
}
