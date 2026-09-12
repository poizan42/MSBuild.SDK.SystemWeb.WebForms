namespace ExampleWebFormsApplication.Controls
{
    public partial class Hello : System.Web.UI.UserControl
    {
        public string Greeting
        {
            get { return lblHello.Text; }
            set { lblHello.Text = value; }
        }
    }
}
