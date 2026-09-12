using System;
using System.Collections.Generic;

namespace MSBuild.SDK.SystemWeb.WebForms.Generator.Resolution
{
    /// <summary>
    /// Maps HTML elements marked <c>runat="server"</c> to their <c>System.Web.UI.HtmlControls</c> type,
    /// following <c>System.Web.UI.HtmlTagNameToTypeMapper</c>.
    /// </summary>
    public static class HtmlControlTypeMap
    {
        public const string Namespace = "System.Web.UI.HtmlControls";

        private static readonly Dictionary<string, string> Tags = new(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = "HtmlAnchor",
            ["button"] = "HtmlButton",
            ["form"] = "HtmlForm",
            ["head"] = "HtmlHead",
            ["img"] = "HtmlImage",
            ["textarea"] = "HtmlTextArea",
            ["select"] = "HtmlSelect",
            ["table"] = "HtmlTable",
            ["tr"] = "HtmlTableRow",
            ["td"] = "HtmlTableCell",
            ["th"] = "HtmlTableCell",
            ["audio"] = "HtmlAudio",
            ["video"] = "HtmlVideo",
            ["track"] = "HtmlTrack",
            ["source"] = "HtmlSource",
            ["iframe"] = "HtmlIframe",
            ["embed"] = "HtmlEmbed",
            ["area"] = "HtmlArea",
            ["html"] = "HtmlElement",
        };

        /// <summary>Children that a server-side <c>&lt;head&gt;</c> turns into controls even without <c>runat="server"</c> (see <c>HtmlHeadBuilder</c>).</summary>
        private static readonly Dictionary<string, string> HeadChildren = new(StringComparer.OrdinalIgnoreCase)
        {
            ["title"] = "HtmlTitle",
            ["link"] = "HtmlLink",
            ["meta"] = "HtmlMeta",
        };

        private static readonly Dictionary<string, string> InputTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["text"] = "HtmlInputText",
            ["password"] = "HtmlInputPassword",
            ["button"] = "HtmlInputButton",
            ["submit"] = "HtmlInputSubmit",
            ["reset"] = "HtmlInputReset",
            ["image"] = "HtmlInputImage",
            ["checkbox"] = "HtmlInputCheckBox",
            ["radio"] = "HtmlInputRadioButton",
            ["hidden"] = "HtmlInputHidden",
            ["file"] = "HtmlInputFile",
        };

        /// <summary>Returns the simple type name (without namespace) of the control for an HTML tag.</summary>
        public static string GetTypeName(string tagName, string? inputType)
        {
            if (string.Equals(tagName, "input", StringComparison.OrdinalIgnoreCase))
            {
                var type = string.IsNullOrEmpty(inputType) ? "text" : inputType!.Trim();
                return InputTypes.TryGetValue(type, out var inputControl) ? inputControl : "HtmlInputGenericControl";
            }

            return Tags.TryGetValue(tagName, out var control) ? control : "HtmlGenericControl";
        }

        public static string GetMetadataName(string tagName, string? inputType) => Namespace + "." + GetTypeName(tagName, inputType);

        /// <summary>The control type for a direct child of a server-side <c>&lt;head&gt;</c>, or null if the child is ordinary markup.</summary>
        public static string? GetHeadChildMetadataName(string tagName)
        {
            return HeadChildren.TryGetValue(tagName, out var control) ? Namespace + "." + control : null;
        }
    }
}
