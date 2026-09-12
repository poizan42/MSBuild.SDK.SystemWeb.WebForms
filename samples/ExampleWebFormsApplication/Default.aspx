<%@ Page Title="Home" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="ExampleWebFormsApplication._Default" %>
<%@ MasterType VirtualPath="~/Site.Master" %>
<%@ Register TagPrefix="uc" TagName="Hello" Src="~/Controls/Hello.ascx" %>

<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="server">
    <meta name="description" content="MSBuild.SDK.SystemWeb.WebForms sample" />
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
    <p>
        <asp:Label ID="lblMessage" runat="server" />
    </p>
    <p>
        <label for="txtName">Your name</label>
        <asp:TextBox ID="txtName" runat="server" />
        <asp:RequiredFieldValidator ID="valName" runat="server" ControlToValidate="txtName" ErrorMessage="Please enter a name." Display="Dynamic" />
        <asp:Button ID="btnGo" runat="server" Text="Say hello" OnClick="btnGo_Click" />
    </p>

    <%-- A server comment: the generator ignores this control. --%>
    <%-- <asp:Label ID="lblCommentedOut" runat="server" /> --%>

    <asp:UpdatePanel ID="upClock" runat="server" UpdateMode="Conditional">
        <ContentTemplate>
            <%-- ContentTemplate is a single-instance template, so lblTime and btnRefresh become page members. --%>
            <asp:Label ID="lblTime" runat="server" />
            <asp:Button ID="btnRefresh" runat="server" Text="Refresh" OnClick="btnRefresh_Click" />
        </ContentTemplate>
    </asp:UpdatePanel>

    <asp:Repeater ID="rptItems" runat="server">
        <HeaderTemplate><ul></HeaderTemplate>
        <ItemTemplate>
            <%-- ItemTemplate is instantiated per row, so lblItem does NOT become a page member. --%>
            <li><asp:Label ID="lblItem" runat="server" Text='<%# Container.DataItem %>' /></li>
        </ItemTemplate>
        <FooterTemplate></ul></FooterTemplate>
    </asp:Repeater>

    <asp:GridView ID="gvItems" runat="server" AutoGenerateColumns="false">
        <Columns>
            <asp:BoundField DataField="Length" HeaderText="Length" />
            <asp:TemplateField HeaderText="Value">
                <ItemTemplate>
                    <asp:Label ID="lblCell" runat="server" Text='<%# Container.DataItem %>' />
                </ItemTemplate>
            </asp:TemplateField>
        </Columns>
    </asp:GridView>

    <div id="divHtml" runat="server" class="panel">
        <input type="text" id="txtHtml" runat="server" />
        <input type="checkbox" id="chkHtml" runat="server" />
    </div>

    <uc:Hello ID="hello1" runat="server" />
    <ex:GreetingLabel ID="greeting" runat="server" />
</asp:Content>
