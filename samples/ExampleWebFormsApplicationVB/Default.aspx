<%@ Page Title="Home" Language="VB" MasterPageFile="~/Site.Master" AutoEventWireup="false" CodeBehind="Default.aspx.vb" Inherits="ExampleWebFormsApplicationVB._Default" %>
<%@ MasterType VirtualPath="~/Site.Master" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <p>
        <asp:Label ID="lblMessage" runat="server" />
    </p>
    <p>
        <asp:TextBox ID="txtName" runat="server" />
        <%-- Handled in code-behind with a Handles clause, which is why the generated fields are WithEvents. --%>
        <asp:Button ID="btnGo" runat="server" Text="Say hello" />
    </p>

    <asp:UpdatePanel ID="upClock" runat="server" UpdateMode="Conditional">
        <ContentTemplate>
            <asp:Label ID="lblTime" runat="server" />
            <asp:Button ID="btnRefresh" runat="server" Text="Refresh" />
        </ContentTemplate>
    </asp:UpdatePanel>

    <asp:Repeater ID="rptItems" runat="server">
        <ItemTemplate>
            <%-- Not a page member: multi-instance template. --%>
            <asp:Label ID="lblItem" runat="server" Text='<%# Container.DataItem %>' />
        </ItemTemplate>
    </asp:Repeater>
</asp:Content>
