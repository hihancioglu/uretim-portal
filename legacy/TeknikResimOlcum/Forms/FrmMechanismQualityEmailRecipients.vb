Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmMechanismQualityEmailRecipients
    Inherits Form
    Private ReadOnly packageMeterMode As Boolean
    Private ReadOnly grid As New DataGridView()
    Private ReadOnly txtEmail As New TextBox()
    Private ReadOnly txtName As New TextBox()
    Private ReadOnly cmbRecipientType As New ComboBox()
    Private ReadOnly chkActive As New CheckBox()
    Private ReadOnly lblCount As New Label()
    Private selectedEmail As String = ""

    Public Sub New(Optional packageMeterMode As Boolean = False)
        Me.packageMeterMode = packageMeterMode
        AuthorizationService.Require(If(packageMeterMode,
                                        AppState.CanManagePackageMeterEmailRecipients,
                                        AppState.CanManageMechanismQualityEmailRecipients),
                                     If(packageMeterMode,
                                        "Paket sayaç uygunsuzluk mail alıcıları",
                                        "Mekanizma kalite uygunsuzluk mail alıcıları"))
        AppIconService.Apply(Me)
        Text = If(packageMeterMode,
                  "Paket Sayaç Test Sonucu Uygun Değil Mail Alıcıları",
                  "Mekanizma Kalite Uygun Değil Mail Alıcıları")
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        Size = New Size(900, 560)
        MinimumSize = New Size(740, 460)
        BackColor = Color.FromArgb(243, 247, 252)
        Font = New Font("Segoe UI", 9.0F)
        BuildScreen()
        LoadGrid()
        ClearEditor()
    End Sub

    Private Sub BuildScreen()
        Dim root As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 4, .Padding = New Padding(10), .BackColor = BackColor}
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 48))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 154))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 60))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        Controls.Add(root)
        root.Controls.Add(New Label With {.Dock = DockStyle.Fill, .Text = If(packageMeterMode, "Paket Sayaç Test Sonucu Uygun Değil Otomatik Mail Alıcıları", "Mekanizma Kalite Uygun Değil Otomatik Mail Alıcıları"), .BackColor = Color.FromArgb(31, 71, 126), .ForeColor = Color.White, .Font = New Font("Segoe UI", 12, FontStyle.Bold), .TextAlign = ContentAlignment.MiddleLeft, .Padding = New Padding(16, 0, 0, 0), .Margin = New Padding(0, 0, 0, 8)}, 0, 0)

        Dim editor As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 4, .RowCount = 3, .BackColor = Color.White, .Padding = New Padding(10), .Margin = New Padding(0, 0, 0, 8)}
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 95))
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 52))
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 85))
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 48))
        editor.RowStyles.Add(New RowStyle(SizeType.Absolute, 38))
        editor.RowStyles.Add(New RowStyle(SizeType.Absolute, 38))
        editor.RowStyles.Add(New RowStyle(SizeType.Absolute, 38))
        AddField(editor, "E-posta", txtEmail, 0, 0)
        AddField(editor, "Açıklama", txtName, 2, 0)
        txtEmail.PlaceholderText = "ornek@firma.com"
        txtName.PlaceholderText = If(packageMeterMode, "Paket sayaç kalite ekibi", "Mekanizma kalite ekibi")
        cmbRecipientType.DropDownStyle = ComboBoxStyle.DropDownList
        cmbRecipientType.Items.AddRange(New Object() {"Kime", "CC"})
        AddField(editor, "Gönderim Türü", cmbRecipientType, 0, 1)
        editor.Controls.Add(New Label With {
            .Text = "Durum",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Font = New Font("Segoe UI", 9, FontStyle.Bold)
        }, 2, 1)
        chkActive.Text = "Aktif"
        chkActive.Checked = True
        chkActive.Dock = DockStyle.Left
        chkActive.Margin = New Padding(5, 8, 5, 6)
        editor.Controls.Add(chkActive, 3, 1)

        Dim hint As New Label() With {
            .Dock = DockStyle.Fill,
            .Text = If(packageMeterMode,
                       "Bir sayacın test sonucu yeni olarak Uygun Değil kaydedildiğinde aktif Kime alıcılarına mail gider; CC seçilen aktif alıcılar bilgi bölümüne eklenir.",
                       "Uygun Değil sonucu oluştuğunda aktif Kime alıcılarına mail gider; CC seçilen aktif alıcılar bilgi bölümüne eklenir."),
            .ForeColor = Color.FromArgb(70, 85, 105),
            .TextAlign = ContentAlignment.MiddleLeft,
            .AutoEllipsis = True,
            .Margin = New Padding(5, 5, 5, 5)
        }
        editor.SetColumnSpan(hint, 4)
        editor.Controls.Add(hint, 0, 2)
        root.Controls.Add(editor, 0, 1)

        Dim buttons As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .WrapContents = False, .Padding = New Padding(8, 7, 8, 7), .BackColor = Color.White, .Margin = New Padding(0, 0, 0, 6)}
        AddToolbarButton(buttons, "Kaydet / Güncelle", 145, Color.FromArgb(31, 71, 126), Color.White, AddressOf Save_Click)
        AddToolbarButton(buttons, "Yeni", 80, Color.White, Color.FromArgb(35, 50, 70), AddressOf New_Click)
        AddToolbarButton(buttons, "Seçili Alıcıyı Sil", 145, Color.MistyRose, Color.DarkRed, AddressOf Delete_Click)
        AddToolbarButton(buttons, "Yenile", 80, Color.White, Color.FromArgb(35, 50, 70), AddressOf Refresh_Click)
        AddToolbarButton(buttons, "Kapat", 80, Color.White, Color.FromArgb(35, 50, 70), AddressOf Close_Click)
        lblCount.AutoSize = False
        lblCount.Width = 220
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
        grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Email", .HeaderText = "E-POSTA", .MinimumWidth = 220, .FillWeight = 38})
        grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Name", .HeaderText = "AÇIKLAMA", .MinimumWidth = 180, .FillWeight = 30})
        grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "RecipientType", .HeaderText = "GÖNDERİM TÜRÜ", .MinimumWidth = 120, .FillWeight = 16})
        grid.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Active", .HeaderText = "DURUM", .MinimumWidth = 90, .FillWeight = 10})
        AddHandler grid.SelectionChanged, AddressOf Grid_SelectionChanged
        root.Controls.Add(grid, 0, 3)
    End Sub

    Private Shared Sub AddToolbarButton(parent As FlowLayoutPanel, caption As String, width As Integer, backColor As Color, foreColor As Color, handler As EventHandler)
        Dim button As New Button With {.Text = caption, .Width = width, .Height = 34, .BackColor = backColor, .ForeColor = foreColor, .FlatStyle = FlatStyle.Flat, .Font = New Font("Segoe UI", 9, FontStyle.Bold), .Margin = New Padding(0, 0, 8, 0), .UseCompatibleTextRendering = False}
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
            Dim items = If(packageMeterMode,
                           DataService.GetPackageMeterEmailRecipients(False),
                           DataService.GetMechanismQualityEmailRecipients(False))
            For Each item In items
                Dim index = grid.Rows.Add(item.Email, item.DisplayName, RecipientTypeDisplay(item.RecipientType), If(item.IsActive = "YES", "AKTİF", "PASİF"))
                grid.Rows(index).Tag = item
            Next
            Dim toCount = items.Where(Function(item) Not String.Equals(item.RecipientType, "CC", StringComparison.OrdinalIgnoreCase)).Count()
            Dim ccCount = items.Where(Function(item) String.Equals(item.RecipientType, "CC", StringComparison.OrdinalIgnoreCase)).Count()
            lblCount.Text = "Kime: " & toCount.ToString() & " | CC: " & ccCount.ToString()
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
        cmbRecipientType.SelectedItem = RecipientTypeDisplay(item.RecipientType)
        chkActive.Checked = String.Equals(item.IsActive, "YES", StringComparison.OrdinalIgnoreCase)
    End Sub

    Private Sub Save_Click(sender As Object, e As EventArgs)
        Try
            Dim item As New PlasticShiftEmailRecipient With {
                .Email = txtEmail.Text.Trim(),
                .DisplayName = txtName.Text.Trim(),
                .RecipientType = SelectedRecipientType(),
                .IsActive = If(chkActive.Checked, "YES", "NO")
            }
            If packageMeterMode Then
                DataService.SavePackageMeterEmailRecipient(selectedEmail, item)
            Else
                DataService.SaveMechanismQualityEmailRecipient(selectedEmail, item)
            End If
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
            If packageMeterMode Then
                DataService.DeletePackageMeterEmailRecipient(selectedEmail)
            Else
                DataService.DeleteMechanismQualityEmailRecipient(selectedEmail)
            End If
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
        cmbRecipientType.SelectedItem = "Kime"
        chkActive.Checked = True
        txtEmail.Focus()
    End Sub

    Private Function SelectedRecipientType() As String
        Dim selected = If(TryCast(cmbRecipientType.SelectedItem, String), "").Trim()
        If String.Equals(selected, "CC", StringComparison.OrdinalIgnoreCase) Then Return "CC"
        Return "Kime"
    End Function

    Private Shared Function RecipientTypeDisplay(value As String) As String
        If String.Equals(If(value, "").Trim(), "CC", StringComparison.OrdinalIgnoreCase) Then Return "CC"
        Return "Kime"
    End Function
End Class
