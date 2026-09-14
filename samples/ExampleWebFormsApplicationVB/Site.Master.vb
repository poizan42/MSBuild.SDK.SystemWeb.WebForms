Public Class SiteMaster
    Inherits System.Web.UI.MasterPage

    ''' <summary>Exposed to content pages through the typed Master property the generator creates from &lt;%@ MasterType %&gt;.</summary>
    Public Property Heading As String
        Get
            Return lblHeading.Text
        End Get
        Set(value As String)
            lblHeading.Text = value
        End Set
    End Property

    Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
        Head1.Title = Heading
    End Sub
End Class
