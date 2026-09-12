using System;

namespace ExampleWebFormsApplication.Controls
{
    /// <summary>A custom server control registered through Web.config (pages/controls) rather than an @Register directive.</summary>
    public class GreetingLabel : System.Web.UI.WebControls.Label
    {
        protected override void OnPreRender(EventArgs e)
        {
            if (string.IsNullOrEmpty(Text))
            {
                Text = "Hello from GreetingLabel";
            }

            base.OnPreRender(e);
        }
    }
}
