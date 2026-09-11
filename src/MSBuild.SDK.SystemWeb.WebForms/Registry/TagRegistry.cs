using System;
using System.Collections.Generic;
using MSBuild.SDK.SystemWeb.WebForms.Generator.Parsing;

namespace MSBuild.SDK.SystemWeb.WebForms.Generator.Registry
{
    /// <summary>
    /// The ordered set of tag prefix registrations that apply to one markup document:
    /// page-level <c>&lt;%@ Register %&gt;</c> directives, then Web.config, then assembly attributes and defaults.
    /// </summary>
    public sealed class TagRegistry
    {
        private readonly List<TagRegistration> _registrations = new();

        private TagRegistry()
        {
        }

        public static TagRegistry Create(MarkupDocument document, WebConfigRegistrations webConfig, AssemblyTagPrefixIndex assemblies)
        {
            var registry = new TagRegistry();

            foreach (var directive in document.GetDirectives("Register"))
            {
                var tagPrefix = directive.Get("TagPrefix");
                if (string.IsNullOrEmpty(tagPrefix))
                {
                    continue;
                }

                var tagName = NullIfEmpty(directive.Get("TagName"));
                var src = NullIfEmpty(directive.Get("Src"));
                var ns = NullIfEmpty(directive.Get("Namespace"));
                var assembly = NullIfEmpty(directive.Get("Assembly"));

                if (tagName is not null && src is not null)
                {
                    registry._registrations.Add(new TagRegistration(tagPrefix!, tagName, null, null, src, RegistrationSource.PageDirective));
                }
                else if (ns is not null)
                {
                    registry._registrations.Add(new TagRegistration(tagPrefix!, null, ns, assembly, null, RegistrationSource.PageDirective));
                }
            }

            registry._registrations.AddRange(webConfig.Registrations);
            registry._registrations.AddRange(assemblies.Registrations);
            return registry;
        }

        /// <summary>All registrations for a prefix, in precedence order.</summary>
        public IEnumerable<TagRegistration> GetCandidates(string prefix)
        {
            foreach (var registration in _registrations)
            {
                if (string.Equals(registration.TagPrefix, prefix, StringComparison.OrdinalIgnoreCase))
                {
                    yield return registration;
                }
            }
        }

        private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
    }
}
