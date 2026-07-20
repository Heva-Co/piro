using Piro.Application.DTOs;
using Piro.Application.Interfaces;
using Piro.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Markdown;

namespace Piro.Infrastructure.Pdf;

/// <summary>
/// QuestPDF implementation of <see cref="IPostmortemPdfGenerator"/>. Renders a finalized postmortem as a
/// clean, printable report: header plus metadata, the analysis sections, referenced incidents, and the
/// merged timeline. LongText section bodies are authored as Markdown and rendered via QuestPDF.Markdown.
/// </summary>
public class PostmortemPdfGenerator : IPostmortemPdfGenerator
{
    private const string RepoUrl = "https://github.com/Heva-Co/piro";

    public byte[] Generate(PostmortemDto p, DateTimeOffset generatedAt)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(t => t.FontSize(10).FontColor(Colors.Grey.Darken4));

                page.Header().Element(h => ComposeHeader(h, p));
                page.Content().PaddingVertical(10).Element(c => ComposeContent(c, p));
                page.Footer().Element(f => ComposeFooter(f, generatedAt));
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeFooter(IContainer container, DateTimeOffset generatedAt)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
            col.Item().Row(row =>
            {
                row.RelativeItem().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Medium));
                    t.Span("Generated with ");
                    t.Hyperlink("Piro", RepoUrl).FontColor(Colors.Blue.Medium);
                    t.Span($" · {FormatDateTime(generatedAt)}");
                });
                row.ConstantItem(80).AlignRight().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Medium));
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        });
    }

    private static void ComposeHeader(IContainer container, PostmortemDto p)
    {
        container.Column(col =>
        {
            col.Item().Text("Incident Postmortem").FontSize(9).FontColor(Colors.Grey.Medium).LetterSpacing(0.05f);
            col.Item().Text(p.Name).FontSize(20).SemiBold();

            col.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text(t =>
                {
                    t.Span("Owner: ").SemiBold();
                    t.Span(string.IsNullOrWhiteSpace(p.ReviewOwnerName) ? "Unassigned" : p.ReviewOwnerName);
                });
                row.RelativeItem().AlignRight().Text(t =>
                {
                    t.Span("Status: ").SemiBold();
                    t.Span(p.Status.ToString());
                });
            });

            col.Item().Row(row =>
            {
                row.RelativeItem().Text(t =>
                {
                    if (p.PublishedAt.HasValue)
                    {
                        t.Span("Published: ").SemiBold();
                        t.Span(FormatDateTime(p.PublishedAt.Value));
                    }
                });
                row.RelativeItem().AlignRight().Text(t =>
                {
                    // Postmortems are internal-only in the current scope (public visibility is a later
                    // phase with a separate flag). Stamp it explicitly so a shared PDF is unambiguous.
                    t.Span("Visibility: ").SemiBold();
                    t.Span("Internal");
                });
            });

            if (p.ImpactStartAt.HasValue || p.ImpactEndAt.HasValue)
            {
                col.Item().Text(t =>
                {
                    t.Span("Impact window: ").SemiBold();
                    t.Span($"{FormatDate(p.ImpactStartAt)} – {FormatDate(p.ImpactEndAt)}");
                });
            }

            col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    private static void ComposeContent(IContainer container, PostmortemDto p)
    {
        container.Column(col =>
        {
            col.Spacing(14);

            // Analysis sections
            foreach (var field in p.Fields)
            {
                col.Item().Column(section =>
                {
                    section.Item().Text(field.Heading).FontSize(13).SemiBold();
                    if (string.IsNullOrWhiteSpace(field.Value))
                    {
                        section.Item().PaddingTop(2).Text("—").FontColor(Colors.Grey.Medium);
                    }
                    else if (field.FieldType == PostmortemFieldType.LongText)
                    {
                        // LongText bodies are authored as Markdown in the editor, so render it (bold, italic,
                        // lists...) instead of dumping the raw source with visible ** markers.
                        section.Item().PaddingTop(2).Markdown(field.Value);
                    }
                    else
                    {
                        section.Item().PaddingTop(2).Text(field.Value).LineHeight(1.4f);
                    }
                });
            }

            // Referenced incidents
            if (p.Incidents.Any())
            {
                col.Item().PaddingTop(4).Column(section =>
                {
                    section.Item().Text("Referenced incidents").FontSize(13).SemiBold();
                    foreach (var inc in p.Incidents)
                        section.Item().PaddingTop(2).Text($"#{inc.IncidentId} · {inc.Title} ({inc.Status})");
                });
            }

            // Timeline
            if (p.Timeline.Any())
            {
                col.Item().PaddingTop(4).Column(section =>
                {
                    section.Item().Text("Timeline").FontSize(13).SemiBold();
                    foreach (var item in p.Timeline)
                    {
                        section.Item().PaddingTop(3).Row(row =>
                        {
                            row.ConstantItem(130).Text(FormatDateTime(item.OccurredAt))
                                .FontColor(Colors.Grey.Darken1);
                            row.RelativeItem().Text(t =>
                            {
                                var label = item.IsAnnotation
                                    ? $"Note{(string.IsNullOrWhiteSpace(item.ActorName) ? "" : $" · {item.ActorName}")}"
                                    : $"{item.Source} · #{item.IncidentId} {item.IncidentTitle}";
                                t.Span(label).FontColor(Colors.Grey.Darken1).FontSize(9);
                                if (!string.IsNullOrWhiteSpace(item.Text))
                                    t.Line(item.Text);
                            });
                        });
                    }
                });
            }
        });
    }

    private static string FormatDate(DateTimeOffset? d) =>
        d.HasValue ? d.Value.UtcDateTime.ToString("yyyy-MM-dd") : "—";

    private static string FormatDateTime(DateTimeOffset d) =>
        d.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'");
}
