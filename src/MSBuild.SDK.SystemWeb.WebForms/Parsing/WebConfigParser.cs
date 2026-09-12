using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Xml;
using System.Xml.Linq;
using MSBuild.SDK.SystemWeb.WebForms.Generator.Registry;
using MSBuild.SDK.SystemWeb.WebForms.Generator.Resolution;

namespace MSBuild.SDK.SystemWeb.WebForms.Generator.Parsing
{
    /// <summary>
    /// A tag prefix registration that applies to markup under <see cref="DirectoryPrefix"/>
    /// (normalized project-relative directory with a trailing slash, or empty for the whole project).
    /// Registrations come from the Web.config in that directory or from a <c>&lt;location path="..."&gt;</c> element.
    /// </summary>
    public sealed record ScopedTagRegistration(string DirectoryPrefix, TagRegistration Registration);

    public sealed record WebConfigProblem(string Path, string Message);

    /// <summary>Tag prefix registrations from <c>system.web/pages/controls</c> of one or more Web.config files.</summary>
    public sealed record WebConfigRegistrations(EquatableArray<ScopedTagRegistration> Registrations, EquatableArray<WebConfigProblem> Problems)
    {
        public static readonly WebConfigRegistrations Empty = new(EquatableArray<ScopedTagRegistration>.Empty, EquatableArray<WebConfigProblem>.Empty);

        /// <summary>Registrations that apply to the markup file at <paramref name="relativePath"/>, most specific directory first.</summary>
        public IEnumerable<TagRegistration> ForDocument(string relativePath)
        {
            var normalized = VirtualPathResolver.Normalize(relativePath);
            var applicable = new List<ScopedTagRegistration>();
            foreach (var scoped in Registrations)
            {
                if (normalized.StartsWith(scoped.DirectoryPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    applicable.Add(scoped);
                }
            }

            // Stable sort: longer (more specific) prefixes first, document order within a prefix.
            var ordered = new List<ScopedTagRegistration>(applicable);
            ordered.Sort((a, b) =>
            {
                var byLength = b.DirectoryPrefix.Length.CompareTo(a.DirectoryPrefix.Length);
                return byLength != 0 ? byLength : applicable.IndexOf(a).CompareTo(applicable.IndexOf(b));
            });

            foreach (var scoped in ordered)
            {
                yield return scoped.Registration;
            }
        }

        public static WebConfigRegistrations Merge(ImmutableArray<WebConfigRegistrations> parts)
        {
            if (parts.Length == 0)
            {
                return Empty;
            }

            if (parts.Length == 1)
            {
                return parts[0];
            }

            var registrations = ImmutableArray.CreateBuilder<ScopedTagRegistration>();
            var problems = ImmutableArray.CreateBuilder<WebConfigProblem>();
            foreach (var part in parts)
            {
                registrations.AddRange(part.Registrations.AsImmutableArray());
                problems.AddRange(part.Problems.AsImmutableArray());
            }

            return new WebConfigRegistrations(new EquatableArray<ScopedTagRegistration>(registrations.ToImmutable()), new EquatableArray<WebConfigProblem>(problems.ToImmutable()));
        }
    }

    public static class WebConfigParser
    {
        /// <summary>
        /// Parses one Web.config. <paramref name="configPath"/> is the file path used in diagnostics and
        /// <paramref name="configDirectory"/> the normalized project-relative directory the file lives in ("" for the root).
        /// </summary>
        public static WebConfigRegistrations Parse(string text, string configPath = "Web.config", string configDirectory = "")
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return WebConfigRegistrations.Empty;
            }

            XDocument document;
            try
            {
                document = XDocument.Parse(text, LoadOptions.None);
            }
            catch (XmlException ex)
            {
                return new WebConfigRegistrations(
                    EquatableArray<ScopedTagRegistration>.Empty,
                    EquatableArray<WebConfigProblem>.From(new[] { new WebConfigProblem(configPath, $"Web.config could not be parsed: {ex.Message}") }));
            }

            var root = document.Root;
            if (root is null)
            {
                return WebConfigRegistrations.Empty;
            }

            var basePrefix = ToPrefix(configDirectory);
            var registrations = ImmutableArray.CreateBuilder<ScopedTagRegistration>();

            AddControls(root, basePrefix, registrations);
            foreach (var location in root.Elements("location"))
            {
                var path = (string?)location.Attribute("path");
                var prefix = string.IsNullOrWhiteSpace(path) || path == "." ? basePrefix : ToPrefix(basePrefix + path);
                AddControls(location, prefix, registrations);
            }

            return new WebConfigRegistrations(new EquatableArray<ScopedTagRegistration>(registrations.ToImmutable()), EquatableArray<WebConfigProblem>.Empty);
        }

        private static void AddControls(XElement parent, string prefix, ImmutableArray<ScopedTagRegistration>.Builder registrations)
        {
            var controls = parent.Element("system.web")?.Element("pages")?.Element("controls");
            if (controls is null)
            {
                return;
            }

            foreach (var add in controls.Elements("add"))
            {
                var tagPrefix = (string?)add.Attribute("tagPrefix");
                if (string.IsNullOrEmpty(tagPrefix))
                {
                    continue;
                }

                registrations.Add(new ScopedTagRegistration(prefix, new TagRegistration(
                    tagPrefix!,
                    NullIfEmpty((string?)add.Attribute("tagName")),
                    NullIfEmpty((string?)add.Attribute("namespace")),
                    NullIfEmpty((string?)add.Attribute("assembly")),
                    NullIfEmpty((string?)add.Attribute("src")),
                    RegistrationSource.WebConfig)));
            }
        }

        private static string ToPrefix(string directory)
        {
            var normalized = VirtualPathResolver.Normalize(directory);
            return normalized.Length == 0 ? string.Empty : normalized + "/";
        }

        private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
    }
}
