using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using MSBuild.SDK.SystemWeb.WebForms.Generator.Emit;
using MSBuild.SDK.SystemWeb.WebForms.Generator.Model;
using MSBuild.SDK.SystemWeb.WebForms.Generator.Parsing;
using MSBuild.SDK.SystemWeb.WebForms.Generator.Resolution;

namespace MSBuild.SDK.SystemWeb.WebForms.Generator
{
    /// <summary>A diagnostic recorded during the walk, without a <see cref="Location"/> so it can be cached and re-reported.</summary>
    public sealed record DiagnosticInfo(DiagnosticDescriptor Descriptor, int Position, DiagnosticSeverity? SeverityOverride, EquatableArray<string> Arguments);

    /// <summary>What the generator emits for one markup document.</summary>
    public sealed class DocumentOutput
    {
        public DocumentOutput(string hintName, SourceText? source, ImmutableArray<DiagnosticInfo> diagnostics)
        {
            HintName = hintName;
            Source = source;
            Diagnostics = diagnostics;
        }

        public string HintName { get; }

        public SourceText? Source { get; }

        public ImmutableArray<DiagnosticInfo> Diagnostics { get; }
    }

    /// <summary>
    /// Remembers the output for each markup document together with what it depended on in the compilation:
    /// the metadata references and the syntax trees that declare every symbol the walk consulted.
    /// The incremental pipeline re-runs the compilation-dependent step for every document on every edit; this cache
    /// lets documents whose dependencies are untouched (which is almost all of them) skip the symbol walk entirely.
    /// Roslyn reuses <see cref="SyntaxTree"/> and <see cref="MetadataReference"/> instances across incremental compilations
    /// for files that did not change, so identity checks are enough to validate an entry.
    /// </summary>
    public static class DocumentCache
    {
        private static readonly ConcurrentDictionary<(string Assembly, string Path), Entry> Entries = new();

        public static DocumentOutput GetOrCompute(
            MarkupDocument document,
            MarkupIndex index,
            WebConfigRegistrations webConfig,
            GeneratorOptions options,
            Compilation compilation)
        {
            var key = (compilation.AssemblyName ?? string.Empty, document.FilePath);
            var references = compilation.References is ImmutableArray<MetadataReference> array ? array : ImmutableArray.CreateRange(compilation.References);

            if (Entries.TryGetValue(key, out var entry) && entry.IsValidFor(document, index, webConfig, options, compilation, references))
            {
                return entry.Output;
            }

            var result = DesignerModelBuilder.Build(document, index, webConfig, options, compilation);
            SourceText? source = null;
            if (result.Model is not null)
            {
                source = SourceText.From(CSharpDesignerEmitter.Emit(result.Model, document.RelativePath), Encoding.UTF8);
            }

            var output = new DocumentOutput(HintNames.ForMarkup(document.RelativePath), source, result.Diagnostics);
            if (result.IsCacheable)
            {
                Entries[key] = new Entry(document, index, webConfig, options, references, result.DependentTrees, output);
            }
            else
            {
                Entries.TryRemove(key, out _);
            }

            return output;
        }

        /// <summary>For tests and diagnostics: number of documents currently cached.</summary>
        public static int Count => Entries.Count;

        public static void Clear() => Entries.Clear();

        private sealed class Entry
        {
            private readonly MarkupDocument _document;
            private readonly MarkupIndex _index;
            private readonly WebConfigRegistrations _webConfig;
            private readonly GeneratorOptions _options;
            private readonly ImmutableArray<MetadataReference> _references;
            private readonly ImmutableArray<SyntaxTree> _trees;

            public Entry(
                MarkupDocument document,
                MarkupIndex index,
                WebConfigRegistrations webConfig,
                GeneratorOptions options,
                ImmutableArray<MetadataReference> references,
                ImmutableArray<SyntaxTree> trees,
                DocumentOutput output)
            {
                _document = document;
                _index = index;
                _webConfig = webConfig;
                _options = options;
                _references = references;
                _trees = trees;
                Output = output;
            }

            public DocumentOutput Output { get; }

            public bool IsValidFor(
                MarkupDocument document,
                MarkupIndex index,
                WebConfigRegistrations webConfig,
                GeneratorOptions options,
                Compilation compilation,
                ImmutableArray<MetadataReference> references)
            {
                if (!Same(_document, document) || !Same(_index, index) || !Same(_webConfig, webConfig) || !Same(_options, options))
                {
                    return false;
                }

                if (_references.Length != references.Length)
                {
                    return false;
                }

                for (var i = 0; i < references.Length; i++)
                {
                    if (!ReferenceEquals(_references[i], references[i]))
                    {
                        return false;
                    }
                }

                foreach (var tree in _trees)
                {
                    if (!compilation.ContainsSyntaxTree(tree))
                    {
                        return false;
                    }
                }

                return true;
            }

            private static bool Same<T>(T cached, T current) where T : class
            {
                return ReferenceEquals(cached, current) || cached.Equals(current);
            }
        }
    }
}
