# Third-party notices

This project is licensed under the MIT License (see `LICENSE.txt`). It contains material derived from the
following third-party projects, reproduced here with their original notices.

## MSBuild.SDK.SystemWeb

<https://github.com/CZEMacLeod/MSBuild.SDK.SystemWeb>

The following parts of this repository are derived from MSBuild.SDK.SystemWeb:

- `Directory.Build.targets`: the `UpdateFiles` target that replaces the `$version$` token in packed files, the item group that
  packs `Sdk\**`, the README and the license into MSBuild SDK packages, and the Nerdbank.GitVersioning setup.
- `samples/Nuget.config`: the local-feed configuration used by the sample projects.

The secondary-SDK layering (`<Sdk Name="..." />` on top of `MSBuild.SDK.SystemWeb`) follows the approach of
`MSBuild.SDK.SystemWeb.RazorLibrary` from the same project.

```
MIT License

Copyright (c) Cloud3D Ltd. All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## .NET Framework reference source (System.Web)

<https://github.com/microsoft/referencesource>

The markup tokenizer in `src/MSBuild.SDK.SystemWeb.WebForms/Parsing/MarkupRegexes.cs` uses the regular expression patterns of
ASP.NET's `System.Web.UI.BaseParser` (`System.Web.RegularExpressions`), so that this generator recognizes the same tags ASP.NET does.
The HTML element to control type mapping in `Resolution/HtmlControlTypeMap.cs` mirrors `System.Web.UI.HtmlTagNameToTypeMapper`.

```
The MIT License (MIT)

Copyright (c) Microsoft Corporation

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
