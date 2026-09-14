using System.Text;
using Microsoft.CodeAnalysis;

namespace DesignerCompare;

public sealed class Report
{
    private readonly ProjectModel _project;
    private readonly bool _showAll;
    private readonly List<PageResult> _pages = new();
    private readonly List<Diagnostic> _generatedErrors = new();
    private readonly List<Diagnostic> _unattributed = new();
    private long _discoveryMs;
    private long _generateMs;

    public Report(ProjectModel project, bool showAll)
    {
        _project = project;
        _showAll = showAll;
    }

    public int PagesWithDifferences => _pages.Count(p => p.HasDifferences);

    public void AddPage(string relativePath, string? generatedSource, string? legacySource, List<Diagnostic> diagnostics)
    {
        var page = new PageResult(relativePath, diagnostics);
        if (generatedSource is not null)
        {
            page.Generated = DesignerMembers.Parse(generatedSource, _project.Language);
        }

        if (legacySource is not null)
        {
            page.Legacy = DesignerMembers.Parse(legacySource, _project.Language);
        }

        if (page.Generated is not null && page.Legacy is not null)
        {
            Compare(page.Legacy.Fields, page.Generated.Fields, page.MissingFields, page.ExtraFields, page.TypeMismatches);
            Compare(page.Legacy.Properties, page.Generated.Properties, page.MissingProperties, page.ExtraProperties, page.TypeMismatches);
        }

        _pages.Add(page);
    }

    public void AddGeneratedErrors(List<Diagnostic> errors) => _generatedErrors.AddRange(errors);

    public void AddUnattributedDiagnostics(List<Diagnostic> diagnostics) => _unattributed.AddRange(diagnostics);

    public void Timing(long discoveryMs, long generateMs)
    {
        _discoveryMs = discoveryMs;
        _generateMs = generateMs;
    }

    public string Summary()
    {
        var compared = _pages.Count(p => p.Generated is not null && p.Legacy is not null);
        var identical = _pages.Count(p => p.Generated is not null && p.Legacy is not null && !p.HasDifferences);
        var noLegacy = _pages.Count(p => p.Legacy is null);
        var noOutput = _pages.Count(p => p.Generated is null);
        var diagnostics = _pages.Sum(p => p.Diagnostics.Count) + _unattributed.Count;
        return $"{_project.Name}: {_pages.Count} markup files, {compared} compared, {identical} identical, {compared - identical} with differences, "
             + $"{noLegacy} without legacy designer, {noOutput} without generated output, {diagnostics} diagnostics, {_generatedErrors.Count} compile errors in generated code "
             + $"(discovery {_discoveryMs} ms, generation {_generateMs} ms)";
    }

    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Designer comparison: {_project.Name}");
        sb.AppendLine();
        sb.AppendLine($"Project directory: `{_project.Directory}`  ");
        sb.AppendLine($"Root namespace: `{_project.RootNamespace}`  ");
        sb.AppendLine($"Sources compiled: {_project.SourceFiles.Count} (designer files excluded: {_project.DesignerFiles.Count}), binary references: {_project.BinaryReferences.Count}, Web.config files: {_project.WebConfigFiles.Count}  ");
        sb.AppendLine();
        foreach (var note in _project.Notes)
        {
            sb.AppendLine($"- {note}");
        }

        sb.AppendLine();
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine(Summary());
        sb.AppendLine();

        var byId = _pages.SelectMany(p => p.Diagnostics).Concat(_unattributed).GroupBy(d => d.Id).OrderBy(g => g.Key).ToList();
        if (byId.Count > 0)
        {
            sb.AppendLine("| Diagnostic | Count |");
            sb.AppendLine("| ---------- | ----- |");
            foreach (var group in byId)
            {
                sb.AppendLine($"| `{group.Key}` | {group.Count()} |");
            }

            sb.AppendLine();
        }

        var differing = _pages.Where(p => p.HasDifferences).ToList();
        if (differing.Count > 0)
        {
            sb.AppendLine("## Pages with differences");
            sb.AppendLine();
            sb.AppendLine("Missing = declared in the legacy designer but not generated. Extra = generated but not in the legacy designer.");
            sb.AppendLine();
            foreach (var page in differing)
            {
                AppendPage(sb, page);
            }
        }

        var noOutput = _pages.Where(p => p.Generated is null).ToList();
        if (noOutput.Count > 0)
        {
            sb.AppendLine("## Markup without generated output");
            sb.AppendLine();
            foreach (var page in noOutput)
            {
                sb.AppendLine($"- `{page.RelativePath}`" + (page.Legacy is not null ? $" (legacy designer has {page.Legacy.Fields.Count} fields)" : string.Empty));
                foreach (var diagnostic in page.Diagnostics)
                {
                    sb.AppendLine($"  - `{diagnostic.Id}` {diagnostic.GetMessage()}");
                }
            }

            sb.AppendLine();
        }

        var diagnosticPages = _pages.Where(p => !p.HasDifferences && p.Generated is not null && p.Diagnostics.Count > 0).ToList();
        if (diagnosticPages.Count > 0)
        {
            sb.AppendLine("## Diagnostics on otherwise matching pages");
            sb.AppendLine();
            foreach (var page in diagnosticPages)
            {
                sb.AppendLine($"- `{page.RelativePath}`");
                foreach (var diagnostic in page.Diagnostics)
                {
                    sb.AppendLine($"  - `{diagnostic.Id}` {diagnostic.GetMessage()}");
                }
            }

            sb.AppendLine();
        }

        if (_unattributed.Count > 0)
        {
            sb.AppendLine("## Other diagnostics");
            sb.AppendLine();
            foreach (var diagnostic in _unattributed)
            {
                sb.AppendLine($"- `{diagnostic.Id}` {diagnostic.Location.GetLineSpan().Path}: {diagnostic.GetMessage()}");
            }

            sb.AppendLine();
        }

        if (_generatedErrors.Count > 0)
        {
            sb.AppendLine("## Compile errors in generated code");
            sb.AppendLine();
            foreach (var error in _generatedErrors.Take(50))
            {
                sb.AppendLine($"- `{error.Id}` {error.Location.GetLineSpan().Path}({error.Location.GetLineSpan().StartLinePosition.Line + 1}): {error.GetMessage()}");
            }

            if (_generatedErrors.Count > 50)
            {
                sb.AppendLine($"- ... {_generatedErrors.Count - 50} more");
            }

            sb.AppendLine();
        }

        if (_showAll)
        {
            sb.AppendLine("## All pages");
            sb.AppendLine();
            foreach (var page in _pages)
            {
                var status = page.Generated is null ? "no output" : page.Legacy is null ? "no legacy designer" : page.HasDifferences ? "DIFFERENT" : "identical";
                sb.AppendLine($"- `{page.RelativePath}`: {status}, {page.Generated?.Fields.Count ?? 0} generated fields, {page.Legacy?.Fields.Count ?? 0} legacy fields");
            }
        }

        return sb.ToString();
    }

    private static void Compare(
        SortedDictionary<string, string> legacy,
        SortedDictionary<string, string> generated,
        List<KeyValuePair<string, string>> missing,
        List<KeyValuePair<string, string>> extra,
        List<(string Name, string LegacyType, string GeneratedType)> mismatches)
    {
        foreach (var pair in legacy)
        {
            if (!generated.TryGetValue(pair.Key, out var generatedType))
            {
                missing.Add(pair);
            }
            else if (!string.Equals(pair.Value, generatedType, StringComparison.Ordinal))
            {
                mismatches.Add((pair.Key, pair.Value, generatedType));
            }
        }

        foreach (var pair in generated)
        {
            if (!legacy.ContainsKey(pair.Key))
            {
                extra.Add(pair);
            }
        }
    }

    private static void AppendPage(StringBuilder sb, PageResult page)
    {
        sb.AppendLine($"### `{page.RelativePath}`");
        sb.AppendLine();
        if (page.Legacy?.ClassName != page.Generated?.ClassName || page.Legacy?.Namespace != page.Generated?.Namespace)
        {
            sb.AppendLine($"- Class: legacy `{page.Legacy?.Namespace}.{page.Legacy?.ClassName}`, generated `{page.Generated?.Namespace}.{page.Generated?.ClassName}`");
        }

        foreach (var field in page.MissingFields)
        {
            sb.AppendLine($"- Missing field `{field.Value} {field.Key}`");
        }

        foreach (var field in page.ExtraFields)
        {
            sb.AppendLine($"- Extra field `{field.Value} {field.Key}`");
        }

        foreach (var (name, legacyType, generatedType) in page.TypeMismatches)
        {
            sb.AppendLine($"- Type mismatch `{name}`: legacy `{legacyType}`, generated `{generatedType}`");
        }

        foreach (var property in page.MissingProperties)
        {
            sb.AppendLine($"- Missing property `{property.Value} {property.Key}`");
        }

        foreach (var property in page.ExtraProperties)
        {
            sb.AppendLine($"- Extra property `{property.Value} {property.Key}`");
        }

        foreach (var diagnostic in page.Diagnostics)
        {
            sb.AppendLine($"- `{diagnostic.Id}` {diagnostic.GetMessage()}");
        }

        sb.AppendLine();
    }

    private sealed class PageResult
    {
        public PageResult(string relativePath, List<Diagnostic> diagnostics)
        {
            RelativePath = relativePath;
            Diagnostics = diagnostics;
        }

        public string RelativePath { get; }

        public List<Diagnostic> Diagnostics { get; }

        public DesignerMembers? Generated { get; set; }

        public DesignerMembers? Legacy { get; set; }

        public List<KeyValuePair<string, string>> MissingFields { get; } = new();

        public List<KeyValuePair<string, string>> ExtraFields { get; } = new();

        public List<KeyValuePair<string, string>> MissingProperties { get; } = new();

        public List<KeyValuePair<string, string>> ExtraProperties { get; } = new();

        public List<(string Name, string LegacyType, string GeneratedType)> TypeMismatches { get; } = new();

        public bool HasDifferences => MissingFields.Count > 0 || ExtraFields.Count > 0 || TypeMismatches.Count > 0 || MissingProperties.Count > 0 || ExtraProperties.Count > 0
            || (Generated is not null && Legacy is not null && (Generated.ClassName != Legacy.ClassName || Generated.Namespace != Legacy.Namespace));
    }
}
