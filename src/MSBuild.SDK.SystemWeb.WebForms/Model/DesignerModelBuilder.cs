using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
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
        private readonly MarkupDocument _document;
        private readonly MarkupIndex _index;
        private readonly GeneratorOptions _options;
        private readonly TypeResolver _resolver;
        private readonly TagRegistry _registry;
        private readonly Action<Diagnostic> _report;
        private readonly List<DesignerField> _fields = new();
        private readonly HashSet<string> _fieldNames = new(StringComparer.Ordinal);
        private INamedTypeSymbol? _classSymbol;

        private DesignerModelBuilder(
            MarkupDocument document,
            MarkupIndex index,
            WebConfigRegistrations webConfig,
            GeneratorOptions options,
            Compilation compilation,
            TypeResolver resolver,
            Action<Diagnostic> report)
        {
            _document = document;
            _index = index;
            _options = options;
            _resolver = resolver;
            _registry = TagRegistry.Create(document, webConfig, AssemblyTagPrefixIndex.Get(compilation));
            _report = report;
        }

        public static DesignerModel? Build(
            MarkupDocument document,
            MarkupIndex index,
            WebConfigRegistrations webConfig,
            GeneratorOptions options,
            Compilation compilation,
            Action<Diagnostic> report)
        {
            var resolver = TypeResolver.Get(compilation);
            if (!resolver.HasSystemWeb)
            {
                return null;
            }

            return new DesignerModelBuilder(document, index, webConfig, options, compilation, resolver, report).Build();
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

            var (ns, className) = codeBehind.Value;

            foreach (var child in _document.Root.Children)
            {
                Visit(child, null);
            }

            TypedProperty? master = null;
            TypedProperty? previousPage = null;
            if (_document.Kind == MarkupKind.Page)
            {
                master = BuildTypedProperty("MasterType", "Master");
                previousPage = BuildTypedProperty("PreviousPageType", "PreviousPage");
            }

            return new DesignerModel(ns, className, EquatableArray<DesignerField>.From(_fields), master, previousPage);
        }

        private (string? Namespace, string ClassName)? ResolveCodeBehindClass(string inherits, int position)
        {
            _classSymbol = FindClass(inherits);
            if (_classSymbol is not null)
            {
                if (!_resolver.IsInThisCompilation(_classSymbol))
                {
                    // A partial declaration can only extend a class in this project; emitting one for a library type
                    // would declare a second, conflicting type. Visual Studio cannot generate anything here either.
                    Report(Diagnostics.CodeBehindClassInOtherAssembly, position, null, inherits, _document.RelativePath, _classSymbol.ContainingAssembly.Name);
                    return null;
                }

                var containing = _classSymbol.ContainingNamespace;
                var ns = containing is null || containing.IsGlobalNamespace ? null : containing.ToDisplayString();
                return (ns, _classSymbol.Name);
            }

            Report(Diagnostics.CodeBehindClassNotFound, position, null, inherits, _document.RelativePath);

            var dot = inherits.LastIndexOf('.');
            return dot < 0 ? (null, inherits) : (inherits.Substring(0, dot), inherits.Substring(dot + 1));
        }

        private INamedTypeSymbol? FindClass(string name)
        {
            var symbol = _resolver.GetTypeByMetadataName(name);
            if (symbol is null && _options.RootNamespace is not null && name.IndexOf('.') < 0)
            {
                symbol = _resolver.GetTypeByMetadataName(_options.RootNamespace + "." + name);
            }

            return symbol;
        }

        private void Visit(MarkupElement element, INamedTypeSymbol? containerType)
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
                    Visit(child, resolved?.Symbol);
                }

                return;
            }

            if (element.RunAtServer)
            {
                var metadataName = HtmlControlTypeMap.GetMetadataName(element.LocalName, element.GetAttribute("type"));
                var symbol = _resolver.GetTypeByMetadataName(metadataName);
                if (element.Id is not null)
                {
                    AddField(element, symbol is not null ? Display(symbol) : "global::" + metadataName);
                }

                foreach (var child in element.Children)
                {
                    Visit(child, null);
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
                            Visit(child, null);
                        }

                        return;
                    }

                    foreach (var child in element.Children)
                    {
                        Visit(child, property.Type as INamedTypeSymbol);
                    }

                    return;
                }
            }

            foreach (var child in element.Children)
            {
                Visit(child, null);
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
            return (symbol, symbol is not null ? Display(symbol) : QualifyFallback(summary.Inherits));
        }

        private (INamedTypeSymbol? Symbol, string TypeText) UserControlFallback()
        {
            const string UserControl = "System.Web.UI.UserControl";
            var symbol = _resolver.GetTypeByMetadataName(UserControl);
            return (symbol, symbol is not null ? Display(symbol) : "global::" + UserControl);
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
            return new TypedProperty(propertyName, masterSymbol is not null ? Display(masterSymbol) : QualifyFallback(summary.Inherits));
        }

        private void AddField(MarkupElement element, string typeText)
        {
            var id = element.Id!;
            if (!SyntaxFacts.IsValidIdentifier(id))
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
                return "global::" + _options.RootNamespace + "." + typeName;
            }

            return "global::" + typeName;
        }

        private static string Display(INamedTypeSymbol symbol) => symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        private void Report(DiagnosticDescriptor descriptor, int position, DiagnosticSeverity? severityOverride, params object[] args)
        {
            var location = CreateLocation(position);
            var diagnostic = severityOverride is null
                ? Diagnostic.Create(descriptor, location, args)
                : Diagnostic.Create(descriptor, location, severityOverride.Value, additionalLocations: null, properties: null, messageArgs: args);
            _report(diagnostic);
        }

        private Location CreateLocation(int position)
        {
            var clamped = Math.Max(0, Math.Min(position, _document.TextLength));
            var linePosition = _document.GetLinePosition(clamped);
            return Location.Create(_document.FilePath, new TextSpan(clamped, 0), new LinePositionSpan(linePosition, linePosition));
        }
    }
}
