namespace MSBuild.SDK.SystemWeb.WebForms.Generator.Registry
{
    public enum RegistrationSource
    {
        PageDirective,
        WebConfig,
        AssemblyAttribute,
        Default,
    }

    /// <summary>
    /// A tag prefix registration. Either a namespace registration (<see cref="Namespace"/> set) or a user
    /// control registration (<see cref="TagName"/> and <see cref="Src"/> set).
    /// </summary>
    public sealed record TagRegistration(
        string TagPrefix,
        string? TagName,
        string? Namespace,
        string? Assembly,
        string? Src,
        RegistrationSource Source)
    {
        public bool IsUserControl => TagName is not null && Src is not null;

        public bool IsNamespace => Namespace is not null && TagName is null;
    }
}
