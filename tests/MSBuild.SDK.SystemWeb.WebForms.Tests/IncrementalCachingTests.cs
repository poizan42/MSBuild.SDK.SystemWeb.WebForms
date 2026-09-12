using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MSBuild.SDK.SystemWeb.WebForms.Tests.Infrastructure;

namespace MSBuild.SDK.SystemWeb.WebForms.Tests;

public class IncrementalCachingTests
{
    private const string Directive = "<%@ Page Language=\"C#\" CodeBehind=\"{0}.aspx.cs\" Inherits=\"WebApp.{0}\" %>";

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
