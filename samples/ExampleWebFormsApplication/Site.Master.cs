using System;

namespace ExampleWebFormsApplication
{
    public partial class SiteMaster : System.Web.UI.MasterPage
    {
        /// <summary>Exposed to content pages through the typed <c>Master</c> property that the generator creates from <c>&lt;%@ MasterType %&gt;</c>.</summary>
        public string Heading
        {
            get { return lblHeading.Text; }
            set { lblHeading.Text = value; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            litFooter.Text = "Rendered " + DateTime.Now.ToString("u");
            Head1.Title = Heading;
        }
    }
}
