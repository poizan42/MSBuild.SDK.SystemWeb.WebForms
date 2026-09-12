namespace MSBuild.SDK.SystemWeb.WebForms.Generator.Model
{
    /// <summary>A control field: <c>protected {FullyQualifiedType} {Name};</c>.</summary>
    public sealed record DesignerField(string Name, string FullyQualifiedType);

    /// <summary>A strongly typed <c>Master</c> or <c>PreviousPage</c> property.</summary>
    public sealed record TypedProperty(string Name, string FullyQualifiedType);

    /// <summary>
    /// Everything the emitter needs to write one designer file. <see cref="ContainingTypes"/> is non-empty when the
    /// code-behind class is nested (outermost first); each gets its own <c>partial class</c> wrapper.
    /// </summary>
    public sealed record DesignerModel(
        string? Namespace,
        EquatableArray<string> ContainingTypes,
        string ClassName,
        EquatableArray<DesignerField> Fields,
        TypedProperty? Master,
        TypedProperty? PreviousPage);
}
