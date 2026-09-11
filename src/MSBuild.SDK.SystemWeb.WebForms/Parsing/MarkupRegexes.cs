using System;
using System.Text.RegularExpressions;

namespace MSBuild.SDK.SystemWeb.WebForms.Generator.Parsing
{
    /// <summary>
    /// The regular expressions ASP.NET's own <c>System.Web.UI.BaseParser</c> uses to tokenize markup
    /// (see <c>System.Web.RegularExpressions</c>), so that this generator sees the same tags ASP.NET does.
    /// All patterns are anchored with <c>\G</c> and must be matched at an explicit position.
    /// </summary>
    internal static class MarkupRegexes
    {
        private const RegexOptions Options = RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.CultureInvariant;

        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

        public static readonly Regex Tag = new(
            @"\G<(?<tagname>[\w:\.]+)(\s+(?<attrname>\w[-\w:]*)(\s*=\s*""(?<attrval>[^""]*)""|\s*=\s*'(?<attrval>[^']*)'|\s*=\s*(?<attrval><%#.*?%>)|\s*=\s*(?<attrval>[^\s=""'/>]*)|(?<attrval>\s*?)))*\s*(?<empty>/)?>",
            Options,
            Timeout);

        public static readonly Regex Directive = new(
            @"\G<%\s*@(\s*(?<attrname>\w[\w:]*(?=\W))(\s*(?<equal>=)\s*""(?<attrval>[^""]*)""|\s*(?<equal>=)\s*'(?<attrval>[^']*)'|\s*(?<equal>=)\s*(?<attrval>[^\s""'%>]*)|(?<equal>)(?<attrval>\s*?)))*\s*?%>",
            Options,
            Timeout);

        public static readonly Regex EndTag = new(@"\G</(?<tagname>[\w:\.]+)\s*>", Options, Timeout);

        public static readonly Regex AspCode = new(@"\G<%(?!@)(?<code>.*?)%>", Options, Timeout);

        public static readonly Regex AspExpr = new(@"\G<%\s*?=(?<code>.*?)?%>", Options, Timeout);

        public static readonly Regex AspEncodedExpr = new(@"\G<%:(?<code>.*?)?%>", Options, Timeout);

        public static readonly Regex DatabindExpr = new(@"\G<%#(?<encode>:)?(?<code>.*?)?%>", Options, Timeout);

        public static readonly Regex Comment = new(@"\G<%--(([^-]*)-)*?-%>", Options, Timeout);

        public static readonly Regex Include = new(
            @"\G<!--\s*#(?i:include)\s*(?<pathtype>[\w]+)\s*=\s*[""']?(?<filename>[^\""']*?)[""']?\s*-->",
            Options,
            Timeout);

        public static readonly Regex Text = new(@"\G[^<]+", Options, Timeout);

        public static readonly Regex RunatServer = new(@"runat\W*server", Options | RegexOptions.IgnoreCase, Timeout);
    }
}
