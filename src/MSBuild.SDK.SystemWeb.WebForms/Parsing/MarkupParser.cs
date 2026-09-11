using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace MSBuild.SDK.SystemWeb.WebForms.Generator.Parsing
{
    /// <summary>
    /// Tolerant ASP.NET markup parser. Produces the element tree the designer generator needs
    /// (tags, attributes, nesting, directives); everything else (literal text, code blocks, server comments,
    /// server-side script blocks) is skipped. Never throws on malformed markup.
    /// </summary>
    public static class MarkupParser
    {
        private static readonly HashSet<string> VoidHtmlElements = new(StringComparer.OrdinalIgnoreCase)
        {
            "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr",
        };

        public static MarkupDocument Parse(string filePath, string relativePath, string text, MarkupKind kind)
        {
            text ??= string.Empty;
            var directives = ImmutableArray.CreateBuilder<Directive>();
            var problems = ImmutableArray.CreateBuilder<ParseProblem>();
            var root = new ElementBuilder(string.Empty, null, string.Empty, EquatableArray<MarkupAttribute>.Empty, false, null, 0);
            var stack = new Stack<ElementBuilder>();
            stack.Push(root);

            var pos = 0;
            try
            {
                while (pos < text.Length)
                {
                    Match m;

                    if ((m = MarkupRegexes.Comment.Match(text, pos)).Success)
                    {
                        pos += m.Length;
                        continue;
                    }

                    if ((m = MarkupRegexes.Directive.Match(text, pos)).Success)
                    {
                        directives.Add(ReadDirective(m, kind, pos));
                        pos += m.Length;
                        continue;
                    }

                    if ((m = MarkupRegexes.Include.Match(text, pos)).Success)
                    {
                        problems.Add(new ParseProblem(
                            $"Server-side include '{m.Groups["filename"].Value}' is not followed; controls declared in it will not get designer fields.",
                            pos,
                            IsInformational: true));
                        pos += m.Length;
                        continue;
                    }

                    if ((m = MarkupRegexes.AspExpr.Match(text, pos)).Success
                        || (m = MarkupRegexes.AspEncodedExpr.Match(text, pos)).Success
                        || (m = MarkupRegexes.DatabindExpr.Match(text, pos)).Success
                        || (m = MarkupRegexes.AspCode.Match(text, pos)).Success)
                    {
                        pos += m.Length;
                        continue;
                    }

                    if ((m = MarkupRegexes.EndTag.Match(text, pos)).Success)
                    {
                        CloseElement(stack, m.Groups["tagname"].Value);
                        pos += m.Length;
                        continue;
                    }

                    if ((m = MarkupRegexes.Tag.Match(text, pos)).Success)
                    {
                        var element = ReadTag(m, pos);
                        pos += m.Length;

                        if (element.IsServerScript)
                        {
                            // <script runat="server"> contains code, not markup; skip to the closing tag.
                            pos = SkipServerScript(text, pos);
                            continue;
                        }

                        stack.Peek().Children.Add(element);
                        var selfClosing = m.Groups["empty"].Success
                            || (element.Prefix is null && VoidHtmlElements.Contains(element.LocalName));
                        if (!selfClosing)
                        {
                            stack.Push(element);
                        }

                        continue;
                    }

                    if ((m = MarkupRegexes.Text.Match(text, pos)).Success)
                    {
                        pos += m.Length;
                        continue;
                    }

                    // '<' that starts none of the above (<!DOCTYPE>, <!-- -->, <?xml ?>, a stray '<'): step over it.
                    // HTML comments are deliberately transparent: ASP.NET still parses server controls inside them.
                    pos++;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                problems.Add(new ParseProblem("Parsing timed out; the markup is too complex to analyse. Designer code may be incomplete.", pos, IsInformational: false));
            }

            return new MarkupDocument(
                filePath,
                relativePath,
                kind,
                new EquatableArray<Directive>(directives.ToImmutable()),
                root.Build(),
                new EquatableArray<ParseProblem>(problems.ToImmutable()),
                ComputeLineStarts(text),
                text.Length);
        }

        private static Directive ReadDirective(Match m, MarkupKind kind, int position)
        {
            var names = m.Groups["attrname"].Captures;
            var equals = m.Groups["equal"].Captures;
            var values = m.Groups["attrval"].Captures;

            string? directiveName = null;
            var attributes = ImmutableArray.CreateBuilder<MarkupAttribute>();
            var count = Math.Min(names.Count, Math.Min(equals.Count, values.Count));
            for (var i = 0; i < count; i++)
            {
                if (equals[i].Length == 0)
                {
                    directiveName ??= names[i].Value;
                }
                else
                {
                    attributes.Add(new MarkupAttribute(names[i].Value, values[i].Value));
                }
            }

            directiveName ??= kind switch
            {
                MarkupKind.Control => "Control",
                MarkupKind.Master => "Master",
                _ => "Page",
            };

            return new Directive(directiveName, new EquatableArray<MarkupAttribute>(attributes.ToImmutable()), position);
        }

        private static ElementBuilder ReadTag(Match m, int position)
        {
            var rawName = m.Groups["tagname"].Value;
            string? prefix = null;
            var localName = rawName;
            var colon = rawName.IndexOf(':');
            if (colon > 0 && colon < rawName.Length - 1)
            {
                prefix = rawName.Substring(0, colon);
                localName = rawName.Substring(colon + 1);
            }

            var names = m.Groups["attrname"].Captures;
            var values = m.Groups["attrval"].Captures;
            var attributes = ImmutableArray.CreateBuilder<MarkupAttribute>();
            var runAtServer = false;
            string? id = null;
            var count = Math.Min(names.Count, values.Count);
            for (var i = 0; i < count; i++)
            {
                var name = names[i].Value;
                var value = values[i].Value;
                attributes.Add(new MarkupAttribute(name, value));

                if (string.Equals(name, "runat", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(value.Trim(), "server", StringComparison.OrdinalIgnoreCase))
                {
                    runAtServer = true;
                }
                else if (string.Equals(name, "id", StringComparison.OrdinalIgnoreCase) && id is null)
                {
                    var trimmed = value.Trim();
                    id = trimmed.Length == 0 ? null : trimmed;
                }
            }

            return new ElementBuilder(rawName, prefix, localName, new EquatableArray<MarkupAttribute>(attributes.ToImmutable()), runAtServer, id, position);
        }

        private static void CloseElement(Stack<ElementBuilder> stack, string tagName)
        {
            // Find the nearest open element with this name; if none, ignore the stray end tag.
            var depth = 0;
            foreach (var open in stack)
            {
                if (depth == stack.Count - 1)
                {
                    break; // never pop the root
                }

                if (string.Equals(open.RawName, tagName, StringComparison.OrdinalIgnoreCase))
                {
                    for (var i = 0; i <= depth; i++)
                    {
                        stack.Pop();
                    }

                    return;
                }

                depth++;
            }
        }

        private static int SkipServerScript(string text, int pos)
        {
            var search = pos;
            while (search < text.Length)
            {
                var index = text.IndexOf("</script", search, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    return text.Length;
                }

                var close = text.IndexOf('>', index);
                return close < 0 ? text.Length : close + 1;
            }

            return text.Length;
        }

        private static EquatableArray<int> ComputeLineStarts(string text)
        {
            var starts = ImmutableArray.CreateBuilder<int>();
            starts.Add(0);
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }

                    starts.Add(i + 1);
                }
                else if (c == '\n')
                {
                    starts.Add(i + 1);
                }
            }

            return new EquatableArray<int>(starts.ToImmutable());
        }

        private sealed class ElementBuilder
        {
            public ElementBuilder(string rawName, string? prefix, string localName, EquatableArray<MarkupAttribute> attributes, bool runAtServer, string? id, int position)
            {
                RawName = rawName;
                Prefix = prefix;
                LocalName = localName;
                Attributes = attributes;
                RunAtServer = runAtServer;
                Id = id;
                Position = position;
            }

            public string RawName { get; }

            public string? Prefix { get; }

            public string LocalName { get; }

            public EquatableArray<MarkupAttribute> Attributes { get; }

            public bool RunAtServer { get; }

            public string? Id { get; }

            public int Position { get; }

            public List<ElementBuilder> Children { get; } = new();

            public bool IsServerScript => Prefix is null && RunAtServer && string.Equals(LocalName, "script", StringComparison.OrdinalIgnoreCase);

            public MarkupElement Build()
            {
                var children = ImmutableArray.CreateBuilder<MarkupElement>(Children.Count);
                foreach (var child in Children)
                {
                    children.Add(child.Build());
                }

                return new MarkupElement(RawName, Prefix, LocalName, Attributes, RunAtServer, Id, new EquatableArray<MarkupElement>(children.MoveToImmutable()), Position);
            }
        }
    }
}
