using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.VisualBasic;
using MSBuild.SDK.SystemWeb.WebForms.Generator;

namespace DesignerCompare;

/// <summary>
/// Runs the designer generator over an existing Web Forms project (Web Application Project or Web Site) and
/// compares what it generates with the *.designer.cs files Visual Studio maintained, field by field.
///
///   DesignerCompare &lt;projectDir&gt; [--out report.md] [--root-namespace Ns] [--exclude-ref Name]* [--refs dir] [--show-all]
///
/// The project's own *.designer.cs files are excluded from the compilation so the generator produces the complete
/// designer for every page; their content is the expected result. Code-behind, App_Code and every DLL under bin\
/// (except the project's own output) take part in type resolution.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            Console.WriteLine("Usage: DesignerCompare <projectDir> [--out report.md] [--language cs|vb] [--root-namespace Ns] [--exclude-ref Name]* [--exclude-dir Name]* [--refs <.NET Framework reference assemblies dir>] [--show-all]");
            return 2;
        }

        var projectDir = args[0];
        string? outFile = null;
        string? rootNamespace = null;
        string? refsDir = null;
        string? language = null;
        var showAll = false;
        var excludedRefs = new List<string>();
        var excludedDirs = new List<string>();
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out": outFile = args[++i]; break;
                case "--root-namespace": rootNamespace = args[++i]; break;
                case "--exclude-ref": excludedRefs.Add(args[++i]); break;
                case "--exclude-dir": excludedDirs.Add(args[++i]); break;
                case "--refs": refsDir = args[++i]; break;
                case "--show-all": showAll = true; break;
                case "--language":
                    var value = args[++i];
                    language = value.Equals("vb", StringComparison.OrdinalIgnoreCase) ? LanguageNames.VisualBasic
                        : value.Equals("cs", StringComparison.OrdinalIgnoreCase) ? LanguageNames.CSharp
                        : throw new ArgumentException($"Unknown language '{value}' (use cs or vb).");
                    break;
                default:
                    Console.Error.WriteLine($"Unknown argument '{args[i]}'.");
                    return 2;
            }
        }

        if (!Directory.Exists(projectDir))
        {
            Console.Error.WriteLine($"Directory not found: {projectDir}");
            return 2;
        }

        var stopwatch = Stopwatch.StartNew();
        var project = ProjectModel.Discover(projectDir, rootNamespace, excludedRefs, language, excludedDirs);
        var frameworkReferences = FrameworkReferences.Load(refsDir);
        var compilation = project.CreateCompilation(frameworkReferences);
        var discoveryMs = stopwatch.ElapsedMilliseconds;

        stopwatch.Restart();
        var generators = ImmutableArray.Create(new WebFormsDesignerGenerator().AsSourceGenerator());
        var additionalTexts = project.CreateAdditionalTexts().ToImmutableArray();
        GeneratorDriver driver = project.Language == LanguageNames.VisualBasic
            ? VisualBasicGeneratorDriver.Create(generators, additionalTexts, new VisualBasicParseOptions(Microsoft.CodeAnalysis.VisualBasic.LanguageVersion.Latest), project.CreateOptionsProvider())
            : CSharpGeneratorDriver.Create(generators, additionalTexts, new CSharpParseOptions(Microsoft.CodeAnalysis.CSharp.LanguageVersion.Latest), project.CreateOptionsProvider());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);
        var runResult = driver.GetRunResult().Results.Single();
        var generateMs = stopwatch.ElapsedMilliseconds;

        var generatedByHint = runResult.GeneratedSources.ToDictionary(s => s.HintName, s => s.SourceText.ToString(), StringComparer.Ordinal);
        var generatedTrees = new HashSet<SyntaxTree>(runResult.GeneratedSources.Select(s => s.SyntaxTree));
        var generatedErrors = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error && d.Location.SourceTree is not null && generatedTrees.Contains(d.Location.SourceTree))
            .ToList();

        var report = new Report(project, showAll);
        foreach (var markup in project.MarkupFiles.OrderBy(m => m, StringComparer.OrdinalIgnoreCase))
        {
            var relative = project.GetRelativePath(markup);
            var hint = HintNames.ForMarkup(relative, project.Language);
            generatedByHint.TryGetValue(hint, out var generatedSource);
            var legacyFile = project.GetLegacyDesignerFile(markup);
            var legacySource = legacyFile is null ? null : File.ReadAllText(legacyFile);
            var diagnostics = generatorDiagnostics.Where(d => string.Equals(d.Location.GetLineSpan().Path, markup, StringComparison.OrdinalIgnoreCase)).ToList();
            report.AddPage(relative, generatedSource, legacySource, diagnostics);
        }

        report.AddGeneratedErrors(generatedErrors);
        report.AddUnattributedDiagnostics(generatorDiagnostics.Where(d => !project.MarkupFiles.Contains(d.Location.GetLineSpan().Path, StringComparer.OrdinalIgnoreCase)).ToList());
        report.Timing(discoveryMs, generateMs);

        var markdown = report.ToMarkdown();
        if (outFile is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))!);
            File.WriteAllText(outFile, markdown, new UTF8Encoding(false));
        }

        Console.WriteLine(report.Summary());
        if (outFile is not null)
        {
            Console.WriteLine($"Report: {Path.GetFullPath(outFile)}");
        }

        return report.PagesWithDifferences == 0 ? 0 : 1;
    }
}

public static class FrameworkReferences
{
    public static IReadOnlyList<MetadataReference> Load(string? directory)
    {
        directory ??= Environment.GetEnvironmentVariable("NETFX48_REFERENCE_ASSEMBLIES");
        if (string.IsNullOrEmpty(directory))
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            directory = Path.Combine(programFiles, "Reference Assemblies", "Microsoft", "Framework", ".NETFramework", "v4.8");
        }

        if (!File.Exists(Path.Combine(directory, "System.Web.dll")))
        {
            throw new InvalidOperationException($".NET Framework reference assemblies not found in '{directory}' (use --refs or NETFX48_REFERENCE_ASSEMBLIES).");
        }

        var references = new List<MetadataReference>();
        foreach (var dll in Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly))
        {
            references.Add(MetadataReference.CreateFromFile(dll));
        }

        var facades = Path.Combine(directory, "Facades");
        if (Directory.Exists(facades))
        {
            foreach (var dll in Directory.EnumerateFiles(facades, "*.dll", SearchOption.TopDirectoryOnly))
            {
                references.Add(MetadataReference.CreateFromFile(dll));
            }
        }

        return references;
    }
}
