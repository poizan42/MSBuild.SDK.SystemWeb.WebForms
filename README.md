# KfbSoft.MSBuild.SDK.SystemWeb.WebForms

A secondary MSBuild SDK for [MSBuild.SDK.SystemWeb](https://github.com/CZEMacLeod/MSBuild.SDK.SystemWeb) projects that generates
ASP.NET Web Forms designer code (`*.designer.cs` equivalents) with a Roslyn incremental source generator.

Visual Studio only maintains designer files for legacy Web Application Projects, so SDK-style Web Forms projects were stuck with
hand-maintained designer files ([CZEMacLeod/MSBuild.SDK.SystemWeb#11](https://github.com/CZEMacLeod/MSBuild.SDK.SystemWeb/issues/11)).
This SDK generates the control fields and typed `Master` / `PreviousPage` properties from the markup at compile time instead, in
Visual Studio, Rider and command-line builds alike.

```xml
<Project Sdk="MSBuild.SDK.SystemWeb">
  <Sdk Name="KfbSoft.MSBuild.SDK.SystemWeb.WebForms" />
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
  </PropertyGroup>
</Project>
```

See the [package README](src/MSBuild.SDK.SystemWeb.WebForms/README.md) for usage, properties, diagnostics and known limitations.

## Status

Prototype. C# and VB projects are supported. Validated against several hundred pages of existing Web Application Projects:
the generated designers matched Visual Studio's checked-in `*.designer.cs` files wherever those were up to date.

## Repository layout

| Path | Contents |
| ---- | -------- |
| `src/MSBuild.SDK.SystemWeb.WebForms` | The generator (`netstandard2.0`, Roslyn 4.8) and the SDK package. `Sdk/` holds the props/targets that register the generator as an analyzer and hand the markup files to the compiler; the built DLL is packed to `analyzers/dotnet/cs`. |
| `tests/MSBuild.SDK.SystemWeb.WebForms.Tests` | xunit tests. Parser tests are self-contained; generator tests run the generator over an in-memory project compiled against the .NET Framework 4.8 reference assemblies and check that the generated code compiles against the real `System.Web`. |
| `samples/ExampleWebFormsApplication` | A Web Forms application (master page, content page, user control, Web.config registrations, templates) with no designer files. It imports the SDK files from the source tree and uses the generator project as an analyzer, so it always exercises the current code. |
| `samples/ExampleWebFormsApplicationVB` | The VB counterpart: master page, content page with `Handles` clauses on generated `WithEvents` fields. |
| `tools/DesignerCompare` | Runs the generator over an existing C# or VB Web Forms project and compares the result, field by field, with the `*.designer.cs` / `*.designer.vb` files Visual Studio maintained. See below. |

## How it works

1. `Sdk/MSBuild.SDK.SystemWeb.WebForms.targets` adds the `.aspx`/`.ascx`/`.master` `Content` items (created by `MSBuild.SDK.SystemWeb`) and `Web.config` as `AdditionalFiles`, and registers the generator assembly as an `Analyzer`.
2. The generator parses each markup file with the same regular expressions ASP.NET's `BaseParser` uses, builds an element tree, and resolves every `runat="server"` tag through the project's `Compilation`: `<%@ Register %>` directives, `Web.config` `pages/controls`, `[assembly: TagPrefix]` attributes on referenced assemblies, and the built-in `asp` prefix.
3. Fields are emitted for controls with an `ID`, skipping controls inside multi-instance templates and members the code-behind already declares, in the same layout Visual Studio produces.
4. Each document's output is cached together with the metadata references and the syntax trees that declare the symbols it consulted. On the next edit only documents whose dependencies changed are walked again; the rest are served from the cache (300 pages: about 60 ms per edit instead of 300 ms).

## Building

```bash
dotnet build src/MSBuild.SDK.SystemWeb.WebForms
```

```bash
dotnet test tests/MSBuild.SDK.SystemWeb.WebForms.Tests
```

The sample needs Visual Studio's MSBuild because `MSBuild.SDK.SystemWeb` imports `Microsoft.WebApplication.targets`:

```bash
msbuild -restore samples/ExampleWebFormsApplication/ExampleWebFormsApplication.csproj
```

The generated designer code is written to `samples/ExampleWebFormsApplication/obj/GeneratedFiles`.

Building the generator project also produces the SDK package in `packages/`. To try the packaged SDK, point a `nuget.config` at that folder
and reference `<Sdk Name="KfbSoft.MSBuild.SDK.SystemWeb.WebForms" Version="..." />` from a project; the NuGet SDK resolver caches SDK packages in
`%USERPROFILE%\.nuget\packages`, so bump the version (or delete the cached folder) when iterating.

Note for generator development: Visual Studio does not reload a rebuilt generator assembly until it is restarted; command-line builds
always pick up the new build.

## Checking parity against an existing project

`tools/DesignerCompare` is the parity test: point it at a Web Application Project (or a project-less Web Site) and it compiles the
project's sources (code-behind, `App_Code`, every DLL in `bin\` except the project's own output) against the .NET Framework 4.8 reference
assemblies, runs the generator, and compares each generated designer with the checked-in `*.designer.cs` file, reporting missing fields,
extra fields and type mismatches per page, plus all generator diagnostics.

```bash
dotnet run --project tools/DesignerCompare -- "C:\path\to\WebApplication" --out report.md
```

The project's own designer files are excluded from the compilation so the generator produces complete designers; their content is the
expected result. Web Sites have no designer files, so for them the tool only reports diagnostics and pages without output.
The language is taken from the project file (`.csproj` / `.vbproj`; for VB the `RootNamespace`, `OptionStrict` and project-level
`Import`s are honoured), or from the majority of source files for a Web Site.
Options: `--language cs|vb`, `--root-namespace`, `--exclude-ref <name>` (repeatable), `--exclude-dir <name>` (repeatable, e.g. a `backup`
folder that duplicates the project), `--refs <reference assemblies dir>`, `--show-all`.
