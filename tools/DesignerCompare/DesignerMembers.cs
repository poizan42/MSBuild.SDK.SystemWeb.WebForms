using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.VisualBasic;
using CS = Microsoft.CodeAnalysis.CSharp.Syntax;
using VB = Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace DesignerCompare;

/// <summary>The members a designer file declares: control fields and typed Master/PreviousPage properties.</summary>
public sealed class DesignerMembers
{
    public SortedDictionary<string, string> Fields { get; } = new(StringComparer.Ordinal);

    public SortedDictionary<string, string> Properties { get; } = new(StringComparer.Ordinal);

    public string? ClassName { get; private set; }

    public string? Namespace { get; private set; }

    public static DesignerMembers Parse(string source, string language)
    {
        return language == LanguageNames.VisualBasic ? ParseVisualBasic(source) : ParseCSharp(source);
    }

    private static DesignerMembers ParseCSharp(string source)
    {
        var members = new DesignerMembers();
        // Both language namespaces are imported, so use the root directly instead of the GetCompilationUnitRoot() extension.
        var root = (CS.CompilationUnitSyntax)CSharpSyntaxTree.ParseText(source).GetRoot();

        foreach (var type in root.DescendantNodes().OfType<CS.TypeDeclarationSyntax>())
        {
            // The innermost declared type is the code-behind class; nested wrappers only carry the inner one.
            if (type.Members.OfType<CS.TypeDeclarationSyntax>().Any() && !type.Members.OfType<CS.FieldDeclarationSyntax>().Any())
            {
                continue;
            }

            members.ClassName = type.Identifier.ValueText;
            members.Namespace = type.Ancestors().OfType<CS.BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();

            foreach (var member in type.Members)
            {
                switch (member)
                {
                    case CS.FieldDeclarationSyntax field:
                        var fieldType = NormalizeType(field.Declaration.Type.ToString());
                        foreach (var variable in field.Declaration.Variables)
                        {
                            members.Fields[NormalizeName(variable.Identifier.ValueText)] = fieldType;
                        }

                        break;

                    case CS.PropertyDeclarationSyntax property:
                        members.Properties[property.Identifier.ValueText] = NormalizeType(property.Type.ToString());
                        break;
                }
            }
        }

        return members;
    }

    private static DesignerMembers ParseVisualBasic(string source)
    {
        var members = new DesignerMembers();
        var root = (VB.CompilationUnitSyntax)VisualBasicSyntaxTree.ParseText(source).GetRoot();

        foreach (var type in root.DescendantNodes().OfType<VB.ClassBlockSyntax>())
        {
            if (type.Members.OfType<VB.ClassBlockSyntax>().Any() && !type.Members.OfType<VB.FieldDeclarationSyntax>().Any())
            {
                continue;
            }

            members.ClassName = type.ClassStatement.Identifier.ValueText;
            members.Namespace = type.Ancestors().OfType<VB.NamespaceBlockSyntax>().FirstOrDefault()?.NamespaceStatement.Name.ToString();

            foreach (var member in type.Members)
            {
                switch (member)
                {
                    case VB.FieldDeclarationSyntax field:
                        foreach (var declarator in field.Declarators)
                        {
                            var fieldType = declarator.AsClause is VB.SimpleAsClauseSyntax asClause ? NormalizeType(asClause.Type.ToString()) : "?";
                            foreach (var name in declarator.Names)
                            {
                                members.Fields[NormalizeName(name.Identifier.ValueText)] = fieldType;
                            }
                        }

                        break;

                    case VB.PropertyBlockSyntax propertyBlock:
                        AddProperty(members, propertyBlock.PropertyStatement);
                        break;

                    case VB.PropertyStatementSyntax propertyStatement:
                        AddProperty(members, propertyStatement);
                        break;
                }
            }
        }

        return members;
    }

    private static void AddProperty(DesignerMembers members, VB.PropertyStatementSyntax property)
    {
        var type = property.AsClause is VB.SimpleAsClauseSyntax asClause ? NormalizeType(asClause.Type.ToString()) : "?";
        members.Properties[property.Identifier.ValueText] = type;
    }

    public static string NormalizeType(string type)
    {
        return type.Replace("global::", string.Empty).Replace("Global.", string.Empty).Replace(" ", string.Empty);
    }

    private static string NormalizeName(string name) => name.TrimStart('@').Trim('[', ']');
}
