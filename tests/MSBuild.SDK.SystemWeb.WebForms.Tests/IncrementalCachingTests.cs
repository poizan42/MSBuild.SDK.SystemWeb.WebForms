using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MSBuild.SDK.SystemWeb.WebForms.Generator;
using MSBuild.SDK.SystemWeb.WebForms.Generator.Model;
using MSBuild.SDK.SystemWeb.WebForms.Tests.Infrastructure;

namespace MSBuild.SDK.SystemWeb.WebForms.Tests;

public class IncrementalCachingTests
{
    private const string Directive = "<%@ Page Language=\"C#\" CodeBehind=\"{0}.aspx.cs\" Inherits=\"WebApp.{0}\" %>";

    private static int Walks(Action action)
    {
        var before = DesignerModelBuilder.WalkCount;
        action();
        return DesignerModelBuilder.WalkCount - before;
    }

    [NetFx48Fact]
    public void Unchanged_documents_skip_the_walk_when_unrelated_source_changes()
    {
        DocumentCache.Clear();
        var host = new GeneratorTestHost()
            .WithMarkup("A.aspx", string.Format(Directive, "A") + "<asp:Label ID=\"a\" runat=\"server\" />")
            .WithMarkup("B.aspx", string.Format(Directive, "B") + "<asp:Label ID=\"b\" runat=\"server\" />")
            .WithSource("A.aspx.cs", "namespace WebApp { public partial class A : System.Web.UI.Page { } }")
            .WithSource("B.aspx.cs", "namespace WebApp { public partial class B : System.Web.UI.Page { } }");

        GeneratorTestResult first = null!;
        Assert.Equal(2, Walks(() => first = host.Run()));
        Assert.Equal(2, DocumentCache.Count);

        GeneratorTestResult second = null!;
        var unrelated = first.InputCompilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("namespace WebApp { public class Unrelated { } }"));
        Assert.Equal(0, Walks(() => second = first.RunAgain(unrelated)));
        Assert.Equal(2, second.HintNames.Count());
        DesignerAssert.HasField(second.GetSource("A.aspx.designer.g.cs"), "global::System.Web.UI.WebControls.Label", "a");
    }

    [NetFx48Fact]
    public void Editing_a_code_behind_re_walks_only_its_own_document()
    {
        DocumentCache.Clear();
        var codeBehindA = CSharpSyntaxTree.ParseText("namespace WebApp { public partial class A : System.Web.UI.Page { } }", path: GeneratorTestHost.ToFullPath("A.aspx.cs"));
        var host = new GeneratorTestHost()
            .WithMarkup("A.aspx", string.Format(Directive, "A") + "<asp:Label ID=\"a\" runat=\"server\" /><asp:Label ID=\"a2\" runat=\"server\" />")
            .WithMarkup("B.aspx", string.Format(Directive, "B") + "<asp:Label ID=\"b\" runat=\"server\" />")
            .WithSource("B.aspx.cs", "namespace WebApp { public partial class B : System.Web.UI.Page { } }");

        GeneratorTestResult first = null!;
        Assert.Equal(2, Walks(() => first = host.WithSyntaxTree(codeBehindA).Run()));
        DesignerAssert.HasField(first.GetSource("A.aspx.designer.g.cs"), "global::System.Web.UI.WebControls.Label", "a2");

        // The developer takes ownership of a2 by declaring it in the code-behind: A must be re-walked, B must not.
        var editedA = CSharpSyntaxTree.ParseText(
            "namespace WebApp { public partial class A : System.Web.UI.Page { protected System.Web.UI.WebControls.Label a2; } }",
            path: GeneratorTestHost.ToFullPath("A.aspx.cs"));
        GeneratorTestResult second = null!;
        Assert.Equal(1, Walks(() => second = first.RunAgain(first.InputCompilation.ReplaceSyntaxTree(codeBehindA, editedA))));
        var a = second.GetSource("A.aspx.designer.g.cs");
        DesignerAssert.HasField(a, "global::System.Web.UI.WebControls.Label", "a");
        DesignerAssert.HasNoField(a, "a2");
        DesignerAssert.Compiles(second);
    }

    [NetFx48Fact]
    public void Editing_a_source_control_type_re_walks_the_documents_that_use_it()
    {
        DocumentCache.Clear();
        var controls = CSharpSyntaxTree.ParseText(
            "namespace WebApp.Controls { public class Fancy : System.Web.UI.WebControls.WebControl { public System.Web.UI.ITemplate Body { get; set; } } }",
            path: GeneratorTestHost.ToFullPath("Controls.cs"));
        var host = new GeneratorTestHost()
            .WithMarkup("A.aspx", string.Format(Directive, "A") + "<%@ Register TagPrefix=\"my\" Namespace=\"WebApp.Controls\" %><my:Fancy ID=\"f\" runat=\"server\"><Body><asp:Label ID=\"inBody\" runat=\"server\" /></Body></my:Fancy>")
            .WithMarkup("B.aspx", string.Format(Directive, "B") + "<asp:Label ID=\"b\" runat=\"server\" />")
            .WithSource("Pages.cs", "namespace WebApp { public partial class A : System.Web.UI.Page { } public partial class B : System.Web.UI.Page { } }");

        GeneratorTestResult first = null!;
        Assert.Equal(2, Walks(() => first = host.WithSyntaxTree(controls).Run()));
        DesignerAssert.HasNoField(first.GetSource("A.aspx.designer.g.cs"), "inBody");

        // Making Body a single-instance template changes A's fields; B does not use the control and stays cached.
        var editedControls = CSharpSyntaxTree.ParseText(
            "namespace WebApp.Controls { public class Fancy : System.Web.UI.WebControls.WebControl { [System.Web.UI.TemplateInstance(System.Web.UI.TemplateInstance.Single)] public System.Web.UI.ITemplate Body { get; set; } } }",
            path: GeneratorTestHost.ToFullPath("Controls.cs"));
        GeneratorTestResult second = null!;
        Assert.Equal(1, Walks(() => second = first.RunAgain(first.InputCompilation.ReplaceSyntaxTree(controls, editedControls))));
        DesignerAssert.HasField(second.GetSource("A.aspx.designer.g.cs"), "global::System.Web.UI.WebControls.Label", "inBody");
    }

    [NetFx48Fact]
    public void Documents_with_unresolved_lookups_are_re_walked_until_they_resolve()
    {
        DocumentCache.Clear();
        var host = new GeneratorTestHost()
            .WithMarkup("A.aspx", string.Format(Directive, "A") + "<%@ Register TagPrefix=\"my\" Namespace=\"WebApp.Controls\" %><my:Later ID=\"later\" runat=\"server\" />")
            .WithSource("A.aspx.cs", "namespace WebApp { public partial class A : System.Web.UI.Page { } }");

        GeneratorTestResult first = null!;
        Assert.Equal(1, Walks(() => first = host.Run()));
        DesignerAssert.HasDiagnostic(first, "SWWF001");
        Assert.Equal(0, DocumentCache.Count);

        // Adding the missing control type in a brand-new file must be picked up even though no existing tree changed.
        var withControl = first.InputCompilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("namespace WebApp.Controls { public class Later : System.Web.UI.WebControls.Label { } }"));
        GeneratorTestResult second = null!;
        Assert.Equal(1, Walks(() => second = first.RunAgain(withControl)));
        DesignerAssert.NoDiagnostics(second);
        DesignerAssert.HasField(second.GetSource("A.aspx.designer.g.cs"), "global::WebApp.Controls.Later", "later");
        Assert.Equal(1, DocumentCache.Count);
    }

    [NetFx48Fact]
    public void Changing_references_invalidates_cached_documents()
    {
        DocumentCache.Clear();
        var host = new GeneratorTestHost()
            .WithMarkup("A.aspx", string.Format(Directive, "A") + "<asp:UpdatePanel ID=\"up\" runat=\"server\" />")
            .WithSource("A.aspx.cs", "namespace WebApp { public partial class A : System.Web.UI.Page { } }");

        GeneratorTestResult first = null!;
        Assert.Equal(1, Walks(() => first = host.Run()));
        DesignerAssert.HasField(first.GetSource("A.aspx.designer.g.cs"), "global::System.Web.UI.UpdatePanel", "up");

        var extensions = first.InputCompilation.References.Single(r => (r.Display ?? string.Empty).EndsWith("System.Web.Extensions.dll", StringComparison.OrdinalIgnoreCase));
        GeneratorTestResult second = null!;
        Assert.Equal(1, Walks(() => second = first.RunAgain(first.InputCompilation.RemoveReferences(extensions))));
        DesignerAssert.HasDiagnostic(second, "SWWF001");
        DesignerAssert.HasNoField(second.GetSource("A.aspx.designer.g.cs"), "up");
    }

    [NetFx48Fact]
    public void Markup_parsing_is_cached_when_only_source_changes()
    {
        var first = new GeneratorTestHost()
            .WithMarkup("A.aspx", string.Format(Directive, "A") + "<asp:Label ID=\"a\" runat=\"server\" />")
            .WithMarkup("B.aspx", string.Format(Directive, "B") + "<asp:Label ID=\"b\" runat=\"server\" />")
            .WithSource("Pages.cs", "namespace WebApp { public partial class A : System.Web.UI.Page { } public partial class B : System.Web.UI.Page { } }")
            .Run();

        Assert.All(first.RunResult.TrackedSteps["Markup"].SelectMany(s => s.Outputs), o => Assert.Equal(IncrementalStepRunReason.New, o.Reason));

        var changedCompilation = first.InputCompilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("namespace WebApp { public class Unrelated { } }"));
        var second = first.RunAgain(changedCompilation);

        Assert.All(
            second.RunResult.TrackedSteps["Markup"].SelectMany(s => s.Outputs),
            o => Assert.True(o.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged, $"Markup step re-ran: {o.Reason}"));
        Assert.All(
            second.RunResult.TrackedSteps["Index"].SelectMany(s => s.Outputs),
            o => Assert.True(o.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged, $"Index step re-ran: {o.Reason}"));
        Assert.Equal(2, second.HintNames.Count());
    }

    [NetFx48Fact]
    public void Editing_one_markup_file_reparses_only_that_file()
    {
        var a = GeneratorTestHost.CreateMarkup("A.aspx", string.Format(Directive, "A") + "<asp:Label ID=\"a\" runat=\"server\" />");
        var host = new GeneratorTestHost()
            .WithMarkup("B.aspx", string.Format(Directive, "B") + "<asp:Label ID=\"b\" runat=\"server\" />")
            .WithSource("Pages.cs", "namespace WebApp { public partial class A : System.Web.UI.Page { } public partial class B : System.Web.UI.Page { } }");

        // Add A through the host so the driver knows the exact AdditionalText instance we replace later.
        var first = host.WithMarkupText(a).Run();
        var edited = GeneratorTestHost.CreateMarkup("A.aspx", string.Format(Directive, "A") + "<asp:Label ID=\"a\" runat=\"server\" /><asp:TextBox ID=\"a2\" runat=\"server\" />");

        var second = first.RunAgain(replace: a, with: edited);

        var reasons = second.RunResult.TrackedSteps["Markup"].SelectMany(s => s.Outputs).Select(o => o.Reason).ToList();
        Assert.Single(reasons, r => r == IncrementalStepRunReason.Modified);
        Assert.Single(reasons, r => r is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged);
        DesignerAssert.HasField(second.GetSource("A.aspx.designer.g.cs"), "global::System.Web.UI.WebControls.TextBox", "a2");
    }
}
