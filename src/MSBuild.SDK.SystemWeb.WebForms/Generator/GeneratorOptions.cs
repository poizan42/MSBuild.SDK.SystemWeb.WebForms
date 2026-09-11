using System;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MSBuild.SDK.SystemWeb.WebForms.Generator
{
    /// <summary>MSBuild properties the generator needs, read from the analyzer config (<c>CompilerVisibleProperty</c>).</summary>
    public sealed record GeneratorOptions(string? ProjectDir, string? RootNamespace)
    {
        public static GeneratorOptions From(AnalyzerConfigOptionsProvider provider)
        {
            var global = provider.GlobalOptions;
            string? projectDir = null;
            if (global.TryGetValue("build_property.ProjectDir", out var pd) && !string.IsNullOrWhiteSpace(pd))
            {
                projectDir = pd;
            }
            else if (global.TryGetValue("build_property.MSBuildProjectDirectory", out var mpd) && !string.IsNullOrWhiteSpace(mpd))
            {
                projectDir = mpd;
            }

            string? rootNamespace = null;
            if (global.TryGetValue("build_property.RootNamespace", out var rn) && !string.IsNullOrWhiteSpace(rn))
            {
                rootNamespace = rn.Trim();
            }

            return new GeneratorOptions(NormalizeDirectory(projectDir), rootNamespace);
        }

        /// <summary>Makes <paramref name="filePath"/> relative to <see cref="ProjectDir"/> (forward slashes); falls back to the file name.</summary>
        public string GetRelativePath(string filePath)
        {
            var normalizedFile = filePath.Replace('\\', '/');
            if (ProjectDir is not null && normalizedFile.StartsWith(ProjectDir, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedFile.Substring(ProjectDir.Length);
            }

            var slash = normalizedFile.LastIndexOf('/');
            return slash < 0 ? normalizedFile : normalizedFile.Substring(slash + 1);
        }

        private static string? NormalizeDirectory(string? directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                return null;
            }

            var normalized = directory!.Trim().Replace('\\', '/');
            return normalized.EndsWith("/", StringComparison.Ordinal) ? normalized : normalized + "/";
        }
    }
}
