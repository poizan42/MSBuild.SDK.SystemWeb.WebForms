using Microsoft.CodeAnalysis;

namespace MSBuild.SDK.SystemWeb.WebForms.Generator
{
    internal static class Diagnostics
    {
        private const string Category = "MSBuild.SDK.SystemWeb.WebForms";

        public static readonly DiagnosticDescriptor UnresolvedControlType = new(
            id: "SWWF001",
            title: "Unresolved server control type",
            messageFormat: "Could not resolve the type of server control '<{0}>' in '{1}'; no designer field is generated for '{2}'",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "The tag prefix is not registered (via <%@ Register %>, Web.config pages/controls, or a [TagPrefix] assembly attribute) or the type does not exist in the referenced assemblies.");

        public static readonly DiagnosticDescriptor MissingInherits = new(
            id: "SWWF002",
            title: "CodeBehind without Inherits",
            messageFormat: "'{0}' has a CodeBehind/CodeFile attribute but no Inherits attribute; designer code is not generated",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor ParseProblem = new(
            id: "SWWF003",
            title: "Markup parse problem",
            messageFormat: "Problem parsing '{0}': {1}",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor ReferencedMarkupNotFound = new(
            id: "SWWF004",
            title: "Referenced markup file not found",
            messageFormat: "The markup file '{0}' referenced from '{1}' ({2}) was not found among the project's markup files; the exact type of '{3}' cannot be determined and {4}",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Master pages and user controls are resolved through the AdditionalFiles the SDK passes to the compiler. Files outside the project, or excluded from Content, cannot be resolved.");

        public static readonly DiagnosticDescriptor CodeBehindClassInOtherAssembly = new(
            id: "SWWF006",
            title: "Code-behind class is declared in another assembly",
            messageFormat: "The class '{0}' that '{1}' inherits is declared in assembly '{2}', not in this project; designer code cannot be added to it",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "Designer code is a partial class declaration and can only extend a class declared in the project being compiled.");

        public static readonly DiagnosticDescriptor WebConfigRegistrationWithoutAssembly = new(
            id: "SWWF007",
            title: "Web.config control registration without assembly",
            messageFormat: "Web.config registers tag prefix '{0}' for namespace '{1}' without an assembly attribute; at runtime ASP.NET only searches App_Code for such registrations, which SDK-style projects do not have, so '<{2}>' will fail with 'Unknown server tag'. Add assembly=\"{3}\" to the registration.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "The generator resolves the type through the compilation regardless, so the page compiles but fails when ASP.NET parses it.");

        public static readonly DiagnosticDescriptor CodeBehindClassNotFound = new(
            id: "SWWF005",
            title: "Code-behind class not found in compilation",
            messageFormat: "The code-behind class '{0}' for '{1}' was not found in the compilation; generating the partial class from the Inherits attribute",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true);
    }
}
