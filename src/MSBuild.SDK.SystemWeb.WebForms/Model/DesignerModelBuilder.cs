using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using MSBuild.SDK.SystemWeb.WebForms.Generator.Emit;
using MSBuild.SDK.SystemWeb.WebForms.Generator.Parsing;
using MSBuild.SDK.SystemWeb.WebForms.Generator.Registry;
using MSBuild.SDK.SystemWeb.WebForms.Generator.Resolution;

namespace MSBuild.SDK.SystemWeb.WebForms.Generator.Model
{
    /// <summary>
    /// Walks a parsed markup document and decides which designer fields and typed properties to generate,
    /// following the rules of the Visual Studio Web Application Project designer:
    /// one field per <c>runat="server"</c> control with an <c>ID</c>, no fields for controls inside
    /// multi-instance templates, and no field when the code-behind class already declares the member.
    /// </summary>
    public sealed class DesignerModelBuilder
    {
        /// <summary>Number of walks performed in this process; lets tests verify that cached documents skip the walk.</summary>
        internal static int WalkCount;

        private readonly MarkupDocument _document;
        private readonly MarkupIndex _index;
        private readonly GeneratorOptions _options;
        private readonly TypeResolver _resolver;
        private readonly TagRegistry _registry;
        private readonly List<DiagnosticInfo> _diagnostics = new();
        private readonly HashSet<ISymbol> _dependencies = new(SymbolEqualityComparer.Default);
        private readonly List<DesignerField> _fields = new();
        private readonly HashSet<string> _fieldNames;
        private readonly string _language;
        private INamedTypeSymbol? _classSymbol;
        private bool _cacheable = true;

        private DesignerModelBuilder(
            MarkupDocument document,
            MarkupIndex index,
            WebConfigRegistrations webConfig,
            GeneratorOptions options,
            Compilation compilation,
            TypeResolver resolver)
        {
            _document = document;
            _index = index;
            _options = options;
            _resolver = resolver;
            _registry = TagRegistry.Create(document, webConfig, AssemblyTagPrefixIndex.Get(compilation));
            _language = compilation.Language;
            // VB identifiers are case-insensitive, so IDs differing only in case would collide.
            _fieldNames = new HashSet<string>(_language == LanguageNames.VisualBasic ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        }

        /// <summary>The fully-qualified-name prefix of the compilation's language (<c>global::</c> or <c>Global.</c>); symbol display strings already use it.</summary>
        private string GlobalPrefix => _language == LanguageNames.VisualBasic ? "Global." : "global::";

        public static BuildResult Build(
            MarkupDocument document,
            MarkupIndex index,
            WebConfigRegistrations webConfig,
            GeneratorOptions options,
            Compilation compilation)
        {
            var resolver = TypeResolver.Get(compilation);
            if (!resolver.HasSystemWeb)
            {
                // Design-time build before restore: nothing to resolve against, and nothing worth caching.
                return new BuildResult(null, ImmutableArray<DiagnosticInfo>.Empty, ImmutableArray<SyntaxTree>.Empty, IsCacheable: false);
            }

            System.Threading.Interlocked.Increment(ref WalkCount);
            var builder = new DesignerModelBuilder(document, index, webConfig, options, compilation, resolver);
            var model = builder.Build();
            return new BuildResult(model, builder._diagnostics.ToImmutableArray(), builder.CollectDependentTrees(), builder._cacheable);
        }

        /// <summary>The syntax trees that declare every source symbol the walk consulted (metadata symbols have none).</summary>
        private ImmutableArray<SyntaxTree> CollectDependentTrees()
        {
            var trees = new HashSet<SyntaxTree>();
            foreach (var symbol in _dependencies)
            {
                foreach (var reference in symbol.DeclaringSyntaxReferences)
                {
                    trees.Add(reference.SyntaxTree);
                }
            }

            return trees.ToImmutableArray();
        }

        /// <summary>Records a type (and its base types, which member and property lookups walk) as a dependency of this document.</summary>
        private void DependOn(INamedTypeSymbol? type)
        {
            for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
            {
                if (!_dependencies.Add(current.OriginalDefinition))
                {
                    return;
                }
            }
        }

        private DesignerModel? Build()
        {
            foreach (var problem in _document.Problems)
            {
                Report(Diagnostics.ParseProblem, problem.Position, problem.IsInformational ? DiagnosticSeverity.Info : (DiagnosticSeverity?)null, _document.RelativePath, problem.Message);
            }

            var main = _document.MainDirective;
            var inherits = main?.Get("Inherits")?.Trim();
            if (string.IsNullOrEmpty(inherits))
            {
                if (main is not null && (main.Get("CodeBehind") is not null || main.Get("CodeFile") is not null))
                {
                    Report(Diagnostics.MissingInherits, main.Position, null, _document.RelativePath);
                }

                return null;
            }

            var codeBehind = ResolveCodeBehindClass(inherits!, main!.Position);
            if (codeBehind is null)
            {
                return null;
            }

            var (ns, containingTypes, className) = codeBehind.Value;

            foreach (var child in _document.Root.Children)
            {
                Visit(child, null, inServerHead: false);
            }

            TypedProperty? master = null;
            TypedProperty? previousPage = null;
            if (_document.Kind == MarkupKind.Page)
            {
                master = BuildTypedProperty("MasterType", "Master");
                previousPage = BuildTypedProperty("PreviousPageType", "PreviousPage");
            }

            return new DesignerModel(ns, containingTypes, className, EquatableArray<DesignerField>.From(_fields), master, previousPage);
        }

        private (string? Namespace, EquatableArray<string> ContainingTypes, string ClassName)? ResolveCodeBehindClass(string inherits, int position)
        {
            _classSymbol = FindClass(inherits);
            if (_classSymbol is not null)
            {
                DependOn(_classSymbol);
                if (!_resolver.IsInThisCompilation(_classSymbol))
                {
                    // A partial declaration can only extend a class in this project; emitting one for a library type
                    // would declare a second, conflicting type. Visual Studio cannot generate anything here either.
                    Report(Diagnostics.CodeBehindClassInOtherAssembly, position, null, inherits, _document.RelativePath, _classSymbol.ContainingAssembly.Name);
                    return null;
                }

                var containing = _classSymbol.ContainingNamespace;
                var ns = containing is null || containing.IsGlobalNamespace ? null : containing.ToDisplayString();

                var containingTypes = new List<string>();
                for (var outer = _classSymbol.ContainingType; outer is not null; outer = outer.ContainingType)
                {
                    containingTypes.Insert(0, outer.Name);
                }

                return (ns, EquatableArray<string>.From(containingTypes), _classSymbol.Name);
            }

            Report(Diagnostics.CodeBehindClassNotFound, position, null, inherits, _document.RelativePath);

            var dot = inherits.LastIndexOf('.');
            return dot < 0
                ? (null, EquatableArray<string>.Empty, inherits)
                : (inherits.Substring(0, dot), EquatableArray<string>.Empty, inherits.Substring(dot + 1));
        }

        /// <summary>
        /// Finds the class named in markup. Markup uses dots throughout (<c>Ns.Outer.Inner</c>), so for nested classes
        /// the trailing dots are progressively tried as nesting separators (<c>Ns.Outer+Inner</c>).
        /// </summary>
        private INamedTypeSymbol? FindClass(string name)
        {
            foreach (var candidate in MetadataNameCandidates(name))
            {
                var symbol = _resolver.GetTypeByMetadataName(candidate);
                if (symbol is not null)
                {
                    return symbol;
                }

                if (_options.RootNamespace is not null)
                {
                    symbol = _resolver.GetTypeByMetadataName(_options.RootNamespace + "." + candidate);
                    if (symbol is not null)
                    {
                        return symbol;
                    }
                }
            }

            return null;
        }

        private static IEnumerable<string> MetadataNameCandidates(string name)
        {
            yield return name;

            var parts = name.Split('.');
            for (var nested = 1; nested < parts.Length; nested++)
            {
                var outer = string.Join(".", parts, 0, parts.Length - nested);
                var inner = string.Join("+", parts, parts.Length - nested, nested);
                yield return outer + "+" + inner;
            }
        }

        private void Visit(MarkupElement element, INamedTypeSymbol? containerType, bool inServerHead)
        {
            if (element.Prefix is not null)
            {
                var resolved = ResolvePrefixedTag(element);
                if (element.RunAtServer && element.Id is not null)
                {
                    if (resolved is null)
                    {
                        Report(Diagnostics.UnresolvedControlType, element.Position, null, element.RawName, _document.RelativePath, element.Id);
                    }
                    else if (!_resolver.IsContent(resolved.Value.Symbol))
                    {
                        AddField(element, resolved.Value.TypeText);
                    }
                }

                foreach (var child in element.Children)
                {
                    Visit(child, resolved?.Symbol, inServerHead: false);
                }

                return;
            }

            // <title>, <link> and <meta> directly inside a server-side <head> are controls even without runat="server".
            var headChild = inServerHead ? HtmlControlTypeMap.GetHeadChildMetadataName(element.LocalName) : null;
            if (element.RunAtServer || headChild is not null)
            {
                var metadataName = headChild ?? HtmlControlTypeMap.GetMetadataName(element.LocalName, element.GetAttribute("type"));
                var symbol = _resolver.GetTypeByMetadataName(metadataName);
                if (element.Id is not null)
                {
                    AddField(element, symbol is not null ? Display(symbol) : GlobalPrefix + metadataName);
                }

                var isServerHead = element.RunAtServer && string.Equals(element.LocalName, "head", StringComparison.OrdinalIgnoreCase);
                foreach (var child in element.Children)
                {
                    Visit(child, null, isServerHead);
                }

                return;
            }

            if (containerType is not null)
            {
                var property = TypeResolver.FindProperty(containerType, element.LocalName);
                if (property is not null)
                {
                    if (_resolver.IsTemplate(property.Type))
                    {
                        if (!TypeResolver.IsSingleInstanceTemplate(property))
                        {
                            // Controls inside a multi-instance template (Repeater/GridView ItemTemplate, ...) are not page members.
                            return;
                        }

                        foreach (var child in element.Children)
                        {
                            Visit(child, null, inServerHead: false);
                        }

                        return;
                    }

                    foreach (var child in element.Children)
                    {
                        Visit(child, property.Type as INamedTypeSymbol, inServerHead: false);
                    }

                    return;
                }
            }

            foreach (var child in element.Children)
            {
                Visit(child, null, inServerHead: false);
            }
        }

        private (INamedTypeSymbol? Symbol, string TypeText)? ResolvePrefixedTag(MarkupElement element)
        {
            foreach (var registration in _registry.GetCandidates(element.Prefix!))
            {
                if (registration.IsUserControl)
                {
                    if (string.Equals(registration.TagName, element.LocalName, StringComparison.OrdinalIgnoreCase))
                    {
                        return ResolveUserControl(registration, element);
                    }
                }
                else if (registration.IsNamespace)
                {
                    var symbol = _resolver.FindType(registration.Namespace!, element.LocalName);
                    if (symbol is not null)
                    {
                        DependOn(symbol);
                        return (symbol, Display(symbol));
                    }
                }
            }

            return null;
        }

        private (INamedTypeSymbol? Symbol, string TypeText)? ResolveUserControl(TagRegistration registration, MarkupElement element)
        {
            var relativePath = VirtualPathResolver.Resolve(registration.Src, _document.RelativePath);
            var summary = _index.Find(relativePath);
            if (summary is null)
            {
                if (element.RunAtServer && element.Id is not null)
                {
                    Report(Diagnostics.ReferencedMarkupNotFound, element.Position, null, registration.Src ?? string.Empty, _document.RelativePath, "<%@ Register %>", element.Id, "the field is typed as System.Web.UI.UserControl");
                }

                return UserControlFallback();
            }

            if (summary.Inherits is null)
            {
                // An inline user control (no code-behind) compiles to a runtime-generated class; UserControl is the best static type.
                return UserControlFallback();
            }

            var symbol = FindClass(summary.Inherits);
            if (symbol is null)
            {
                // The class may appear in a later edit; do not cache a guess.
                _cacheable = false;
            }

            DependOn(symbol);
            return (symbol, symbol is not null ? Display(symbol) : QualifyFallback(summary.Inherits));
        }

        private (INamedTypeSymbol? Symbol, string TypeText) UserControlFallback()
        {
            const string UserControl = "System.Web.UI.UserControl";
            var symbol = _resolver.GetTypeByMetadataName(UserControl);
            return (symbol, symbol is not null ? Display(symbol) : GlobalPrefix + UserControl);
        }

        private TypedProperty? BuildTypedProperty(string directiveName, string propertyName)
        {
            Directive? directive = null;
            foreach (var candidate in _document.GetDirectives(directiveName))
            {
                directive = candidate;
                break;
            }

            if (directive is null)
            {
                return null;
            }

            // The user may have declared the typed property themselves.
            if (_classSymbol is not null && _classSymbol.GetMembers(propertyName).Length > 0)
            {
                return null;
            }

            var typeName = directive.Get("TypeName")?.Trim();
            if (!string.IsNullOrEmpty(typeName))
            {
                var symbol = FindClass(typeName!);
                if (symbol is null)
                {
                    _cacheable = false;
                }

                DependOn(symbol);
                return new TypedProperty(propertyName, symbol is not null ? Display(symbol) : QualifyFallback(typeName!));
            }

            var virtualPath = directive.Get("VirtualPath");
            if (string.IsNullOrWhiteSpace(virtualPath))
            {
                return null;
            }

            var relativePath = VirtualPathResolver.Resolve(virtualPath, _document.RelativePath);
            var summary = _index.Find(relativePath);
            if (summary is null || summary.Inherits is null)
            {
                Report(Diagnostics.ReferencedMarkupNotFound, directive.Position, null, virtualPath!.Trim(), _document.RelativePath, "<%@ " + directiveName + " %>", propertyName, "the typed property is not generated");
                return null;
            }

            var masterSymbol = FindClass(summary.Inherits);
            if (masterSymbol is null)
            {
                _cacheable = false;
            }

            DependOn(masterSymbol);
            return new TypedProperty(propertyName, masterSymbol is not null ? Display(masterSymbol) : QualifyFallback(summary.Inherits));
        }

        private void AddField(MarkupElement element, string typeText)
        {
            var id = element.Id!;
            if (!IdentifierRules.IsValidIdentifier(id))
            {
                Report(Diagnostics.ParseProblem, element.Position, null, _document.RelativePath, $"'{id}' is not a valid C# identifier; no designer field is generated for it.");
                return;
            }

            if (!_fieldNames.Add(id))
            {
                return;
            }

            if (_classSymbol is not null && TypeResolver.HasMember(_classSymbol, id))
            {
                // Declared in the code-behind (or a legacy .designer.cs, or a base class): the developer owns it.
                return;
            }

            _fields.Add(new DesignerField(id, typeText));
        }

        private string QualifyFallback(string typeName)
        {
            if (_options.RootNamespace is not null && typeName.IndexOf('.') < 0)
            {
                return GlobalPrefix + _options.RootNamespace + "." + typeName;
            }

            return GlobalPrefix + typeName;
        }

        private static string Display(INamedTypeSymbol symbol) => symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        private void Report(DiagnosticDescriptor descriptor, int position, DiagnosticSeverity? severityOverride, params string[] args)
        {
            // A failed lookup may succeed after the next edit anywhere in the project, so such results are never cached.
            if (ReferenceEquals(descriptor, Diagnostics.UnresolvedControlType)
                || ReferenceEquals(descriptor, Diagnostics.ReferencedMarkupNotFound)
                || ReferenceEquals(descriptor, Diagnostics.CodeBehindClassNotFound))
            {
                _cacheable = false;
            }

            _diagnostics.Add(new DiagnosticInfo(descriptor, position, severityOverride, EquatableArray<string>.From(args)));
        }
    }

    /// <summary>The outcome of walking one document, plus what it depended on so the result can be cached.</summary>
    public sealed record BuildResult(
        DesignerModel? Model,
        ImmutableArray<DiagnosticInfo> Diagnostics,
        ImmutableArray<SyntaxTree> DependentTrees,
        bool IsCacheable);
}
