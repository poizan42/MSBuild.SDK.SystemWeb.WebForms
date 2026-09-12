using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace MSBuild.SDK.SystemWeb.WebForms.Tests.Infrastructure;

public sealed class InMemoryAdditionalText : AdditionalText
{
    private readonly SourceText _text;

    public InMemoryAdditionalText(string path, string text)
    {
        Path = path;
        _text = SourceText.From(text, Encoding.UTF8);
    }

    public override string Path { get; }

    public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
}

public sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
{
    private readonly TestAnalyzerConfigOptions _global;

    public TestAnalyzerConfigOptionsProvider(IReadOnlyDictionary<string, string> globalOptions)
    {
        _global = new TestAnalyzerConfigOptions(globalOptions);
    }

    public override AnalyzerConfigOptions GlobalOptions => _global;

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => TestAnalyzerConfigOptions.Empty;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => TestAnalyzerConfigOptions.Empty;

    private sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        public static readonly TestAnalyzerConfigOptions Empty = new(new Dictionary<string, string>());

        private readonly IReadOnlyDictionary<string, string> _values;

        public TestAnalyzerConfigOptions(IReadOnlyDictionary<string, string> values)
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
