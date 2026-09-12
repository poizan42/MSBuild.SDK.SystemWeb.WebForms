using MSBuild.SDK.SystemWeb.WebForms.Generator.Parsing;

namespace MSBuild.SDK.SystemWeb.WebForms.Tests;

public class MarkupParserTests
{
    private static MarkupDocument Parse(string markup, MarkupKind kind = MarkupKind.Page)
        => MarkupParser.Parse(@"C:\fake\WebApp\Default.aspx", "Default.aspx", markup, kind);

    private static IEnumerable<MarkupElement> Flatten(MarkupElement element)
    {
        foreach (var child in element.Children)
        {
            yield return child;
            foreach (var descendant in Flatten(child))
            {
                yield return descendant;
            }
        }
    }

    [Fact]
    public void Parses_named_directive_with_quoted_and_unquoted_attributes()
    {
        var doc = Parse("<%@ Page Language=\"C#\" AutoEventWireup='true' CodeBehind=Default.aspx.cs Inherits=\"WebApp._Default\" %>");

        var directive = Assert.Single(doc.Directives);
        Assert.Equal("Page", directive.Name);
        Assert.Equal("C#", directive.Get("language"));
        Assert.Equal("true", directive.Get("AutoEventWireup"));
        Assert.Equal("Default.aspx.cs", directive.Get("CodeBehind"));
        Assert.Equal("WebApp._Default", directive.Get("Inherits"));
        Assert.Same(directive, doc.MainDirective);
    }

    [Fact]
    public void Unnamed_directive_defaults_to_the_file_kind()
    {
        Assert.Equal("Page", Parse("<%@ Language=\"C#\" %>").Directives[0].Name);
        Assert.Equal("Control", Parse("<%@ Language=\"C#\" %>", MarkupKind.Control).Directives[0].Name);
        Assert.Equal("Master", Parse("<%@ Language=\"C#\" %>", MarkupKind.Master).Directives[0].Name);
    }

    [Fact]
    public void Builds_element_tree_with_prefix_id_and_runat()
    {
        var doc = Parse("<html><body><form id=\"form1\" runat=\"server\"><asp:Label ID=\"lbl\" runat=\"server\" Text=\"x\" /></form></body></html>");

        var html = Assert.Single(doc.Root.Children);
        var body = Assert.Single(html.Children);
        var form = Assert.Single(body.Children);
        Assert.Equal("form", form.LocalName);
        Assert.Null(form.Prefix);
        Assert.True(form.RunAtServer);
        Assert.Equal("form1", form.Id);

        var label = Assert.Single(form.Children);
        Assert.Equal("asp", label.Prefix);
        Assert.Equal("Label", label.LocalName);
        Assert.Equal("asp:Label", label.RawName);
        Assert.Equal("lbl", label.Id);
        Assert.Equal("x", label.GetAttribute("text"));
        Assert.Empty(label.Children);
    }

    [Fact]
    public void Void_html_elements_do_not_swallow_siblings()
    {
        var doc = Parse("<div><input type=\"text\" id=\"a\" runat=\"server\"><br><asp:Label ID=\"b\" runat=\"server\" /></div>");

        var div = Assert.Single(doc.Root.Children);
        Assert.Equal(new[] { "input", "br", "asp:Label" }, div.Children.Select(c => c.RawName));
    }

    [Fact]
    public void Stray_end_tags_are_ignored_and_unclosed_elements_close_with_their_parent()
    {
        var doc = Parse("<div><span></p><asp:Label ID=\"a\" runat=\"server\" /></div><asp:Label ID=\"b\" runat=\"server\" />");

        var ids = Flatten(doc.Root).Where(e => e.Id is not null).Select(e => e.Id).ToArray();
        Assert.Equal(new[] { "a", "b" }, ids);
        Assert.Equal(2, doc.Root.Children.Count);
    }

    [Fact]
    public void Server_side_script_blocks_are_skipped()
    {
        var doc = Parse("<script runat=\"server\">string s = \"<asp:Label ID='ghost' runat='server' />\";</script><asp:Label ID=\"real\" runat=\"server\" />");

        var ids = Flatten(doc.Root).Where(e => e.Id is not null).Select(e => e.Id).ToArray();
        Assert.Equal(new[] { "real" }, ids);
    }

    [Fact]
    public void Server_comments_and_code_blocks_are_skipped_but_html_comments_are_not()
    {
        var doc = Parse(
            "<%-- <asp:Label ID=\"commented\" runat=\"server\" /> --%>" +
            "<% if (true) { %><asp:Label ID=\"inCode\" runat=\"server\" /><% } %>" +
            "<%= \"<asp:Label ID='expr' runat='server' />\" %>" +
            "<!-- <asp:Label ID=\"inHtmlComment\" runat=\"server\" /> -->");

        var ids = Flatten(doc.Root).Where(e => e.Id is not null).Select(e => e.Id).ToArray();
        Assert.Equal(new[] { "inCode", "inHtmlComment" }, ids);
    }

    [Fact]
    public void Databinding_attribute_values_are_kept_intact()
    {
        var doc = Parse("<asp:Label ID=\"a\" runat=\"server\" Text='<%# Eval(\"Name\") %>' />");

        var label = Assert.Single(doc.Root.Children);
        Assert.Equal("<%# Eval(\"Name\") %>", label.GetAttribute("Text"));
        Assert.Equal("a", label.Id);
    }

    [Fact]
    public void Doctype_and_processing_instructions_do_not_break_parsing()
    {
        var doc = Parse("<?xml version=\"1.0\"?><!DOCTYPE html><html><asp:Label ID=\"a\" runat=\"server\" /></html>");

        Assert.Equal("a", Flatten(doc.Root).Single(e => e.Id is not null).Id);
        Assert.Empty(doc.Problems);
    }

    [Fact]
    public void Server_side_include_is_reported_as_informational_problem()
    {
        var doc = Parse("<!-- #include file=\"header.inc\" --><asp:Label ID=\"a\" runat=\"server\" />");

        var problem = Assert.Single(doc.Problems);
        Assert.True(problem.IsInformational);
        Assert.Contains("header.inc", problem.Message);
    }

    [Fact]
    public void Line_positions_are_computed_from_offsets()
    {
        var doc = Parse("line1\r\nline2\n<asp:Label ID=\"a\" runat=\"server\" />");

        var label = Assert.Single(doc.Root.Children);
        var position = doc.GetLinePosition(label.Position);
        Assert.Equal(2, position.Line);
        Assert.Equal(0, position.Character);
    }

    [Fact]
    public void Parsed_documents_are_structurally_equal()
    {
        const string markup = "<%@ Page Inherits=\"A\" %><asp:Label ID=\"a\" runat=\"server\" />";

        Assert.Equal(Parse(markup), Parse(markup));
        Assert.NotEqual(Parse(markup), Parse(markup + "<asp:Label ID=\"b\" runat=\"server\" />"));
    }
}
