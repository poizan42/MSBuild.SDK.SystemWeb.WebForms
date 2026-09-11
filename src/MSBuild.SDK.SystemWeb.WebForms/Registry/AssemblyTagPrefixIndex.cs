using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace MSBuild.SDK.SystemWeb.WebForms.Generator.Registry
{
    /// <summary>
    /// The <c>[assembly: System.Web.UI.TagPrefix(namespace, prefix)]</c> registrations of the project and every
    /// referenced assembly, plus ASP.NET's built-in default (<c>asp</c> -> <c>System.Web.UI.WebControls</c>).
    /// Cached per <see cref="Compilation"/>.
    /// </summary>
    public sealed class AssemblyTagPrefixIndex
    {
        private static readonly ConditionalWeakTable<Compilation, AssemblyTagPrefixIndex> Cache = new();

        private AssemblyTagPrefixIndex(ImmutableArray<TagRegistration> registrations)
        {
            Registrations = registrations;
        }

        public ImmutableArray<TagRegistration> Registrations { get; }

        public static AssemblyTagPrefixIndex Get(Compilation compilation)
        {
            return Cache.GetValue(compilation, static c => Build(c));
        }

        private static AssemblyTagPrefixIndex Build(Compilation compilation)
        {
            var builder = ImmutableArray.CreateBuilder<TagRegistration>();

            AddAttributes(compilation.Assembly, builder);
            foreach (var reference in compilation.References)
            {
                if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly)
                {
                    AddAttributes(assembly, builder);
                }
            }

            builder.Add(new TagRegistration("asp", null, "System.Web.UI.WebControls", "System.Web", null, RegistrationSource.Default));
            return new AssemblyTagPrefixIndex(builder.ToImmutable());
        }

        private static void AddAttributes(IAssemblySymbol assembly, ImmutableArray<TagRegistration>.Builder builder)
        {
            foreach (var attribute in assembly.GetAttributes())
            {
                var attributeClass = attribute.AttributeClass;
                if (attributeClass is null
                    || attributeClass.Name != "TagPrefixAttribute"
                    || attributeClass.ContainingNamespace?.ToDisplayString() != "System.Web.UI")
                {
                    continue;
                }

                if (attribute.ConstructorArguments.Length == 2
                    && attribute.ConstructorArguments[0].Value is string ns
                    && attribute.ConstructorArguments[1].Value is string prefix
                    && ns.Length > 0
                    && prefix.Length > 0)
                {
                    builder.Add(new TagRegistration(prefix, null, ns, assembly.Name, null, RegistrationSource.AssemblyAttribute));
                }
            }
        }
    }
}
