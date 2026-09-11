using System.Collections.Immutable;
using System.Xml;
using System.Xml.Linq;
using MSBuild.SDK.SystemWeb.WebForms.Generator.Registry;

namespace MSBuild.SDK.SystemWeb.WebForms.Generator.Parsing
{
    /// <summary>Tag prefix registrations from <c>configuration/system.web/pages/controls</c> in Web.config.</summary>
    public sealed record WebConfigRegistrations(EquatableArray<TagRegistration> Registrations, string? Problem)
    {
        public static readonly WebConfigRegistrations Empty = new(EquatableArray<TagRegistration>.Empty, null);
    }

    public static class WebConfigParser
    {
        public static WebConfigRegistrations Parse(string text)
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
                return new WebConfigRegistrations(EquatableArray<TagRegistration>.Empty, $"Web.config could not be parsed: {ex.Message}");
            }

            var controls = document.Root?.Element("system.web")?.Element("pages")?.Element("controls");
            if (controls is null)
            {
                return WebConfigRegistrations.Empty;
            }

            var registrations = ImmutableArray.CreateBuilder<TagRegistration>();
            foreach (var add in controls.Elements("add"))
            {
                var tagPrefix = (string?)add.Attribute("tagPrefix");
                if (string.IsNullOrEmpty(tagPrefix))
                {
                    continue;
                }

                registrations.Add(new TagRegistration(
                    tagPrefix!,
                    NullIfEmpty((string?)add.Attribute("tagName")),
                    NullIfEmpty((string?)add.Attribute("namespace")),
                    NullIfEmpty((string?)add.Attribute("assembly")),
                    NullIfEmpty((string?)add.Attribute("src")),
                    RegistrationSource.WebConfig));
            }

            return new WebConfigRegistrations(new EquatableArray<TagRegistration>(registrations.ToImmutable()), null);
        }

        private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
    }
}
