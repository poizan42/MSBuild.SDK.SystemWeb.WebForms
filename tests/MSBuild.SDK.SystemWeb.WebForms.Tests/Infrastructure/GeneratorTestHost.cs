using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MSBuild.SDK.SystemWeb.WebForms.Generator;

namespace MSBuild.SDK.SystemWeb.WebForms.Tests.Infrastructure;

/// <summary>Builds a fake Web Forms project in memory and runs the generator over it.</summary>
public sealed class GeneratorTestHost
{
    public static readonly string ProjectDir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\fake\WebApp\" : "/fake/WebApp/";

    private readonly List<AdditionalText> _additionalTexts = new();
    private readonly List<SyntaxTree> _syntaxTrees = new();
    private string _rootNamespace = "WebApp";

    public GeneratorTestHost WithRootNamespace(string rootNamespace)
    {
        _rootNamespace = rootNamespace;
        return this;
    }

    public GeneratorTestHost WithMarkup(string relativePath, string markup)
    {
        _additionalTexts.Add(new InMemoryAdditionalText(ToFullPath(relativePath), markup));
        return this;
    }

    public GeneratorTestHost WithMarkupText(AdditionalText text)
    {
        _additionalTexts.Add(text);
        return this;
    }

    public GeneratorTestHost WithWebConfig(string xml) => WithMarkup("Web.config", xml);

    public GeneratorTestHost WithSource(string relativePath, string code)
    {
        _syntaxTrees.Add(CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Latest), path: ToFullPath(relativePath)));
        return this;
    }

    public GeneratorTestHost WithSyntaxTree(SyntaxTree tree)
    {
        _syntaxTrees.Add(tree);
        return this;
    }

    public GeneratorTestResult Run(bool referenceSystemWeb = true)
    {
        var references = referenceSystemWeb
            ? NetFx48ReferenceAssemblies.References
            : NetFx48ReferenceAssemblies.References.Where(r => !(r.Display ?? string.Empty).Contains("System.Web", StringComparison.OrdinalIgnoreCase)).ToImmutableArray();

        var compilation = CSharpCompilation.Create(
            "WebApp",
            _syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var optionsProvider = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
        {
            ["build_property.MSBuildProjectDirectory"] = ProjectDir.TrimEnd('\\', '/'),
            ["build_property.RootNamespace"] = _rootNamespace,
        });

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new WebFormsDesignerGenerator().AsSourceGenerator() },
            _additionalTexts,
            new CSharpParseOptions(LanguageVersion.Latest),
            optionsProvider,
            new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

        return GeneratorTestResult.Run(driver, compilation);
    }

    public static string ToFullPath(string relativePath) => ProjectDir + relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

    public static AdditionalText CreateMarkup(string relativePath, string markup) => new InMemoryAdditionalText(ToFullPath(relativePath), markup);
}

public sealed class GeneratorTestResult
{
    private GeneratorTestResult(GeneratorDriver driver, Compilation inputCompilation, Compilation outputCompilation, ImmutableArray<Diagnostic> generatorDiagnostics)
    {
        Driver = driver;
        InputCompilation = inputCompilation;
        OutputCompilation = outputCompilation;
        GeneratorDiagnostics = generatorDiagnostics;
        RunResult = driver.GetRunResult().Results.Single();
    }

    public GeneratorDriver Driver { get; }

    public Compilation InputCompilation { get; }

    public Compilation OutputCompilation { get; }

    public ImmutableArray<Diagnostic> GeneratorDiagnostics { get; }

    public GeneratorRunResult RunResult { get; }

    public IEnumerable<string> HintNames => RunResult.GeneratedSources.Select(s => s.HintName);

    public ImmutableArray<Diagnostic> CompileErrors => OutputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray();

    public static GeneratorTestResult Run(GeneratorDriver driver, Compilation compilation)
    {
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        return new GeneratorTestResult(driver, compilation, output, diagnostics);
    }

    public bool HasSource(string hintName) => RunResult.GeneratedSources.Any(s => s.HintName == hintName);

    public string GetSource(string hintName)
    {
        foreach (var source in RunResult.GeneratedSources)
        {
            if (source.HintName == hintName)
            {
                return source.SourceText.ToString();
            }
        }

        throw new Xunit.Sdk.XunitException($"No generated source named '{hintName}'. Generated: [{string.Join(", ", HintNames)}]. Diagnostics: [{string.Join("; ", GeneratorDiagnostics)}]");
    }

    /// <summary>Runs the same driver again over a (possibly changed) compilation, optionally swapping one additional text.</summary>
    public GeneratorTestResult RunAgain(Compilation? compilation = null, AdditionalText? replace = null, AdditionalText? with = null)
    {
        var driver = Driver;
        if (replace is not null && with is not null)
        {
            driver = driver.ReplaceAdditionalText(replace, with);
        }

        return Run(driver, compilation ?? InputCompilation);
    }
}

public static class DesignerAssert
{
    public static void HasField(string source, string fullyQualifiedType, string name)
    {
        var declaration = $"protected {fullyQualifiedType} {name};";
        Assert.True(source.Contains(declaration, StringComparison.Ordinal), $"Expected '{declaration}' in:\n{source}");
    }

    public static void HasNoField(string source, string name)
    {
        Assert.False(source.Contains($" {name};", StringComparison.Ordinal), $"Did not expect a field named '{name}' in:\n{source}");
    }

    public static void Compiles(GeneratorTestResult result)
    {
        var errors = result.CompileErrors;
        Assert.True(errors.IsEmpty, "Generated code does not compile:\n" + string.Join("\n", errors) + "\n\nGenerated sources:\n" + string.Join("\n---\n", result.RunResult.GeneratedSources.Select(s => s.HintName + "\n" + s.SourceText)));
    }

    public static Diagnostic HasDiagnostic(GeneratorTestResult result, string id)
    {
        var diagnostic = result.GeneratorDiagnostics.FirstOrDefault(d => d.Id == id);
        Assert.True(diagnostic is not null, $"Expected diagnostic {id}; got [{string.Join("; ", result.GeneratorDiagnostics)}]");
        return diagnostic!;
    }

    public static void NoDiagnostics(GeneratorTestResult result)
    {
        Assert.True(result.GeneratorDiagnostics.IsEmpty, "Unexpected generator diagnostics: " + string.Join("; ", result.GeneratorDiagnostics));
    }
}
