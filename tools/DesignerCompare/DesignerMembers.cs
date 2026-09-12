using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DesignerCompare;

/// <summary>The members a designer file declares: control fields and typed Master/PreviousPage properties.</summary>
public sealed class DesignerMembers
{
    public SortedDictionary<string, string> Fields { get; } = new(StringComparer.Ordinal);

    public SortedDictionary<string, string> Properties { get; } = new(StringComparer.Ordinal);

    public string? ClassName { get; private set; }

    public string? Namespace { get; private set; }

    public static DesignerMembers Parse(string source)
    {
        var members = new DesignerMembers();
        var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();

        foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            // The innermost declared type is the code-behind class; nested wrappers only carry the inner one.
            if (type.Members.OfType<TypeDeclarationSyntax>().Any() && !type.Members.OfType<FieldDeclarationSyntax>().Any())
            {
                continue;
            }

            members.ClassName = type.Identifier.ValueText;
            members.Namespace = type.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();

            foreach (var member in type.Members)
            {
                switch (member)
                {
                    case FieldDeclarationSyntax field:
                        var fieldType = NormalizeType(field.Declaration.Type.ToString());
                        foreach (var variable in field.Declaration.Variables)
                        {
                            members.Fields[NormalizeName(variable.Identifier.ValueText)] = fieldType;
                        }

                        break;

                    case PropertyDeclarationSyntax property:
                        members.Properties[property.Identifier.ValueText] = NormalizeType(property.Type.ToString());
                        break;
                }
            }
        }

        return members;
    }

    public static string NormalizeType(string type)
    {
        return type.Replace("global::", string.Empty).Replace(" ", string.Empty);
    }

    private static string NormalizeName(string name) => name.TrimStart('@');
}
