namespace MSBuild.SDK.SystemWeb.WebForms.Generator.Model
{
    /// <summary>A control field: <c>protected {FullyQualifiedType} {Name};</c>.</summary>
    public sealed record DesignerField(string Name, string FullyQualifiedType);

    /// <summary>A strongly typed <c>Master</c> or <c>PreviousPage</c> property.</summary>
    public sealed record TypedProperty(string Name, string FullyQualifiedType);

    /// <summary>Everything the emitter needs to write one designer file.</summary>
    public sealed record DesignerModel(
        string? Namespace,
        string ClassName,
        EquatableArray<DesignerField> Fields,
        TypedProperty? Master,
        TypedProperty? PreviousPage);
}
