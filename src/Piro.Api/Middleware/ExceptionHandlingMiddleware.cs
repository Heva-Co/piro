using System.Text.Json;
using Piro.Domain.Exceptions;

namespace Piro.Api.Middleware;

/// <summary>Translates domain exceptions into consistent HTTP problem responses.</summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (NotFoundException ex)
        {
            await WriteProblem(context, StatusCodes.Status404NotFound, "Not Found", ex.Message);
        }
        catch (CyclicDependencyException ex)
        {
            await WriteProblem(context, StatusCodes.Status422UnprocessableEntity, "Cyclic Dependency", ex.Message);
        }
        catch (DomainValidationException ex)
        {
            await WriteProblem(context, StatusCodes.Status400BadRequest, "Validation Error", ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await WriteProblem(context, StatusCodes.Status500InternalServerError, "Internal Server Error", "An unexpected error occurred.");
        }
    }

    private static async Task WriteProblem(HttpContext context, int statusCode, string title, string detail)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        var body = JsonSerializer.Serialize(new { title, detail, status = statusCode });
        await context.Response.WriteAsync(body);
    }
}
