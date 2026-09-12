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

        Assert.Empty(config.Problems);
        Assert.Equal(2, config.Registrations.Count);
        Assert.All(config.Registrations, r => Assert.Equal(string.Empty, r.DirectoryPrefix));
        Assert.True(config.Registrations[0].Registration.IsNamespace);
        Assert.Equal("WebApp.Controls", config.Registrations[0].Registration.Namespace);
        Assert.True(config.Registrations[1].Registration.IsUserControl);
        Assert.Equal("~/Controls/Hello.ascx", config.Registrations[1].Registration.Src);
    }

    [Fact]
    public void Location_elements_and_subfolder_configs_scope_registrations()
    {
        var root = WebConfigParser.Parse(
            "<configuration>" +
            "<system.web><pages><controls><add tagPrefix=\"a\" namespace=\"A\" /></controls></pages></system.web>" +
            "<location path=\"Admin\"><system.web><pages><controls><add tagPrefix=\"b\" namespace=\"B\" /></controls></pages></system.web></location>" +
            "</configuration>");
        var sub = WebConfigParser.Parse(
            "<configuration><system.web><pages><controls><add tagPrefix=\"c\" namespace=\"C\" /></controls></pages></system.web></configuration>",
            @"C:\fake\WebApp\Admin\Reports\Web.config",
            "Admin/Reports");

        var merged = WebConfigRegistrations.Merge([root, sub]);
        Assert.Equal(["", "Admin/", "Admin/Reports/"], merged.Registrations.Select(r => r.DirectoryPrefix));

        Assert.Equal(["a"], merged.ForDocument("Default.aspx").Select(r => r.TagPrefix));
        Assert.Equal(["b", "a"], merged.ForDocument("Admin/Users.aspx").Select(r => r.TagPrefix));
        Assert.Equal(["c", "b", "a"], merged.ForDocument(@"Admin\Reports\Sales.aspx").Select(r => r.TagPrefix));
        Assert.Equal(["a"], merged.ForDocument("Administration/Page.aspx").Select(r => r.TagPrefix));
    }

    [Fact]
    public void Missing_section_yields_no_registrations()
    {
        var config = WebConfigParser.Parse("<configuration><system.web /></configuration>");
        Assert.Empty(config.Registrations);
        Assert.Empty(config.Problems);
    }

    [Fact]
    public void Malformed_xml_is_reported_not_thrown()
    {
        var config = WebConfigParser.Parse("<configuration><system.web>", @"C:\fake\WebApp\Web.config");
        Assert.Empty(config.Registrations);
        var problem = Assert.Single(config.Problems);
        Assert.EndsWith("Web.config", problem.Path);
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
    [InlineData("title", null, "HtmlGenericControl")]
    [InlineData("meta", null, "HtmlGenericControl")]
    public void Maps_html_tags_to_control_types(string tag, string? type, string expected)
    {
        Assert.Equal(expected, HtmlControlTypeMap.GetTypeName(tag, type));
        Assert.Equal("System.Web.UI.HtmlControls." + expected, HtmlControlTypeMap.GetMetadataName(tag, type));
    }

    [Theory]
    [InlineData("title", "System.Web.UI.HtmlControls.HtmlTitle")]
    [InlineData("LINK", "System.Web.UI.HtmlControls.HtmlLink")]
    [InlineData("meta", "System.Web.UI.HtmlControls.HtmlMeta")]
    [InlineData("script", null)]
    public void Maps_children_of_a_server_side_head(string tag, string? expected)
    {
        Assert.Equal(expected, HtmlControlTypeMap.GetHeadChildMetadataName(tag));
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
