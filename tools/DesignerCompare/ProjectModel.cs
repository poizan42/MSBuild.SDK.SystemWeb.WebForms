using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace DesignerCompare;

/// <summary>Everything discovered about the project under test.</summary>
public sealed class ProjectModel
{
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "node_modules", "packages", ".git", ".vs", "TestResults",
    };

    private ProjectModel(string directory)
    {
        Directory = directory;
    }

    public string Directory { get; }

    public string Name => Path.GetFileName(Directory.TrimEnd('\\', '/'));

    public string RootNamespace { get; private set; } = string.Empty;

    public string? AssemblyName { get; private set; }

    public List<string> MarkupFiles { get; } = new();

    public List<string> WebConfigFiles { get; } = new();

    public List<string> SourceFiles { get; } = new();

    public List<string> DesignerFiles { get; } = new();

    public List<string> BinaryReferences { get; } = new();

    public List<string> Notes { get; } = new();

    public static ProjectModel Discover(string directory, string? rootNamespaceOverride, IReadOnlyCollection<string> excludedReferenceNames)
    {
        var model = new ProjectModel(Path.GetFullPath(directory).TrimEnd('\\', '/') + Path.DirectorySeparatorChar);

        foreach (var file in EnumerateFiles(model.Directory))
        {
            var name = Path.GetFileName(file);
            var extension = Path.GetExtension(file);
            if (extension.Equals(".aspx", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".ascx", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".master", StringComparison.OrdinalIgnoreCase))
            {
                model.MarkupFiles.Add(file);
            }
            else if (name.Equals("web.config", StringComparison.OrdinalIgnoreCase))
            {
                model.WebConfigFiles.Add(file);
            }
            else if (extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                if (name.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase))
                {
                    model.DesignerFiles.Add(file);
                }
                else
                {
                    model.SourceFiles.Add(file);
                }
            }
        }

        model.ReadProjectFile();
        model.RootNamespace = rootNamespaceOverride ?? model.RootNamespace;
        if (string.IsNullOrEmpty(model.RootNamespace))
        {
            model.RootNamespace = model.Name.Replace(' ', '_');
        }

        model.CollectBinaryReferences(excludedReferenceNames);
        return model;
    }

    public string GetRelativePath(string file) => file.Substring(Directory.Length).Replace('\\', '/');

    /// <summary>The legacy designer file that Visual Studio would have maintained for a markup file, if it exists.</summary>
    public string? GetLegacyDesignerFile(string markupFile)
    {
        var candidate = markupFile + ".designer.cs";
        return File.Exists(candidate) ? candidate : null;
    }

    public CSharpCompilation CreateCompilation(IEnumerable<MetadataReference> frameworkReferences)
    {
        var trees = new List<SyntaxTree>();
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        foreach (var source in SourceFiles)
        {
            trees.Add(CSharpSyntaxTree.ParseText(SourceText.From(File.ReadAllText(source), Encoding.UTF8), parseOptions, path: source));
        }

        var references = new List<MetadataReference>(frameworkReferences);
        var known = new HashSet<string>(references.Select(r => Path.GetFileNameWithoutExtension(r.Display ?? string.Empty)), StringComparer.OrdinalIgnoreCase);
        foreach (var binary in BinaryReferences)
        {
            if (known.Add(Path.GetFileNameWithoutExtension(binary)))
            {
                try
                {
                    references.Add(MetadataReference.CreateFromFile(binary));
                }
                catch (Exception ex)
                {
                    Notes.Add($"Skipped reference '{GetRelativePath(binary)}': {ex.Message}");
                }
            }
        }

        return CSharpCompilation.Create(
            AssemblyName ?? RootNamespace,
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    public IReadOnlyList<AdditionalText> CreateAdditionalTexts()
    {
        var texts = new List<AdditionalText>();
        foreach (var file in MarkupFiles.Concat(WebConfigFiles))
        {
            texts.Add(new FileAdditionalText(file));
        }

        return texts;
    }

    public AnalyzerConfigOptionsProvider CreateOptionsProvider()
    {
        return new DictionaryOptionsProvider(new Dictionary<string, string>
        {
            ["build_property.MSBuildProjectDirectory"] = Directory.TrimEnd('\\', '/'),
            ["build_property.RootNamespace"] = RootNamespace,
        });
    }

    private static IEnumerable<string> EnumerateFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            IEnumerable<string> entries;
            try
            {
                entries = System.IO.Directory.EnumerateFileSystemEntries(current);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var entry in entries)
            {
                if (System.IO.Directory.Exists(entry))
                {
                    var name = Path.GetFileName(entry);
                    if (!ExcludedDirectories.Contains(name) && !name.StartsWith("_ReSharper", StringComparison.OrdinalIgnoreCase))
                    {
                        pending.Push(entry);
                    }
                }
                else
                {
                    yield return entry;
                }
            }
        }
    }

    private void ReadProjectFile()
    {
        var projectFile = System.IO.Directory.EnumerateFiles(Directory, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (projectFile is null)
        {
            Notes.Add("No .csproj found: treated as a Web Site project (CodeFile model, App_Code compiled as source).");
            return;
        }

        try
        {
            var document = XDocument.Load(projectFile);
            var properties = document.Descendants().Where(e => e.Name.LocalName is "RootNamespace" or "AssemblyName").ToList();
            RootNamespace = properties.FirstOrDefault(e => e.Name.LocalName == "RootNamespace")?.Value.Trim() ?? string.Empty;
            AssemblyName = properties.FirstOrDefault(e => e.Name.LocalName == "AssemblyName")?.Value.Trim();
            if (string.IsNullOrEmpty(RootNamespace) && AssemblyName is not null)
            {
                RootNamespace = AssemblyName;
            }

            if (string.IsNullOrEmpty(RootNamespace))
            {
                RootNamespace = Path.GetFileNameWithoutExtension(projectFile);
            }

            AssemblyName ??= Path.GetFileNameWithoutExtension(projectFile);
            Notes.Add($"Project file: {Path.GetFileName(projectFile)} (RootNamespace={RootNamespace}, AssemblyName={AssemblyName})");
        }
        catch (Exception ex)
        {
            Notes.Add($"Could not read {Path.GetFileName(projectFile)}: {ex.Message}");
        }
    }

    private void CollectBinaryReferences(IReadOnlyCollection<string> excludedReferenceNames)
    {
        var bin = Path.Combine(Directory, "bin");
        if (!System.IO.Directory.Exists(bin))
        {
            Notes.Add("No bin folder: only framework assemblies and project sources are available for type resolution.");
            return;
        }

        foreach (var dll in System.IO.Directory.EnumerateFiles(bin, "*.dll", SearchOption.AllDirectories))
        {
            var name = Path.GetFileNameWithoutExtension(dll);
            if (name.StartsWith("App_", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase)
                || (AssemblyName is not null && name.Equals(AssemblyName, StringComparison.OrdinalIgnoreCase))
                || excludedReferenceNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            BinaryReferences.Add(dll);
        }
    }

    private sealed class FileAdditionalText : AdditionalText
    {
        private readonly Lazy<SourceText> _text;

        public FileAdditionalText(string path)
        {
            Path = path;
            _text = new Lazy<SourceText>(() => SourceText.From(File.ReadAllText(path), Encoding.UTF8));
        }

        public override string Path { get; }

        public override SourceText GetText(CancellationToken cancellationToken = default) => _text.Value;
    }

    private sealed class DictionaryOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly DictionaryOptions _global;

        public DictionaryOptionsProvider(Dictionary<string, string> values)
        {
            _global = new DictionaryOptions(values);
        }

        public override AnalyzerConfigOptions GlobalOptions => _global;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => DictionaryOptions.Empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => DictionaryOptions.Empty;

        private sealed class DictionaryOptions : AnalyzerConfigOptions
        {
            public static readonly DictionaryOptions Empty = new(new Dictionary<string, string>());

            private readonly Dictionary<string, string> _values;

            public DictionaryOptions(Dictionary<string, string> values)
            {
                _values = values;
            }

            public override bool TryGetValue(string key, out string value)
            {
                if (_values.TryGetValue(key, out var found))
                {
                    value = found;
                    return true;
                }

                value = string.Empty;
                return false;
            }
        }
    }
}
