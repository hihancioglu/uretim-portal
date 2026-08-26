Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmPlasticShiftEmailRecipients
    Inherits Form

    Private ReadOnly grid As New DataGridView()
    Private ReadOnly txtEmail As New TextBox()
    Private ReadOnly txtDisplayName As New TextBox()
    Private ReadOnly cmbRecipientType As New ComboBox()
    Private ReadOnly chkActive As New CheckBox()
    Private ReadOnly lblCount As New Label()
    Private ReadOnly mechanismMode As Boolean
    Private selectedOriginalEmail As String = ""

    Public Sub New(Optional useMechanismMode As Boolean = False)
        mechanismMode = useMechanismMode
        AuthorizationService.Require(AppState.CanManagePlasticShiftEmailRecipients,
                                     If(mechanismMode, "Mekanizma Vardiya Takip Mail Alıcıları", "Plastikhane Vardiya Takip Mail Alıcıları"))
        AppIconService.Apply(Me)

        Text = If(mechanismMode, "Mekanizma Vardiya Takip Mail Alıcıları", "Plastikhane Vardiya Takip Mail Alıcıları")
        StartPosition = FormStartPosition.CenterParent
        Size = New Size(960, 600)
        MinimumSize = New Size(780, 520)
        BackColor = Color.FromArgb(243, 247, 252)
        Font = New Font("Segoe UI", 9.0F)

        BuildScreen()
        LoadGrid()
        New_Click(Me, EventArgs.Empty)
    End Sub

    Private Sub BuildScreen()
        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 4,
            .Padding = New Padding(10),
            .BackColor = BackColor
        }
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 48.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 154.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 60.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Controls.Add(root)

        Dim header As New Label() With {
            .Dock = DockStyle.Fill,
            .Text = If(mechanismMode,
                       "Mekanizma Vardiya Takip Otomatik Mail Alıcıları",
                       "Plastikhane Vardiya Takip Otomatik Mail Alıcıları"),
            .BackColor = Color.FromArgb(31, 71, 126),
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 12.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(16, 0, 0, 0),
            .Margin = New Padding(0, 0, 0, 8)
        }
        root.Controls.Add(header, 0, 0)

        Dim editor As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 4,
            .RowCount = 3,
            .BackColor = Color.White,
            .Padding = New Padding(10),
            .Margin = New Padding(0, 0, 0, 8)
        }
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 95.0F))
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 52.0F))
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 85.0F))
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 48.0F))
        editor.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        editor.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        editor.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        root.Controls.Add(editor, 0, 1)

        AddField(editor, "E-posta", txtEmail, 0, 0)
        AddField(editor, "Açıklama", txtDisplayName, 2, 0)
        txtEmail.PlaceholderText = "ornek@firma.com"
        txtDisplayName.PlaceholderText = "Örn. Kalite ekip listesi"

        cmbRecipientType.DropDownStyle = ComboBoxStyle.DropDownList
        cmbRecipientType.Items.AddRange(New Object() {"Kime", "CC"})
        AddField(editor, "Gönderim Türü", cmbRecipientType, 0, 1)

        editor.Controls.Add(New Label() With {
            .Dock = DockStyle.Fill,
            .Text = "Durum",
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Margin = New Padding(5)
        }, 2, 1)

        chkActive.Text = "Aktif"
        chkActive.Checked = True
        chkActive.Dock = DockStyle.Left
        chkActive.Margin = New Padding(5, 8, 5, 6)
        editor.Controls.Add(chkActive, 3, 1)

        Dim hint As New Label() With {
            .Dock = DockStyle.Fill,
            .Text = If(mechanismMode,
                       "Yeni mekanizma vardiya takip kaydı oluştuğunda aktif Kime alıcılarına mail gider; CC seçilen aktif alıcılar bilgi bölümüne eklenir.",
                       "Yeni plastikhane vardiya takip kaydı oluştuğunda aktif Kime alıcılarına mail gider; CC seçilen aktif alıcılar bilgi bölümüne eklenir."),
            .ForeColor = Color.FromArgb(70, 85, 105),
            .TextAlign = ContentAlignment.MiddleLeft,
            .AutoEllipsis = True,
            .Margin = New Padding(5, 5, 5, 5)
        }
        editor.SetColumnSpan(hint, 4)
        editor.Controls.Add(hint, 0, 2)

        Dim actions As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .BackColor = Color.White,
            .Padding = New Padding(8, 7, 8, 7),
            .Margin = New Padding(0, 0, 0, 6)
        }
        root.Controls.Add(actions, 0, 2)

        Dim btnSave As New Button()
        ConfigureButton(btnSave, "Kaydet / Güncelle", 145, Color.FromArgb(31, 71, 126), Color.White)
        AddHandler btnSave.Click, AddressOf Save_Click

        Dim btnNew As New Button()
        ConfigureButton(btnNew, "Yeni", 90, Color.White, Color.FromArgb(35, 50, 70))
        AddHandler btnNew.Click, AddressOf New_Click

        Dim btnDelete As New Button()
        ConfigureButton(btnDelete, "Seçili Alıcıyı Sil", 145, Color.MistyRose, Color.DarkRed)
        AddHandler btnDelete.Click, AddressOf Delete_Click

        Dim btnRefresh As New Button()
        ConfigureButton(btnRefresh, "Yenile", 90, Color.White, Color.FromArgb(35, 50, 70))
        AddHandler btnRefresh.Click, Sub() LoadGrid()

        Dim btnClose As New Button()
        ConfigureButton(btnClose, "Kapat", 90, Color.White, Color.FromArgb(35, 50, 70))
        AddHandler btnClose.Click, Sub() Close()

        lblCount.AutoSize = False
        lblCount.Width = 220
        lblCount.Height = 34
        lblCount.TextAlign = ContentAlignment.MiddleLeft
        lblCount.ForeColor = Color.FromArgb(31, 71, 126)
        lblCount.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        lblCount.Margin = New Padding(12, 0, 0, 0)

        actions.Controls.AddRange({btnSave, btnNew, btnDelete, btnRefresh, btnClose, lblCount})

        ConfigureGrid()
        root.Controls.Add(grid, 0, 3)
    End Sub

    Private Sub ConfigureGrid()
        grid.Dock = DockStyle.Fill
        grid.ReadOnly = True
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.AllowUserToResizeRows = False
        grid.MultiSelect = False
        grid.RowHeadersVisible = False
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.AutoGenerateColumns = False
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        grid.BackgroundColor = Color.White
        grid.BorderStyle = BorderStyle.FixedSingle
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersHeight = 36
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(220, 232, 247)
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(31, 50, 75)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)

        grid.Columns.Clear()
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Email", .HeaderText = "E-POSTA", .MinimumWidth = 220, .FillWeight = 40})
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "DisplayName", .HeaderText = "AÇIKLAMA", .MinimumWidth = 180, .FillWeight = 30})
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "RecipientType", .HeaderText = "GÖNDERİM TÜRÜ", .MinimumWidth = 120, .FillWeight = 12})
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "IsActive", .HeaderText = "DURUM", .MinimumWidth = 90, .FillWeight = 8})
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "UpdatedAt", .HeaderText = "GÜNCELLEME", .MinimumWidth = 130, .FillWeight = 10})

        AddHandler grid.SelectionChanged, AddressOf Grid_SelectionChanged
        AddHandler grid.CellDoubleClick, Sub(sender, e) If e.RowIndex >= 0 Then LoadSelected()
    End Sub

    Private Sub LoadGrid()
        Try
            Dim items = If(mechanismMode,
                           DataService.GetMechanismShiftEmailRecipients(False),
                           DataService.GetPlasticShiftEmailRecipients(False))
            grid.Rows.Clear()
            For Each item In items
                Dim statusText = If(String.Equals(item.IsActive, "YES", StringComparison.OrdinalIgnoreCase), "AKTİF", "PASİF")
                Dim rowIndex = grid.Rows.Add(item.Email, item.DisplayName, RecipientTypeDisplay(item.RecipientType), statusText, FormatDateTime(item.UpdatedAt))
                grid.Rows(rowIndex).Tag = item
            Next
            lblCount.Text = "Alıcı: " & items.Count.ToString() & " adet"
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Alıcı listesi yüklenemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub Save_Click(sender As Object, e As EventArgs)
        Try
            Dim item As New PlasticShiftEmailRecipient With {
                .Email = txtEmail.Text.Trim(),
                .DisplayName = txtDisplayName.Text.Trim(),
                .RecipientType = SelectedRecipientType(),
                .IsActive = If(chkActive.Checked, "YES", "NO")
            }
            If mechanismMode Then
                DataService.SaveMechanismShiftEmailRecipient(selectedOriginalEmail, item)
            Else
                DataService.SavePlasticShiftEmailRecipient(selectedOriginalEmail, item)
            End If
            LoadGrid()
            SelectByEmail(item.Email)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Alıcı kaydedilemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Sub Delete_Click(sender As Object, e As EventArgs)
        If grid.CurrentRow Is Nothing Then Return
        Dim item = TryCast(grid.CurrentRow.Tag, PlasticShiftEmailRecipient)
        If item Is Nothing Then Return

        Dim answer = MessageBox.Show(
            item.Email & " alıcısı silinecek. Devam edilsin mi?",
            "Alıcıyı sil",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2)
        If answer <> DialogResult.Yes Then Return

        Try
            If mechanismMode Then
                DataService.DeleteMechanismShiftEmailRecipient(item.Email)
            Else
                DataService.DeletePlasticShiftEmailRecipient(item.Email)
            End If
            LoadGrid()
            New_Click(Me, EventArgs.Empty)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Alıcı silinemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub New_Click(sender As Object, e As EventArgs)
        selectedOriginalEmail = ""
        txtEmail.Clear()
        txtDisplayName.Clear()
        cmbRecipientType.SelectedItem = "Kime"
        chkActive.Checked = True
        txtEmail.Focus()
    End Sub

    Private Sub Grid_SelectionChanged(sender As Object, e As EventArgs)
        LoadSelected()
    End Sub

    Private Sub LoadSelected()
        If grid.CurrentRow Is Nothing Then Return
        Dim item = TryCast(grid.CurrentRow.Tag, PlasticShiftEmailRecipient)
        If item Is Nothing Then Return

        selectedOriginalEmail = item.Email
        txtEmail.Text = item.Email
        txtDisplayName.Text = item.DisplayName
        cmbRecipientType.SelectedItem = RecipientTypeDisplay(item.RecipientType)
        chkActive.Checked = String.Equals(item.IsActive, "YES", StringComparison.OrdinalIgnoreCase)
    End Sub

    Private Sub SelectByEmail(email As String)
        For Each row As DataGridViewRow In grid.Rows
            Dim item = TryCast(row.Tag, PlasticShiftEmailRecipient)
            If item IsNot Nothing AndAlso String.Equals(item.Email, email, StringComparison.OrdinalIgnoreCase) Then
                row.Selected = True
                grid.CurrentCell = row.Cells("Email")
                LoadSelected()
                Exit For
            End If
        Next
    End Sub

    Private Shared Sub AddField(layout As TableLayoutPanel, caption As String, control As Control, column As Integer, row As Integer)
        layout.Controls.Add(New Label() With {
            .Dock = DockStyle.Fill,
            .Text = caption,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Margin = New Padding(5)
        }, column, row)
        control.Dock = DockStyle.Fill
        control.Margin = New Padding(5, 6, 5, 6)
        layout.Controls.Add(control, column + 1, row)
    End Sub

    Private Shared Sub ConfigureButton(button As Button, text As String, width As Integer, backColor As Color, foreColor As Color)
        button.Text = text
        button.Width = width
        button.Height = 34
        button.BackColor = backColor
        button.ForeColor = foreColor
        button.FlatStyle = FlatStyle.Flat
        button.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        button.Margin = New Padding(4, 0, 4, 0)
        button.Cursor = Cursors.Hand
        button.UseVisualStyleBackColor = False
    End Sub

    Private Shared Function FormatDateTime(value As String) As String
        Dim parsed As DateTime
        If DateTime.TryParse(value, parsed) Then Return parsed.ToString("dd.MM.yyyy HH:mm")
        Return ""
    End Function

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
