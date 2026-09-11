using System;
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
    /// <summary>
    /// Generates the ASP.NET Web Forms designer code (<c>*.designer.cs</c> equivalent) for every
    /// <c>.aspx</c>, <c>.ascx</c> and <c>.master</c> file passed to the compiler as an AdditionalFile.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class WebFormsDesignerGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var options = context.AnalyzerConfigOptionsProvider
                .Select(static (provider, _) => GeneratorOptions.From(provider))
                .WithTrackingName("Options");

            // Parsed once per file; re-parsed only when that file (or the project directory) changes.
            var markup = context.AdditionalTextsProvider
                .Where(static text => MarkupDocument.IsMarkupPath(text.Path))
                .Combine(options)
                .Select(static (pair, cancellationToken) =>
                {
                    var (text, generatorOptions) = pair;
                    var source = text.GetText(cancellationToken)?.ToString() ?? string.Empty;
                    return MarkupParser.Parse(text.Path, generatorOptions.GetRelativePath(text.Path), source, MarkupDocument.KindFromExtension(text.Path));
                })
                .WithTrackingName("Markup");

            // What every document needs to know about the others (master page / user control class names).
            var index = markup
                .Select(static (document, _) => MarkupSummary.From(document))
                .Collect()
                .Select(static (summaries, _) => new MarkupIndex(new EquatableArray<MarkupSummary>(summaries)))
                .WithTrackingName("Index");

            var webConfig = context.AdditionalTextsProvider
                .Where(static text => IsWebConfig(text.Path))
                .Select(static (text, cancellationToken) => new WebConfigInput(text.Path, WebConfigParser.Parse(text.GetText(cancellationToken)?.ToString() ?? string.Empty)))
                .Collect()
                .Select(static (configs, _) => PickRootWebConfig(configs))
                .WithTrackingName("WebConfig");

            context.RegisterSourceOutput(webConfig, static (productionContext, config) =>
            {
                if (config.Registrations.Problem is not null && config.Path.Length > 0)
                {
                    var location = Location.Create(config.Path, new TextSpan(0, 0), new LinePositionSpan(new LinePosition(0, 0), new LinePosition(0, 0)));
                    productionContext.ReportDiagnostic(Diagnostic.Create(Diagnostics.ParseProblem, location, VirtualPathResolver.GetFileName(config.Path), config.Registrations.Problem));
                }
            });

            var inputs = markup
                .Combine(index)
                .Combine(webConfig)
                .Combine(options)
                .Combine(context.CompilationProvider);

            context.RegisterSourceOutput(inputs, static (productionContext, input) =>
            {
                var (((document, markupIndex), config), generatorOptions) = input.Left;
                Execute(productionContext, document, markupIndex, config.Registrations, generatorOptions, input.Right);
            });
        }

        private static void Execute(
            SourceProductionContext context,
            MarkupDocument document,
            MarkupIndex index,
            WebConfigRegistrations webConfig,
            GeneratorOptions options,
            Compilation compilation)
        {
            var model = DesignerModelBuilder.Build(document, index, webConfig, options, compilation, context.ReportDiagnostic);
            if (model is null)
            {
                return;
            }

            var source = CSharpDesignerEmitter.Emit(model, document.RelativePath);
            context.AddSource(HintNames.ForMarkup(document.RelativePath), SourceText.From(source, Encoding.UTF8));
        }

        private static bool IsWebConfig(string path)
        {
            return string.Equals(VirtualPathResolver.GetFileName(path), "web.config", StringComparison.OrdinalIgnoreCase);
        }

        private static WebConfigInput PickRootWebConfig(ImmutableArray<WebConfigInput> configs)
        {
            // Only the root Web.config carries pages/controls registrations; if several were passed, take the shortest path.
            WebConfigInput? best = null;
            foreach (var config in configs)
            {
                if (best is null || config.Path.Length < best.Path.Length)
                {
                    best = config;
                }
            }

            return best ?? new WebConfigInput(string.Empty, WebConfigRegistrations.Empty);
        }

        private sealed record WebConfigInput(string Path, WebConfigRegistrations Registrations);
    }
}
