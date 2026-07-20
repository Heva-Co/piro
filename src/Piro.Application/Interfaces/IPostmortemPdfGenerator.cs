using Piro.Application.DTOs;

namespace Piro.Application.Interfaces;

/// <summary>Renders a finalized postmortem report to a PDF document (RFC 0005 follow-up).</summary>
public interface IPostmortemPdfGenerator
{
    /// <summary>Produces the PDF bytes for the given postmortem, stamping <paramref name="generatedAt"/> in the footer.</summary>
    byte[] Generate(PostmortemDto postmortem, DateTimeOffset generatedAt);
}
