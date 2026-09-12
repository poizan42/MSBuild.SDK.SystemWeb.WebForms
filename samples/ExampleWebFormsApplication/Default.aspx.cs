using System;

namespace ExampleWebFormsApplication
{
    public partial class _Default : System.Web.UI.Page
    {
        // Declared here on purpose: the generator sees the member exists and does not generate a field for btnGo.
        protected System.Web.UI.WebControls.Button btnGo;

        protected void Page_Load(object sender, EventArgs e)
        {
            // All of these members come from the generated designer code (obj\GeneratedFiles\...\Default.aspx.designer.g.cs).
            Master.Heading = "Example Web Forms Application (SDK-style)";
            lblTime.Text = DateTime.Now.ToLongTimeString();
            hello1.Greeting = "Hello from a user control";
            greeting.Text = "Hello from a control registered in Web.config";

            if (!IsPostBack)
            {
                lblMessage.Text = "Enter your name and press the button.";
                var items = new[] { "alpha", "beta", "gamma" };
                rptItems.DataSource = items;
                rptItems.DataBind();
                gvItems.DataSource = items;
                gvItems.DataBind();
                divHtml.Attributes["title"] = "HTML controls get fields too: " + txtHtml.ClientID + ", " + chkHtml.ClientID;
            }
        }

        protected void btnGo_Click(object sender, EventArgs e)
        {
            if (Page.IsValid)
            {
                lblMessage.Text = "Hello, " + Server.HtmlEncode(txtName.Text) + "!";
            }
        }

        protected void btnRefresh_Click(object sender, EventArgs e)
        {
            lblTime.Text = DateTime.Now.ToLongTimeString();
        }
    }
}
