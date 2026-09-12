using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace MSBuild.SDK.SystemWeb.WebForms.Generator.Resolution
{
    /// <summary>
    /// Case-insensitive type lookup over everything the compilation can see (source and references),
    /// because ASP.NET resolves tag names case-insensitively. Cached per <see cref="Compilation"/>.
    /// </summary>
    public sealed class TypeResolver
    {
        private static readonly ConditionalWeakTable<Compilation, TypeResolver> Cache = new();

        private readonly Compilation _compilation;
        private readonly Dictionary<string, Dictionary<string, INamedTypeSymbol>?> _namespaces = new(StringComparer.OrdinalIgnoreCase);

        private TypeResolver(Compilation compilation)
        {
            _compilation = compilation;
            Control = compilation.GetTypeByMetadataName("System.Web.UI.Control");
            ITemplate = compilation.GetTypeByMetadataName("System.Web.UI.ITemplate");
            Content = compilation.GetTypeByMetadataName("System.Web.UI.WebControls.Content");
        }

        public INamedTypeSymbol? Control { get; }

        public INamedTypeSymbol? ITemplate { get; }

        public INamedTypeSymbol? Content { get; }

        /// <summary>False when System.Web is not referenced (e.g. a design-time build before restore): generate nothing.</summary>
        public bool HasSystemWeb => Control is not null;

        /// <summary>True when the type is declared in the project being compiled, i.e. a partial declaration can be added to it.</summary>
        public bool IsInThisCompilation(INamedTypeSymbol type) => SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, _compilation.Assembly);

        public static TypeResolver Get(Compilation compilation)
        {
            return Cache.GetValue(compilation, static c => new TypeResolver(c));
        }

        public INamedTypeSymbol? GetTypeByMetadataName(string fullName) => _compilation.GetTypeByMetadataName(fullName);

        /// <summary>Finds a non-generic, usable type named <paramref name="typeName"/> (case-insensitive) in <paramref name="namespaceName"/> (case-insensitive).</summary>
        public INamedTypeSymbol? FindType(string namespaceName, string typeName)
        {
            if (!_namespaces.TryGetValue(namespaceName, out var types))
            {
                types = BuildNamespaceIndex(namespaceName);
                _namespaces[namespaceName] = types;
            }

            if (types is null)
            {
                return null;
            }

            return types.TryGetValue(typeName, out var symbol) ? symbol : null;
        }

        public bool IsControl(ITypeSymbol? type) => Control is not null && DerivesFrom(type, Control);

        public bool IsContent(ITypeSymbol? type) => Content is not null && DerivesFrom(type, Content);

        public bool IsTemplate(ITypeSymbol? type)
        {
            if (type is null || ITemplate is null)
            {
                return false;
            }

            if (SymbolEqualityComparer.Default.Equals(type, ITemplate))
            {
                return true;
            }

            foreach (var iface in type.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(iface, ITemplate))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool DerivesFrom(ITypeSymbol? type, INamedTypeSymbol baseType)
        {
            for (var current = type; current is not null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, baseType))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Finds an instance property by name (case-insensitive) on the type or its base types.</summary>
        public static IPropertySymbol? FindProperty(INamedTypeSymbol type, string name)
        {
            for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
            {
                foreach (var member in current.GetMembers())
                {
                    if (member is IPropertySymbol property
                        && !property.IsStatic
                        && !property.IsIndexer
                        && string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return property;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// True when the property is a template that is instantiated exactly once
        /// (<c>[TemplateInstance(TemplateInstance.Single)]</c>), in which case controls declared inside it get designer fields.
        /// </summary>
        public static bool IsSingleInstanceTemplate(IPropertySymbol property)
        {
            foreach (var attribute in property.GetAttributes())
            {
                var attributeClass = attribute.AttributeClass;
                if (attributeClass is null || attributeClass.Name != "TemplateInstanceAttribute")
                {
                    continue;
                }

                if (attribute.ConstructorArguments.Length == 1)
                {
                    var argument = attribute.ConstructorArguments[0];
                    return argument.Value is not null && Equals(argument.Value, GetEnumValue(argument.Type as INamedTypeSymbol, "Single"));
                }
            }

            return false;
        }

        /// <summary>The constant value of an enum member, looked up by name so we do not depend on the numeric values (TemplateInstance.Multiple is 0, Single is 1).</summary>
        private static object? GetEnumValue(INamedTypeSymbol? enumType, string memberName)
        {
            if (enumType is null)
            {
                return null;
            }

            foreach (var member in enumType.GetMembers(memberName))
            {
                if (member is IFieldSymbol { HasConstantValue: true } field)
                {
                    return field.ConstantValue;
                }
            }

            return null;
        }

        /// <summary>True when the class or a base class already declares a non-private member with this name.</summary>
        public static bool HasMember(INamedTypeSymbol type, string name)
        {
            if (type.GetMembers(name).Length > 0)
            {
                return true;
            }

            for (var current = type.BaseType; current is not null; current = current.BaseType)
            {
                foreach (var member in current.GetMembers(name))
                {
                    if (member.DeclaredAccessibility != Accessibility.Private)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private Dictionary<string, INamedTypeSymbol>? BuildNamespaceIndex(string namespaceName)
        {
            var ns = FindNamespace(namespaceName);
            if (ns is null)
            {
                return null;
            }

            var index = new Dictionary<string, INamedTypeSymbol>(StringComparer.OrdinalIgnoreCase);
            foreach (var type in ns.GetTypeMembers())
            {
                if (type.Arity != 0 || type.TypeKind != TypeKind.Class)
                {
                    continue;
                }

                if (type.DeclaredAccessibility != Accessibility.Public
                    && !SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, _compilation.Assembly))
                {
                    continue;
                }

                // First declaration wins when several assemblies contribute a type of the same name.
                if (!index.ContainsKey(type.Name))
                {
                    index[type.Name] = type;
                }
            }

            return index;
        }

        private INamespaceSymbol? FindNamespace(string namespaceName)
        {
            INamespaceSymbol current = _compilation.GlobalNamespace;
            foreach (var segment in namespaceName.Split('.'))
            {
                if (segment.Length == 0)
                {
                    return null;
                }

                INamespaceSymbol? next = null;
                foreach (var child in current.GetNamespaceMembers())
                {
                    if (child.Name == segment)
                    {
                        next = child;
                        break;
                    }

                    if (next is null && string.Equals(child.Name, segment, StringComparison.OrdinalIgnoreCase))
                    {
                        next = child;
                    }
                }

                if (next is null)
                {
                    return null;
                }

                current = next;
            }

            return current;
        }
    }
}
