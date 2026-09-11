using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;

namespace MSBuild.SDK.SystemWeb.WebForms.Generator.Parsing
{
    public enum MarkupKind
    {
        Page,
        Control,
        Master,
    }

    /// <summary>An attribute on a tag or directive. Names are compared case-insensitively.</summary>
    public sealed record MarkupAttribute(string Name, string Value);

    /// <summary>A <c>&lt;%@ Name ... %&gt;</c> directive.</summary>
    public sealed record Directive(string Name, EquatableArray<MarkupAttribute> Attributes, int Position)
    {
        public string? Get(string attributeName) => Attributes.FindValue(attributeName);

        public bool Is(string name) => string.Equals(Name, name, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>An element in the markup tree. The synthetic root has an empty <see cref="RawName"/>.</summary>
    public sealed record MarkupElement(
        string RawName,
        string? Prefix,
        string LocalName,
        EquatableArray<MarkupAttribute> Attributes,
        bool RunAtServer,
        string? Id,
        EquatableArray<MarkupElement> Children,
        int Position)
    {
        public string? GetAttribute(string attributeName) => Attributes.FindValue(attributeName);
    }

    public sealed record ParseProblem(string Message, int Position, bool IsInformational);

    /// <summary>The parsed representation of one markup file. Equatable so the incremental pipeline can cache it.</summary>
    public sealed record MarkupDocument(
        string FilePath,
        string RelativePath,
        MarkupKind Kind,
        EquatableArray<Directive> Directives,
        MarkupElement Root,
        EquatableArray<ParseProblem> Problems,
        EquatableArray<int> LineStarts,
        int TextLength)
    {
        /// <summary>The main directive: <c>@Page</c>, <c>@Control</c> or <c>@Master</c>.</summary>
        public Directive? MainDirective
        {
            get
            {
                var name = Kind switch
                {
                    MarkupKind.Control => "Control",
                    MarkupKind.Master => "Master",
                    _ => "Page",
                };

                foreach (var directive in Directives)
                {
                    if (directive.Is(name))
                    {
                        return directive;
                    }
                }

                return null;
            }
        }

        public IEnumerable<Directive> GetDirectives(string name)
        {
            foreach (var directive in Directives)
            {
                if (directive.Is(name))
                {
                    yield return directive;
                }
            }
        }

        public LinePosition GetLinePosition(int position)
        {
            var starts = LineStarts.AsImmutableArray();
            if (starts.Length == 0)
            {
                return new LinePosition(0, 0);
            }

            var index = starts.BinarySearch(position);
            if (index < 0)
            {
                index = ~index - 1;
            }

            if (index < 0)
            {
                index = 0;
            }

            return new LinePosition(index, position - starts[index]);
        }

        public static MarkupKind KindFromExtension(string path)
        {
            if (path.EndsWith(".ascx", StringComparison.OrdinalIgnoreCase))
            {
                return MarkupKind.Control;
            }

            if (path.EndsWith(".master", StringComparison.OrdinalIgnoreCase))
            {
                return MarkupKind.Master;
            }

            return MarkupKind.Page;
        }

        public static bool IsMarkupPath(string path)
        {
            return path.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".ascx", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".master", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal static class MarkupAttributeExtensions
    {
        public static string? FindValue(this EquatableArray<MarkupAttribute> attributes, string name)
        {
            foreach (var attribute in attributes)
            {
                if (string.Equals(attribute.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return attribute.Value;
                }
            }

            return null;
        }
    }
}
