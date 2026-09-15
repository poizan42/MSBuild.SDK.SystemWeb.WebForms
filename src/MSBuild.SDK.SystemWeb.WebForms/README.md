# KfbSoft.MSBuild.SDK.SystemWeb.WebForms

A secondary MSBuild SDK for [MSBuild.SDK.SystemWeb](https://github.com/CZEMacLeod/MSBuild.SDK.SystemWeb) projects that brings back
ASP.NET Web Forms designer code for SDK-style projects.

Visual Studio only maintains `*.aspx.designer.cs` files for legacy Web Application Projects
([CZEMacLeod/MSBuild.SDK.SystemWeb#11](https://github.com/CZEMacLeod/MSBuild.SDK.SystemWeb/issues/11)).
This SDK replaces them with a Roslyn incremental source generator: the control fields and the typed `Master` / `PreviousPage`
properties are generated from the `.aspx`, `.ascx` and `.master` markup at compile time, and they update live in the IDE as you type.
No `*.designer.cs` files are needed any more, and existing ones keep working.

## How can I use this SDK?

Add it as a secondary SDK to a project that uses `MSBuild.SDK.SystemWeb`:

```xml
<Project Sdk="MSBuild.SDK.SystemWeb">
  <Sdk Name="KfbSoft.MSBuild.SDK.SystemWeb.WebForms" />
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
  </PropertyGroup>
</Project>
```

MSBuild SDKs obtained from NuGet need a version, either inline (`<Sdk Name="KfbSoft.MSBuild.SDK.SystemWeb.WebForms" Version="$version$" />`)
or in `global.json`:

```json
{
  "msbuild-sdks": {
    "MSBuild.SDK.SystemWeb": "4.0.106",
    "KfbSoft.MSBuild.SDK.SystemWeb.WebForms": "$version$"
  }
}
```

Then delete the `*.designer.cs` files (optional: any member they declare is simply not generated again), build, and use the controls from your code-behind.

## What gets generated

For every `.aspx`, `.ascx` and `.master` file that has an `Inherits` attribute, a `partial class` with:

- `protected` fields for every element with `runat="server"` and an `ID`, typed through the project's own compilation:
  `asp:` controls, controls registered with `<%@ Register %>` or Web.config `pages/controls`, `[assembly: TagPrefix]` registrations
  (for example `asp:ScriptManager` from System.Web.Extensions), user controls (typed as their code-behind class, or as
  `System.Web.UI.UserControl` for inline user controls without one), and HTML controls (`form`, `head`, `input type="..."`, `div`, ...).
  `<title>`, `<link>` and `<meta>` directly inside a server-side `<head>` are controls too, as in ASP.NET.
- Web.config registrations follow ASP.NET configuration inheritance: the root `Web.config` applies everywhere, a `Web.config` in a
  subfolder or a `<location path="...">` element only to the markup below that folder.
- IDs that are C# keywords are escaped (`ID="class"` becomes `@class`); nested code-behind classes get nested partial declarations.
- Controls inside multi-instance templates (`Repeater` / `GridView` `ItemTemplate`, `TemplateField`, ...) get no fields; controls inside
  single-instance templates (`UpdatePanel.ContentTemplate`, `WizardStep`, ...) do, exactly as Visual Studio does.
- A member that the code-behind (or a base class, or a legacy `.designer.cs`) already declares is skipped, so you can take control of a
  field by moving its declaration into the code-behind, as before.
- `<%@ MasterType %>` and `<%@ PreviousPageType %>` produce `public new T Master` / `PreviousPage` properties.
- VB projects get the VB shape Visual Studio produces: `Protected WithEvents` fields (so `Handles` clauses work), a
  `Public Shadows ReadOnly Property Master()`, `Option Strict On`, and a `Namespace` block relative to the project's root
  namespace, exactly as the code-behind declares it.

Generated files show up in Visual Studio under *Dependencies > Analyzers > MSBuild.SDK.SystemWeb.WebForms.Generator*. To also write
them to disk, set `EmitCompilerGeneratedFiles=true` in the project.

## Properties

| Property | Default value | Description |
| -------- | ------------- | ----------- |
| `EnableWebFormsDesignerGenerator` | true | Set to false to turn the generator off while keeping the SDK imported. |
| `MSBuildSDKSystemWebWebFormsGeneratorPath` | the DLL inside this package | Path of the generator assembly. Only useful when developing the generator itself. |

The markup files are taken from the `Content` items that `MSBuild.SDK.SystemWeb` creates (`EnableWebFormsDefaultItems`).
If you turned those off, add your markup files as `AdditionalFiles` yourself.

## Diagnostics

| Id | Severity | Meaning |
| -- | -------- | ------- |
| `SWWF001` | Warning | The type of a server control could not be resolved (prefix not registered, or type not in the referenced assemblies). No field is generated for it. |
| `SWWF002` | Warning | The page has `CodeBehind`/`CodeFile` but no `Inherits`; nothing is generated for it. |
| `SWWF003` | Warning / Info | A problem while parsing the markup (invalid identifier as `ID`, server-side include, parse timeout, malformed Web.config). |
| `SWWF004` | Warning | A master page or user control referenced by virtual path was not found among the project's markup files. A missing user control is typed as `System.Web.UI.UserControl`; a missing master page gets no typed `Master` property. |
| `SWWF005` | Info | The code-behind class named by `Inherits` is not in the compilation; the partial class is generated from the attribute anyway. |
| `SWWF006` | Info | The class named by `Inherits` lives in a referenced assembly, so no partial class can be generated for it. |
| `SWWF007` | Warning | A Web.config `pages/controls` registration has a `namespace` but no `assembly`. The generator resolves the type anyway, but at runtime ASP.NET only searches `App_Code` for such registrations, so the page fails with "Unknown server tag". Add `assembly="..."`. |

## Known limitations

- Server-side includes (`<!-- #include -->`) are not followed.
- Controls whose markup is interpreted by a custom `ControlBuilder` may be mapped differently from the runtime.
- `App_Code` is not part of the compilation in SDK-style projects, so controls declared there cannot be resolved.
- The generator needs Visual Studio 2022 17.8 or later (Roslyn 4.8).
