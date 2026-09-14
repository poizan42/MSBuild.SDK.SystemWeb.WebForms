using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace MSBuild.SDK.SystemWeb.WebForms.Tests.Infrastructure;

/// <summary>
/// Metadata references for the .NET Framework 4.8 reference assemblies, so generated code is verified
/// against the real System.Web surface. Location: <c>NETFX48_REFERENCE_ASSEMBLIES</c> or the default
/// Windows install path.
/// </summary>
public static class NetFx48ReferenceAssemblies
{
    private static readonly string[] AssemblyNames =
    {
        "mscorlib", "System", "System.Core", "System.Xml", "System.Configuration", "System.Drawing", "System.Data",
        "System.Web", "System.Web.Extensions", "Microsoft.VisualBasic",
    };

    private static readonly Lazy<ImmutableArray<MetadataReference>> LazyReferences = new(Load);

    public static string? Directory { get; } = Locate();

    public static bool IsAvailable => Directory is not null;

    public static ImmutableArray<MetadataReference> References => LazyReferences.Value;

    private static string? Locate()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("NETFX48_REFERENCE_ASSEMBLIES");
        if (!string.IsNullOrEmpty(fromEnvironment) && File.Exists(Path.Combine(fromEnvironment, "System.Web.dll")))
        {
            return fromEnvironment;
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (string.IsNullOrEmpty(programFiles))
        {
            return null;
        }

        var candidate = Path.Combine(programFiles, "Reference Assemblies", "Microsoft", "Framework", ".NETFramework", "v4.8");
        return File.Exists(Path.Combine(candidate, "System.Web.dll")) ? candidate : null;
    }

    private static ImmutableArray<MetadataReference> Load()
    {
        if (Directory is null)
        {
            throw new InvalidOperationException("The .NET Framework 4.8 reference assemblies were not found.");
        }

        var builder = ImmutableArray.CreateBuilder<MetadataReference>(AssemblyNames.Length);
        foreach (var name in AssemblyNames)
        {
            builder.Add(MetadataReference.CreateFromFile(Path.Combine(Directory, name + ".dll")));
        }

        return builder.MoveToImmutable();
    }
}

/// <summary>A fact that is skipped when the .NET Framework 4.8 reference assemblies are not installed.</summary>
public sealed class NetFx48FactAttribute : FactAttribute
{
    public NetFx48FactAttribute()
    {
        if (!NetFx48ReferenceAssemblies.IsAvailable)
        {
            Skip = "The .NET Framework 4.8 reference assemblies are not installed (set NETFX48_REFERENCE_ASSEMBLIES).";
        }
    }
}
