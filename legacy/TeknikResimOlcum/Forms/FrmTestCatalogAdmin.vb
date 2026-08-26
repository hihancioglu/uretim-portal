Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmTestCatalogAdmin
    Inherits Form

    Private ReadOnly grid As New DataGridView()
    Private ReadOnly txtName As New TextBox()
    Private ReadOnly txtDescription As New TextBox()
    Private ReadOnly numSort As New NumericUpDown()
    Private ReadOnly chkActive As New CheckBox()
    Private ReadOnly lblCount As New Label()
    Private selectedOriginalName As String = ""

    Public Sub New()
        AuthorizationService.Require(AppState.IsAdmin, "Test Listesi Yönetimi")
        AppIconService.Apply(Me)

        Text = "Test Listesi Yönetimi"
        StartPosition = FormStartPosition.CenterParent
        Size = New Size(980, 620)
        MinimumSize = New Size(760, 500)
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
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 150.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Controls.Add(root)

        Dim header As New Label() With {
            .Dock = DockStyle.Fill,
            .Text = "Test Listesi",
            .BackColor = Color.FromArgb(31, 71, 126),
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 13.0F, FontStyle.Bold),
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
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 105.0F))
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 55.0F))
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 95.0F))
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 45.0F))
        editor.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        editor.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        editor.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        root.Controls.Add(editor, 0, 1)

        ConfigureTextBox(txtName, "Örn. Çekme Testi / Ölçü Kontrol / Renk Kontrol")
        ConfigureMultiline(txtDescription, "Açıklama veya standart kontrol yöntemi")
        numSort.Minimum = 0
        numSort.Maximum = 9999
        numSort.Width = 90
        numSort.Dock = DockStyle.Left
        numSort.Margin = New Padding(5, 6, 5, 6)
        chkActive.Text = "Aktif"
        chkActive.Checked = True
        chkActive.Dock = DockStyle.Left
        chkActive.Margin = New Padding(5, 8, 5, 6)

        AddField(editor, "Test Adı", txtName, 0, 0)
        AddField(editor, "Sıra", numSort, 2, 0)
        AddField(editor, "Açıklama", txtDescription, 0, 1)
        editor.SetColumnSpan(txtDescription, 2)
        editor.Controls.Add(chkActive, 3, 1)

        Dim actions As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .BackColor = Color.White,
            .Margin = New Padding(0)
        }
        Dim btnSave As New Button()
        ConfigureButton(btnSave, "Kaydet / Güncelle", 150, Color.FromArgb(31, 71, 126), Color.White)
        AddHandler btnSave.Click, AddressOf Save_Click
        Dim btnNew As New Button()
        ConfigureButton(btnNew, "Yeni", 90, Color.White, Color.FromArgb(35, 50, 70))
        AddHandler btnNew.Click, AddressOf New_Click
        Dim btnMoveUp As New Button()
        ConfigureButton(btnMoveUp, "Yukarı", 85, Color.White, Color.FromArgb(31, 71, 126))
        AddHandler btnMoveUp.Click, Sub() MoveSelected(-1)
        Dim btnMoveDown As New Button()
        ConfigureButton(btnMoveDown, "Aşağı", 85, Color.White, Color.FromArgb(31, 71, 126))
        AddHandler btnMoveDown.Click, Sub() MoveSelected(1)
        Dim btnGroups As New Button()
        ConfigureButton(btnGroups, "Test Grupları", 120, Color.FromArgb(13, 126, 68), Color.White)
        AddHandler btnGroups.Click, AddressOf Groups_Click
        Dim btnDelete As New Button()
        ConfigureButton(btnDelete, "Seçili Testi Sil", 135, Color.MistyRose, Color.DarkRed)
        AddHandler btnDelete.Click, AddressOf Delete_Click
        Dim btnRefresh As New Button()
        ConfigureButton(btnRefresh, "Yenile", 90, Color.White, Color.FromArgb(35, 50, 70))
        AddHandler btnRefresh.Click, Sub() LoadGrid()
        Dim btnClose As New Button()
        ConfigureButton(btnClose, "Kapat", 90, Color.White, Color.FromArgb(35, 50, 70))
        AddHandler btnClose.Click, Sub() Close()
        actions.Controls.AddRange({btnSave, btnNew, btnMoveUp, btnMoveDown, btnGroups, btnDelete, btnRefresh, btnClose})
        editor.SetColumnSpan(actions, 4)
        editor.Controls.Add(actions, 0, 2)

        lblCount.Dock = DockStyle.Fill
        lblCount.BackColor = Color.FromArgb(229, 238, 249)
        lblCount.ForeColor = Color.FromArgb(31, 71, 126)
        lblCount.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        lblCount.TextAlign = ContentAlignment.MiddleLeft
        lblCount.Padding = New Padding(12, 0, 0, 0)
        lblCount.Margin = New Padding(0, 0, 0, 6)
        root.Controls.Add(lblCount, 0, 2)

        ConfigureGrid()
        root.Controls.Add(grid, 0, 3)
    End Sub

    Private Sub ConfigureGrid()
        grid.Dock = DockStyle.Fill
        grid.ReadOnly = True
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.MultiSelect = False
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.RowHeadersVisible = False
        grid.AutoGenerateColumns = False
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        grid.BackgroundColor = Color.White
        grid.BorderStyle = BorderStyle.FixedSingle
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(220, 232, 247)
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(31, 50, 75)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        grid.ColumnHeadersHeight = 38
        grid.RowTemplate.Height = 30

        grid.Columns.Add(MakeColumn("SortNo", "SIRA", 70, 6))
        grid.Columns.Add(MakeColumn("TestName", "TEST ADI", 220, 24))
        grid.Columns.Add(MakeColumn("Description", "AÇIKLAMA", 360, 45))
        grid.Columns.Add(MakeColumn("IsActiveDisplay", "DURUM", 90, 8))
        grid.Columns.Add(MakeColumn("UpdatedAt", "GÜNCELLEME", 130, 12))

        AddHandler grid.CellClick,
            Sub(sender, e)
                If e.RowIndex >= 0 Then LoadSelectedRow()
            End Sub
        AddHandler grid.CellDoubleClick,
            Sub(sender, e)
                If e.RowIndex >= 0 Then LoadSelectedRow()
            End Sub
    End Sub

    Private Sub LoadGrid()
        Try
            Dim items = DataService.GetTestCatalog(False)
            grid.Rows.Clear()
            For Each item In items
                Dim index = grid.Rows.Add(
                    item.SortNo,
                    item.TestName,
                    item.Description,
                    If(String.Equals(item.IsActive, "YES", StringComparison.OrdinalIgnoreCase), "Aktif", "Pasif"),
                    item.UpdatedAt)
                grid.Rows(index).Tag = item
                If Not String.Equals(item.IsActive, "YES", StringComparison.OrdinalIgnoreCase) Then
                    grid.Rows(index).DefaultCellStyle.ForeColor = Color.Gray
                    grid.Rows(index).DefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245)
                End If
            Next

            lblCount.Text = "Test sayısı: " & items.Count.ToString() &
                            "   |   Aktif: " & items.Where(Function(item) String.Equals(item.IsActive, "YES", StringComparison.OrdinalIgnoreCase)).Count().ToString()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Test listesi yüklenemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub LoadSelectedRow()
        If grid.CurrentRow Is Nothing Then Return
        Dim item = TryCast(grid.CurrentRow.Tag, TestCatalogItem)
        If item Is Nothing Then Return

        selectedOriginalName = item.TestName
        txtName.Text = item.TestName
        txtDescription.Text = item.Description
        Dim sortNo As Integer
        If Integer.TryParse(item.SortNo, sortNo) Then
            numSort.Value = Math.Max(numSort.Minimum, Math.Min(numSort.Maximum, sortNo))
        Else
            numSort.Value = 0
        End If
        chkActive.Checked = String.Equals(item.IsActive, "YES", StringComparison.OrdinalIgnoreCase)
    End Sub

    Private Sub Save_Click(sender As Object, e As EventArgs)
        Try
            Dim item As New TestCatalogItem With {
                .TestName = txtName.Text.Trim(),
                .Description = txtDescription.Text.Trim(),
                .SortNo = CInt(numSort.Value).ToString(),
                .IsActive = If(chkActive.Checked, "YES", "NO")
            }
            DataService.SaveTestCatalogItem(selectedOriginalName, item)
            AuditService.Log("TEST_CATALOG_SAVE", "", "", "Test=" & item.TestName)
            LoadGrid()
            SelectTest(item.TestName)
            MessageBox.Show("Test listesi kaydedildi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Test kaydedilemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub Delete_Click(sender As Object, e As EventArgs)
        Try
            Dim target = selectedOriginalName.Trim()
            If target = "" Then
                MessageBox.Show("Silinecek testi listeden seçiniz.", "Seçim gerekli", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If
            If MessageBox.Show("'" & target & "' test listesinden silinsin mi?" & Environment.NewLine &
                               "Eski test talepleri silinmez; sadece seçim listesinden kaldırılır.",
                               "Silme Onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) <> DialogResult.Yes Then
                Return
            End If

            DataService.DeleteTestCatalogItem(target)
            AuditService.Log("TEST_CATALOG_DELETE", "", "", "Test=" & target)
            LoadGrid()
            New_Click(sender, e)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Test silinemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub New_Click(sender As Object, e As EventArgs)
        selectedOriginalName = ""
        txtName.Clear()
        txtDescription.Clear()
        numSort.Value = GetNextSortNo()
        chkActive.Checked = True
        grid.ClearSelection()
        txtName.Focus()
    End Sub

    Private Sub MoveSelected(direction As Integer)
        Try
            Dim target = selectedOriginalName.Trim()
            If target = "" Then
                MessageBox.Show("Sırası değiştirilecek testi listeden seçiniz.", "Seçim gerekli", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            DataService.MoveTestCatalogItem(target, direction)
            AuditService.Log("TEST_CATALOG_MOVE", "", "", "Test=" & target & "; Yön=" & direction.ToString())
            LoadGrid()
            SelectTest(target)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Sıra değiştirilemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Sub Groups_Click(sender As Object, e As EventArgs)
        Using form As New FrmTestGroupAdmin()
            form.ShowDialog(Me)
        End Using
    End Sub

    Private Function GetNextSortNo() As Decimal
        Dim maxSort As Integer = 0
        For Each item In DataService.GetTestCatalog(False)
            Dim parsed As Integer
            If Integer.TryParse(item.SortNo, parsed) Then maxSort = Math.Max(maxSort, parsed)
        Next
        Return Math.Min(CDec(maxSort + 1), numSort.Maximum)
    End Function

    Private Sub SelectTest(testName As String)
        For Each row As DataGridViewRow In grid.Rows
            Dim item = TryCast(row.Tag, TestCatalogItem)
            If item IsNot Nothing AndAlso String.Equals(item.TestName, testName, StringComparison.OrdinalIgnoreCase) Then
                row.Selected = True
                grid.CurrentCell = row.Cells(0)
                LoadSelectedRow()
                Exit For
            End If
        Next
    End Sub

    Private Shared Function MakeColumn(name As String, header As String, minimumWidth As Integer, fillWeight As Single) As DataGridViewTextBoxColumn
        Return New DataGridViewTextBoxColumn() With {
            .Name = name,
            .HeaderText = header,
            .MinimumWidth = minimumWidth,
            .FillWeight = fillWeight,
            .SortMode = DataGridViewColumnSortMode.Automatic
        }
    End Function

    Private Shared Sub ConfigureTextBox(box As TextBox, placeholder As String)
        box.Dock = DockStyle.Fill
        box.Margin = New Padding(5, 6, 5, 6)
        box.PlaceholderText = placeholder
    End Sub

    Private Shared Sub ConfigureMultiline(box As TextBox, placeholder As String)
        ConfigureTextBox(box, placeholder)
        box.Multiline = True
        box.ScrollBars = ScrollBars.Vertical
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

    Private Shared Sub AddField(layout As TableLayoutPanel, caption As String, control As Control, column As Integer, row As Integer)
        Dim label As New Label() With {
            .Text = caption,
            .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Margin = New Padding(5)
        }
        layout.Controls.Add(label, column, row)
        layout.Controls.Add(control, column + 1, row)
    End Sub
End Class
