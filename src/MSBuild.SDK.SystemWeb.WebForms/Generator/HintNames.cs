using System.Text;
using Microsoft.CodeAnalysis;

namespace MSBuild.SDK.SystemWeb.WebForms.Generator
{
    public static class HintNames
    {
        /// <summary>
        /// <c>Controls/Hello.ascx</c> becomes <c>Controls_Hello.ascx.designer.g.cs</c> (or <c>.g.vb</c>).
        /// Hint names must be unique per generator and may only contain file-name-safe characters.
        /// </summary>
        public static string ForMarkup(string relativePath, string language = LanguageNames.CSharp)
        {
            var builder = new StringBuilder(relativePath.Length + 16);
            foreach (var c in relativePath)
            {
                builder.Append(char.IsLetterOrDigit(c) || c == '.' || c == '-' ? c : '_');
            }

            builder.Append(language == LanguageNames.VisualBasic ? ".designer.g.vb" : ".designer.g.cs");
            return builder.ToString();
        }
    }
}
