using MSBuild.SDK.SystemWeb.WebForms.Generator.Parsing;
using MSBuild.SDK.SystemWeb.WebForms.Generator.Resolution;

namespace MSBuild.SDK.SystemWeb.WebForms.Tests;

public class WebConfigParserTests
{
    [Fact]
    public void Reads_namespace_and_user_control_registrations()
    {
        var config = WebConfigParser.Parse(
            "<configuration><system.web><pages><controls>" +
            "<add tagPrefix=\"ex\" namespace=\"WebApp.Controls\" assembly=\"WebApp\" />" +
            "<add tagPrefix=\"uc\" tagName=\"Hello\" src=\"~/Controls/Hello.ascx\" />" +
            "<add namespace=\"Ignored.NoPrefix\" />" +
            "</controls></pages></system.web></configuration>");

        Assert.Null(config.Problem);
        Assert.Equal(2, config.Registrations.Count);
        Assert.True(config.Registrations[0].IsNamespace);
        Assert.Equal("WebApp.Controls", config.Registrations[0].Namespace);
        Assert.True(config.Registrations[1].IsUserControl);
        Assert.Equal("~/Controls/Hello.ascx", config.Registrations[1].Src);
    }

    [Fact]
    public void Missing_section_yields_no_registrations()
    {
        var config = WebConfigParser.Parse("<configuration><system.web /></configuration>");
        Assert.Empty(config.Registrations);
        Assert.Null(config.Problem);
    }

    [Fact]
    public void Malformed_xml_is_reported_not_thrown()
    {
        var config = WebConfigParser.Parse("<configuration><system.web>");
        Assert.Empty(config.Registrations);
        Assert.NotNull(config.Problem);
    }
}

public class HtmlControlTypeMapTests
{
    [Theory]
    [InlineData("form", null, "HtmlForm")]
    [InlineData("FORM", null, "HtmlForm")]
    [InlineData("head", null, "HtmlHead")]
    [InlineData("td", null, "HtmlTableCell")]
    [InlineData("th", null, "HtmlTableCell")]
    [InlineData("div", null, "HtmlGenericControl")]
    [InlineData("input", null, "HtmlInputText")]
    [InlineData("input", "Password", "HtmlInputPassword")]
    [InlineData("input", "checkbox", "HtmlInputCheckBox")]
    [InlineData("input", "file", "HtmlInputFile")]
    [InlineData("input", "range", "HtmlInputGenericControl")]
    public void Maps_html_tags_to_control_types(string tag, string? type, string expected)
    {
        Assert.Equal(expected, HtmlControlTypeMap.GetTypeName(tag, type));
        Assert.Equal("System.Web.UI.HtmlControls." + expected, HtmlControlTypeMap.GetMetadataName(tag, type));
    }
}

public class VirtualPathResolverTests
{
    [Theory]
    [InlineData("~/Site.Master", "Pages/Default.aspx", "Site.Master")]
    [InlineData("/Controls/Hello.ascx", "Pages/Default.aspx", "Controls/Hello.ascx")]
    [InlineData("Hello.ascx", "Controls/Default.aspx", "Controls/Hello.ascx")]
    [InlineData("../Shared/Hello.ascx", "Pages/Default.aspx", "Shared/Hello.ascx")]
    [InlineData("~\\Controls\\Hello.ascx", "Default.aspx", "Controls/Hello.ascx")]
    public void Resolves_virtual_paths_relative_to_the_project(string virtualPath, string referencing, string expected)
    {
        Assert.Equal(expected, VirtualPathResolver.Resolve(virtualPath, referencing));
    }
}
