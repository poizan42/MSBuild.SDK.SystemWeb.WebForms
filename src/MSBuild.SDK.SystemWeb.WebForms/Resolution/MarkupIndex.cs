using System;
using MSBuild.SDK.SystemWeb.WebForms.Generator.Parsing;

namespace MSBuild.SDK.SystemWeb.WebForms.Generator.Resolution
{
    /// <summary>The little a document needs to know about the other markup files: where they are and what class they inherit.</summary>
    public sealed record MarkupSummary(string RelativePath, MarkupKind Kind, string? Inherits)
    {
        public static MarkupSummary From(MarkupDocument document)
        {
            var inherits = document.MainDirective?.Get("Inherits");
            return new MarkupSummary(VirtualPathResolver.Normalize(document.RelativePath), document.Kind, string.IsNullOrWhiteSpace(inherits) ? null : inherits!.Trim());
        }
    }

    public sealed record MarkupIndex(EquatableArray<MarkupSummary> Summaries)
    {
        public MarkupSummary? Find(string? normalizedRelativePath)
        {
            if (string.IsNullOrEmpty(normalizedRelativePath))
            {
                return null;
            }

            foreach (var summary in Summaries)
            {
                if (string.Equals(summary.RelativePath, normalizedRelativePath, StringComparison.OrdinalIgnoreCase))
                {
                    return summary;
                }
            }

            return null;
        }
    }
}
