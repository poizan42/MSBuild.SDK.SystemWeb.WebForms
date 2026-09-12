using System.Text;

namespace MSBuild.SDK.SystemWeb.WebForms.Generator
{
    public static class HintNames
    {
        /// <summary>
        /// <c>Controls/Hello.ascx</c> becomes <c>Controls_Hello.ascx.designer.g.cs</c>.
        /// Hint names must be unique per generator and may only contain file-name-safe characters.
        /// </summary>
        public static string ForMarkup(string relativePath)
        {
            var builder = new StringBuilder(relativePath.Length + 16);
            foreach (var c in relativePath)
            {
                builder.Append(char.IsLetterOrDigit(c) || c == '.' || c == '-' ? c : '_');
            }

            builder.Append(".designer.g.cs");
            return builder.ToString();
        }
    }
}
