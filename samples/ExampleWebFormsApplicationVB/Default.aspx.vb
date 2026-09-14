Public Class _Default
    Inherits System.Web.UI.Page

    Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
        ' Master is the typed property generated from <%@ MasterType %>; the controls are generated WithEvents fields.
        Master.Heading = "Example Web Forms Application (VB, SDK-style)"
        lblTime.Text = DateTime.Now.ToLongTimeString()

        If Not IsPostBack Then
            lblMessage.Text = "Enter your name and press the button."
            rptItems.DataSource = New String() {"alpha", "beta", "gamma"}
            rptItems.DataBind()
        End If
    End Sub

    Protected Sub btnGo_Click(sender As Object, e As EventArgs) Handles btnGo.Click
        lblMessage.Text = "Hello, " & Server.HtmlEncode(txtName.Text) & "!"
    End Sub

    Protected Sub btnRefresh_Click(sender As Object, e As EventArgs) Handles btnRefresh.Click
        lblTime.Text = DateTime.Now.ToLongTimeString()
    End Sub
End Class
