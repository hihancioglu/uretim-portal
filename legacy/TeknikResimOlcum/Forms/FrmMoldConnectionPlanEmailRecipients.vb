Imports System.Drawing
Imports System.Windows.Forms

Public Class FrmMoldConnectionPlanEmailRecipients
    Inherits Form

    Private ReadOnly grid As New DataGridView()
    Private ReadOnly txtEmail As New TextBox()
    Private ReadOnly txtName As New TextBox()
    Private ReadOnly chkActive As New CheckBox()
    Private ReadOnly lblCount As New Label()
    Private selectedEmail As String = ""

    Public Sub New()
        AuthorizationService.Require(AppState.CanManageMoldConnectionPlanEmailRecipients, "Bağlanacak Kalıp Listesi Mail Alıcıları")
        AppIconService.Apply(Me)
        Text = "Bağlanacak Kalıp Listesi - Açık Ticket Mail Alıcıları"
        StartPosition = FormStartPosition.CenterParent
        Size = New Size(960, 600)
        MinimumSize = New Size(820, 520)
        BackColor = Color.FromArgb(243, 247, 252)
        Font = New Font("Segoe UI", 9.0F)
        BuildScreen()
        LoadGrid()
        ClearEditor()
    End Sub

    Private Sub BuildScreen()
        Dim root As New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 4,
            .Padding = New Padding(10),
            .BackColor = BackColor
        }
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 48))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 108))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        Controls.Add(root)

        root.Controls.Add(New Label With {
            .Dock = DockStyle.Fill,
            .Text = "Bağlanacak Kalıp Listesi - Açık Kalıp Ticket Otomatik Mail Alıcıları",
            .BackColor = Color.FromArgb(31, 71, 126),
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 12, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(16, 0, 0, 0),
            .Margin = New Padding(0, 0, 0, 8),
            .AutoEllipsis = True
        }, 0, 0)

        Dim editor As New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 4,
            .RowCount = 2,
            .BackColor = Color.White,
            .Padding = New Padding(12),
            .Margin = New Padding(0, 0, 0, 8)
        }
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 90))
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 90))
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50))
        editor.RowStyles.Add(New RowStyle(SizeType.Absolute, 40))
        editor.RowStyles.Add(New RowStyle(SizeType.Absolute, 42))

        AddField(editor, "E-posta", txtEmail, 0, 0)
        AddField(editor, "Açıklama", txtName, 2, 0)
        txtEmail.PlaceholderText = "ornek@firma.com"
        txtName.PlaceholderText = "Örn. Üretim / kalıp bakım / kalite ekibi"

        chkActive.Text = "Aktif"
        chkActive.Checked = True
        chkActive.Dock = DockStyle.Left
        chkActive.Margin = New Padding(4, 9, 4, 6)
        editor.Controls.Add(chkActive, 1, 1)

        Dim hint As New Label With {
            .Dock = DockStyle.Fill,
            .Text = "Excel'den liste yüklendiğinde, listedeki TR veya kalıpta açık kalıp ticket varsa aktif alıcılara Outlook üzerinden otomatik mail gider.",
            .ForeColor = Color.FromArgb(70, 85, 105),
            .TextAlign = ContentAlignment.MiddleLeft,
            .AutoEllipsis = True,
            .Margin = New Padding(4, 4, 4, 4)
        }
        editor.SetColumnSpan(hint, 2)
        editor.Controls.Add(hint, 2, 1)
        root.Controls.Add(editor, 0, 1)

        Dim buttons As New FlowLayoutPanel With {
            .Dock = DockStyle.Fill,
            .WrapContents = False,
            .Padding = New Padding(8, 8, 8, 8),
            .BackColor = Color.White,
            .Margin = New Padding(0, 0, 0, 6),
            .AutoScroll = True
        }
        AddToolbarButton(buttons, "Kaydet / Güncelle", 150, Color.FromArgb(31, 71, 126), Color.White, AddressOf Save_Click)
        AddToolbarButton(buttons, "Yeni", 86, Color.White, Color.FromArgb(35, 50, 70), AddressOf New_Click)
        AddToolbarButton(buttons, "Seçili Alıcıyı Sil", 160, Color.MistyRose, Color.DarkRed, AddressOf Delete_Click)
        AddToolbarButton(buttons, "Yenile", 86, Color.White, Color.FromArgb(35, 50, 70), AddressOf Refresh_Click)
        AddToolbarButton(buttons, "Kapat", 86, Color.White, Color.FromArgb(35, 50, 70), AddressOf Close_Click)

        lblCount.AutoSize = False
        lblCount.Width = 230
        lblCount.Height = 34
        lblCount.TextAlign = ContentAlignment.MiddleLeft
        lblCount.ForeColor = Color.FromArgb(31, 71, 126)
        lblCount.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        lblCount.Margin = New Padding(12, 0, 0, 0)
        buttons.Controls.Add(lblCount)
        root.Controls.Add(buttons, 0, 2)

        grid.Dock = DockStyle.Fill
        grid.ReadOnly = True
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.AllowUserToResizeRows = False
        grid.RowHeadersVisible = False
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.MultiSelect = False
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        grid.BackgroundColor = Color.White
        grid.BorderStyle = BorderStyle.FixedSingle
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersHeight = 36
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(220, 232, 247)
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(31, 50, 75)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        grid.Columns.Add("Email", "E-POSTA")
        grid.Columns.Add("Name", "AÇIKLAMA")
        grid.Columns.Add("Active", "DURUM")
        AddHandler grid.SelectionChanged, AddressOf Grid_SelectionChanged
        root.Controls.Add(grid, 0, 3)
    End Sub

    Private Shared Sub AddToolbarButton(parent As FlowLayoutPanel, caption As String, width As Integer, backColor As Color, foreColor As Color, handler As EventHandler)
        Dim button As New Button With {
            .Text = caption,
            .Width = width,
            .Height = 34,
            .BackColor = backColor,
            .ForeColor = foreColor,
            .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Segoe UI", 9, FontStyle.Bold),
            .Margin = New Padding(0, 0, 8, 0),
            .UseCompatibleTextRendering = False
        }
        AddHandler button.Click, handler
        parent.Controls.Add(button)
    End Sub

    Private Shared Sub AddField(parent As TableLayoutPanel, caption As String, control As Control, labelColumn As Integer, row As Integer)
        parent.Controls.Add(New Label With {.Text = caption, .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Font = New Font("Segoe UI", 9, FontStyle.Bold)}, labelColumn, row)
        control.Dock = DockStyle.Fill
        control.Margin = New Padding(4)
        parent.Controls.Add(control, labelColumn + 1, row)
    End Sub

    Private Sub LoadGrid()
        Try
            grid.Rows.Clear()
            Dim items = DataService.GetMoldConnectionPlanEmailRecipients(False)
            For Each item In items
                Dim index = grid.Rows.Add(item.Email, item.DisplayName, If(String.Equals(item.IsActive, "YES", StringComparison.OrdinalIgnoreCase), "AKTİF", "PASİF"))
                grid.Rows(index).Tag = item
            Next
            lblCount.Text = "Alıcı: " & items.Count.ToString() & " adet"
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Alıcı listesi yüklenemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub Grid_SelectionChanged(sender As Object, e As EventArgs)
        If grid.CurrentRow Is Nothing Then Return
        Dim item = TryCast(grid.CurrentRow.Tag, PlasticShiftEmailRecipient)
        If item Is Nothing Then Return
        selectedEmail = item.Email
        txtEmail.Text = item.Email
        txtName.Text = item.DisplayName
        chkActive.Checked = String.Equals(item.IsActive, "YES", StringComparison.OrdinalIgnoreCase)
    End Sub

    Private Sub Save_Click(sender As Object, e As EventArgs)
        Try
            Dim item As New PlasticShiftEmailRecipient With {
                .Email = txtEmail.Text.Trim(),
                .DisplayName = txtName.Text.Trim(),
                .IsActive = If(chkActive.Checked, "YES", "NO")
            }
            DataService.SaveMoldConnectionPlanEmailRecipient(selectedEmail, item)
            LoadGrid()
            ClearEditor()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Alıcı kaydedilemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Sub Delete_Click(sender As Object, e As EventArgs)
        If selectedEmail = "" Then Return
        If MessageBox.Show(selectedEmail & " silinsin mi?", "Alıcıyı sil", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) <> DialogResult.Yes Then Return

        Try
            DataService.DeleteMoldConnectionPlanEmailRecipient(selectedEmail)
            LoadGrid()
            ClearEditor()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Alıcı silinemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub New_Click(sender As Object, e As EventArgs)
        ClearEditor()
    End Sub

    Private Sub Refresh_Click(sender As Object, e As EventArgs)
        LoadGrid()
    End Sub

    Private Sub Close_Click(sender As Object, e As EventArgs)
        Close()
    End Sub

    Private Sub ClearEditor()
        selectedEmail = ""
        txtEmail.Clear()
        txtName.Clear()
        chkActive.Checked = True
        txtEmail.Focus()
    End Sub
End Class
