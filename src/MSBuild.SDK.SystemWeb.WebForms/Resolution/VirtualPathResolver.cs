using System;
using System.Collections.Generic;

namespace MSBuild.SDK.SystemWeb.WebForms.Generator.Resolution
{
    /// <summary>
    /// Resolves ASP.NET virtual paths (<c>~/Site.Master</c>, <c>/Controls/x.ascx</c>, <c>../x.ascx</c>) to
    /// normalized project-relative paths (forward slashes, no leading slash). Pure string manipulation:
    /// the application root is assumed to be the project directory.
    /// </summary>
    public static class VirtualPathResolver
    {
        public static string Normalize(string path)
        {
            var normalized = path.Replace('\\', '/').Trim();
            while (normalized.StartsWith("/", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(1);
            }

            return Collapse(normalized);
        }

        /// <summary>Resolves <paramref name="virtualPath"/> as referenced from the file at <paramref name="referencingRelativePath"/>.</summary>
        public static string? Resolve(string? virtualPath, string referencingRelativePath)
        {
            if (string.IsNullOrWhiteSpace(virtualPath))
            {
                return null;
            }

            var path = virtualPath!.Trim().Replace('\\', '/');
            if (path.StartsWith("~/", StringComparison.Ordinal))
            {
                return Normalize(path.Substring(2));
            }

            if (path.StartsWith("/", StringComparison.Ordinal))
            {
                return Normalize(path);
            }

            var referencing = referencingRelativePath.Replace('\\', '/');
            var slash = referencing.LastIndexOf('/');
            var directory = slash < 0 ? string.Empty : referencing.Substring(0, slash + 1);
            return Normalize(directory + path);
        }

        public static string GetDirectory(string relativePath)
        {
            var normalized = relativePath.Replace('\\', '/');
            var slash = normalized.LastIndexOf('/');
            return slash < 0 ? string.Empty : normalized.Substring(0, slash);
        }

        public static string GetFileName(string relativePath)
        {
            var normalized = relativePath.Replace('\\', '/');
            var slash = normalized.LastIndexOf('/');
            return slash < 0 ? normalized : normalized.Substring(slash + 1);
        }

        private static string Collapse(string path)
        {
            var segments = new List<string>();
            foreach (var segment in path.Split('/'))
            {
                if (segment.Length == 0 || segment == ".")
                {
                    continue;
                }

                if (segment == "..")
                {
                    if (segments.Count > 0)
                    {
                        segments.RemoveAt(segments.Count - 1);
                    }

                    continue;
                }

                segments.Add(segment);
            }

            return string.Join("/", segments);
        }
    }
}
