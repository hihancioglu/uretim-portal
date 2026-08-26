Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmTestGroupAdmin
    Inherits Form

    Private ReadOnly gridGroups As New DataGridView()
    Private ReadOnly gridCatalog As New DataGridView()
    Private ReadOnly lstSelectedTests As New ListBox()
    Private ReadOnly txtGroupName As New TextBox()
    Private ReadOnly numSort As New NumericUpDown()
    Private ReadOnly chkActive As New CheckBox()
    Private ReadOnly lblCount As New Label()
    Private catalogItems As New List(Of TestCatalogItem)()
    Private selectedOriginalName As String = ""

    Public Sub New()
        AuthorizationService.Require(AppState.IsAdmin, "Test Grubu Yönetimi")
        AppIconService.Apply(Me)

        Text = "Test Grubu Yönetimi"
        StartPosition = FormStartPosition.CenterParent
        Size = New Size(1100, 720)
        MinimumSize = New Size(900, 580)
        BackColor = Color.FromArgb(243, 247, 252)
        Font = New Font("Segoe UI", 9.0F)

        BuildScreen()
        LoadCatalog()
        LoadGroups()
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
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 300.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Controls.Add(root)

        Dim header As New Label() With {
            .Dock = DockStyle.Fill,
            .Text = "Test Grupları",
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
            .ColumnCount = 3,
            .RowCount = 3,
            .BackColor = Color.White,
            .Padding = New Padding(10),
            .Margin = New Padding(0, 0, 0, 8)
        }
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 46.0F))
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 150.0F))
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 54.0F))
        editor.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        editor.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        editor.RowStyles.Add(New RowStyle(SizeType.Absolute, 48.0F))
        root.Controls.Add(editor, 0, 1)

        Dim fieldRow As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 6,
            .RowCount = 1,
            .BackColor = Color.White
        }
        fieldRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 78.0F))
        fieldRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        fieldRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 46.0F))
        fieldRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 84.0F))
        fieldRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 70.0F))
        fieldRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 90.0F))
        editor.SetColumnSpan(fieldRow, 3)
        editor.Controls.Add(fieldRow, 0, 0)

        fieldRow.Controls.Add(MakeFieldLabel("Grup Adı"), 0, 0)
        ConfigureTextBox(txtGroupName, "Örn. Giriş kontrol temel testleri")
        fieldRow.Controls.Add(txtGroupName, 1, 0)
        fieldRow.Controls.Add(MakeFieldLabel("Sıra"), 2, 0)
        numSort.Minimum = 0
        numSort.Maximum = 9999
        numSort.Dock = DockStyle.Fill
        numSort.Margin = New Padding(5, 7, 10, 7)
        fieldRow.Controls.Add(numSort, 3, 0)
        chkActive.Text = "Aktif"
        chkActive.Checked = True
        chkActive.Dock = DockStyle.Fill
        chkActive.Margin = New Padding(5, 8, 5, 6)
        fieldRow.Controls.Add(chkActive, 4, 0)

        ConfigureCatalogGrid()
        ConfigureSelectedList()
        editor.Controls.Add(BuildPanel("Test Kataloğu", gridCatalog), 0, 1)

        Dim middleButtons As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 7,
            .BackColor = Color.White,
            .Padding = New Padding(8, 30, 8, 30)
        }
        For i As Integer = 0 To 6
            middleButtons.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Next
        editor.Controls.Add(middleButtons, 1, 1)

        Dim btnAdd As New Button()
        ConfigureButton(btnAdd, "Ekle →", 120, Color.FromArgb(31, 71, 126), Color.White)
        btnAdd.Dock = DockStyle.Fill
        AddHandler btnAdd.Click, Sub() AddSelectedCatalogTest()
        middleButtons.Controls.Add(btnAdd, 0, 0)

        Dim btnRemove As New Button()
        ConfigureButton(btnRemove, "← Çıkar", 120, Color.MistyRose, Color.DarkRed)
        btnRemove.Dock = DockStyle.Fill
        AddHandler btnRemove.Click, Sub() RemoveSelectedTest()
        middleButtons.Controls.Add(btnRemove, 0, 2)

        Dim btnUp As New Button()
        ConfigureButton(btnUp, "Yukarı", 120, Color.White, Color.FromArgb(31, 71, 126))
        btnUp.Dock = DockStyle.Fill
        AddHandler btnUp.Click, Sub() MoveSelectedTest(-1)
        middleButtons.Controls.Add(btnUp, 0, 3)

        Dim btnDown As New Button()
        ConfigureButton(btnDown, "Aşağı", 120, Color.White, Color.FromArgb(31, 71, 126))
        btnDown.Dock = DockStyle.Fill
        AddHandler btnDown.Click, Sub() MoveSelectedTest(1)
        middleButtons.Controls.Add(btnDown, 0, 4)

        Dim btnClearList As New Button()
        ConfigureButton(btnClearList, "Listeyi Temizle", 120, Color.White, Color.DarkRed)
        btnClearList.Dock = DockStyle.Fill
        AddHandler btnClearList.Click, Sub() lstSelectedTests.Items.Clear()
        middleButtons.Controls.Add(btnClearList, 0, 6)

        editor.Controls.Add(BuildPanel("Grup İçindeki Test Sırası", lstSelectedTests), 2, 1)

        Dim actions As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .BackColor = Color.White,
            .Margin = New Padding(0)
        }
        editor.SetColumnSpan(actions, 3)
        editor.Controls.Add(actions, 0, 2)

        Dim btnSave As New Button()
        ConfigureButton(btnSave, "Kaydet / Güncelle", 150, Color.FromArgb(31, 71, 126), Color.White)
        AddHandler btnSave.Click, AddressOf Save_Click
        Dim btnNew As New Button()
        ConfigureButton(btnNew, "Yeni", 90, Color.White, Color.FromArgb(35, 50, 70))
        AddHandler btnNew.Click, AddressOf New_Click
        Dim btnDelete As New Button()
        ConfigureButton(btnDelete, "Seçili Grubu Sil", 140, Color.MistyRose, Color.DarkRed)
        AddHandler btnDelete.Click, AddressOf Delete_Click
        Dim btnRefresh As New Button()
        ConfigureButton(btnRefresh, "Yenile", 90, Color.White, Color.FromArgb(35, 50, 70))
        AddHandler btnRefresh.Click, Sub() LoadGroups()
        Dim btnClose As New Button()
        ConfigureButton(btnClose, "Kapat", 90, Color.White, Color.FromArgb(35, 50, 70))
        AddHandler btnClose.Click, Sub() Close()
        actions.Controls.AddRange({btnSave, btnNew, btnDelete, btnRefresh, btnClose})

        lblCount.Dock = DockStyle.Fill
        lblCount.BackColor = Color.FromArgb(229, 238, 249)
        lblCount.ForeColor = Color.FromArgb(31, 71, 126)
        lblCount.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        lblCount.TextAlign = ContentAlignment.MiddleLeft
        lblCount.Padding = New Padding(12, 0, 0, 0)
        lblCount.Margin = New Padding(0, 0, 0, 6)
        root.Controls.Add(lblCount, 0, 2)

        ConfigureGroupsGrid()
        root.Controls.Add(gridGroups, 0, 3)
    End Sub

    Private Function BuildPanel(title As String, child As Control) As TableLayoutPanel
        Dim panel As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .BackColor = Color.White,
            .Padding = New Padding(0),
            .Margin = New Padding(0)
        }
        panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 28.0F))
        panel.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        panel.Controls.Add(New Label() With {
            .Dock = DockStyle.Fill,
            .Text = title,
            .ForeColor = Color.FromArgb(31, 71, 126),
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0)
        panel.Controls.Add(child, 0, 1)
        Return panel
    End Function

    Private Sub ConfigureCatalogGrid()
        ConfigureGridBase(gridCatalog)
        gridCatalog.Columns.Add(MakeColumn("SortNo", "SIRA", 65, 8))
        gridCatalog.Columns.Add(MakeColumn("TestName", "TEST", 200, 45))
        gridCatalog.Columns.Add(MakeColumn("Description", "AÇIKLAMA", 240, 47))
        AddHandler gridCatalog.CellDoubleClick,
            Sub(sender, e)
                If e.RowIndex >= 0 Then AddSelectedCatalogTest()
            End Sub
    End Sub

    Private Sub ConfigureGroupsGrid()
        ConfigureGridBase(gridGroups)
        gridGroups.Columns.Add(MakeColumn("SortNo", "SIRA", 65, 6))
        gridGroups.Columns.Add(MakeColumn("GroupName", "GRUP ADI", 220, 22))
        gridGroups.Columns.Add(MakeColumn("TestsText", "TESTLER", 520, 50))
        gridGroups.Columns.Add(MakeColumn("IsActiveDisplay", "DURUM", 90, 8))
        gridGroups.Columns.Add(MakeColumn("UpdatedAt", "GÜNCELLEME", 140, 14))
        AddHandler gridGroups.CellClick,
            Sub(sender, e)
                If e.RowIndex >= 0 Then LoadSelectedGroup()
            End Sub
        AddHandler gridGroups.CellDoubleClick,
            Sub(sender, e)
                If e.RowIndex >= 0 Then LoadSelectedGroup()
            End Sub
    End Sub

    Private Shared Sub ConfigureGridBase(targetGrid As DataGridView)
        targetGrid.Dock = DockStyle.Fill
        targetGrid.ReadOnly = True
        targetGrid.AllowUserToAddRows = False
        targetGrid.AllowUserToDeleteRows = False
        targetGrid.MultiSelect = False
        targetGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        targetGrid.RowHeadersVisible = False
        targetGrid.AutoGenerateColumns = False
        targetGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        targetGrid.BackgroundColor = Color.White
        targetGrid.BorderStyle = BorderStyle.FixedSingle
        targetGrid.EnableHeadersVisualStyles = False
        targetGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(220, 232, 247)
        targetGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(31, 50, 75)
        targetGrid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        targetGrid.ColumnHeadersHeight = 34
        targetGrid.RowTemplate.Height = 30
        targetGrid.Columns.Clear()
    End Sub

    Private Shared Function MakeColumn(name As String, header As String, minWidth As Integer, fillWeight As Single) As DataGridViewTextBoxColumn
        Return New DataGridViewTextBoxColumn() With {
            .Name = name,
            .HeaderText = header,
            .MinimumWidth = minWidth,
            .FillWeight = fillWeight,
            .SortMode = DataGridViewColumnSortMode.NotSortable
        }
    End Function

    Private Sub ConfigureSelectedList()
        lstSelectedTests.Dock = DockStyle.Fill
        lstSelectedTests.BorderStyle = BorderStyle.FixedSingle
        lstSelectedTests.HorizontalScrollbar = True
        lstSelectedTests.IntegralHeight = False
        lstSelectedTests.BackColor = Color.White
        lstSelectedTests.Font = New Font("Segoe UI", 9.0F)
    End Sub

    Private Shared Function MakeFieldLabel(text As String) As Label
        Return New Label() With {
            .Dock = DockStyle.Fill,
            .Text = text,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .ForeColor = Color.FromArgb(20, 35, 55),
            .TextAlign = ContentAlignment.MiddleLeft
        }
    End Function

    Private Sub LoadCatalog()
        catalogItems = DataService.GetTestCatalog(True)
        gridCatalog.Rows.Clear()
        For Each item In catalogItems
            Dim index = gridCatalog.Rows.Add(item.SortNo, item.TestName, item.Description)
            gridCatalog.Rows(index).Tag = item
        Next
    End Sub

    Private Sub LoadGroups()
        Dim items = DataService.GetTestGroups(False)
        gridGroups.Rows.Clear()
        For Each item In items
            Dim index = gridGroups.Rows.Add(
                item.SortNo,
                item.GroupName,
                item.TestsText,
                If(String.Equals(item.IsActive, "YES", StringComparison.OrdinalIgnoreCase), "Aktif", "Pasif"),
                item.UpdatedAt)
            gridGroups.Rows(index).Tag = item
            If Not String.Equals(item.IsActive, "YES", StringComparison.OrdinalIgnoreCase) Then
                gridGroups.Rows(index).DefaultCellStyle.ForeColor = Color.Gray
                gridGroups.Rows(index).DefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245)
            End If
        Next

        lblCount.Text = "Test grubu: " & items.Count.ToString() &
                        "   |   Aktif: " & items.Where(Function(item) String.Equals(item.IsActive, "YES", StringComparison.OrdinalIgnoreCase)).Count().ToString()
    End Sub

    Private Sub LoadSelectedGroup()
        If gridGroups.CurrentRow Is Nothing Then Return
        Dim item = TryCast(gridGroups.CurrentRow.Tag, TestGroupItem)
        If item Is Nothing Then Return

        selectedOriginalName = item.GroupName
        txtGroupName.Text = item.GroupName
        lstSelectedTests.Items.Clear()
        For Each testName In SplitTests(item.TestsText)
            lstSelectedTests.Items.Add(testName)
        Next

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
            Dim item As New TestGroupItem With {
                .GroupName = txtGroupName.Text.Trim(),
                .TestsText = BuildSelectedTestsText(),
                .SortNo = CInt(numSort.Value).ToString(),
                .IsActive = If(chkActive.Checked, "YES", "NO")
            }
            DataService.SaveTestGroup(selectedOriginalName, item)
            LoadGroups()
            SelectGroup(item.GroupName)
            MessageBox.Show("Test grubu kaydedildi.", "Test Grubu Yönetimi", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Test grubu kaydedilemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub New_Click(sender As Object, e As EventArgs)
        selectedOriginalName = ""
        txtGroupName.Clear()
        lstSelectedTests.Items.Clear()
        chkActive.Checked = True
        numSort.Value = NextSortNo()
        txtGroupName.Focus()
    End Sub

    Private Sub Delete_Click(sender As Object, e As EventArgs)
        If selectedOriginalName.Trim() = "" Then
            MessageBox.Show("Silinecek test grubunu seçin.", "Test Grubu Yönetimi", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        If MessageBox.Show("Seçili test grubu silinsin mi?", "Test Grubu Sil", MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then Return

        Try
            DataService.DeleteTestGroup(selectedOriginalName)
            LoadGroups()
            New_Click(Me, EventArgs.Empty)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Test grubu silinemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub AddSelectedCatalogTest()
        If gridCatalog.CurrentRow Is Nothing Then Return
        Dim item = TryCast(gridCatalog.CurrentRow.Tag, TestCatalogItem)
        If item Is Nothing Then Return
        lstSelectedTests.Items.Add(item.TestName.Trim())
        lstSelectedTests.SelectedIndex = lstSelectedTests.Items.Count - 1
    End Sub

    Private Sub RemoveSelectedTest()
        If lstSelectedTests.SelectedIndex < 0 Then Return
        Dim index = lstSelectedTests.SelectedIndex
        lstSelectedTests.Items.RemoveAt(index)
        If lstSelectedTests.Items.Count > 0 Then lstSelectedTests.SelectedIndex = Math.Min(index, lstSelectedTests.Items.Count - 1)
    End Sub

    Private Sub MoveSelectedTest(direction As Integer)
        Dim index = lstSelectedTests.SelectedIndex
        If index < 0 Then Return
        Dim newIndex = index + If(direction < 0, -1, 1)
        If newIndex < 0 OrElse newIndex >= lstSelectedTests.Items.Count Then Return

        Dim value = lstSelectedTests.Items(index)
        lstSelectedTests.Items.RemoveAt(index)
        lstSelectedTests.Items.Insert(newIndex, value)
        lstSelectedTests.SelectedIndex = newIndex
    End Sub

    Private Function BuildSelectedTestsText() As String
        Dim parts As New List(Of String)()
        For Each item In lstSelectedTests.Items
            parts.Add(item.ToString())
        Next
        Return String.Join("; ", parts)
    End Function

    Private Function NextSortNo() As Integer
        Dim maxSort As Integer = 0
        For Each item In DataService.GetTestGroups(False)
            Dim sortNo As Integer
            If Integer.TryParse(If(item.SortNo, "").Trim(), sortNo) Then
                maxSort = Math.Max(maxSort, sortNo)
            End If
        Next
        Return maxSort + 1
    End Function

    Private Sub SelectGroup(groupName As String)
        For Each row As DataGridViewRow In gridGroups.Rows
            Dim item = TryCast(row.Tag, TestGroupItem)
            If item IsNot Nothing AndAlso String.Equals(item.GroupName, groupName, StringComparison.OrdinalIgnoreCase) Then
                gridGroups.CurrentCell = row.Cells(0)
                LoadSelectedGroup()
                Return
            End If
        Next
    End Sub

    Private Shared Function SplitTests(value As String) As List(Of String)
        Return If(value, "").
            Replace(vbCrLf, ";").
            Replace(vbCr, ";").
            Replace(vbLf, ";").
            Split({";"c}, StringSplitOptions.RemoveEmptyEntries).
            Select(Function(part) part.Trim()).
            Where(Function(part) part <> "").
            ToList()
    End Function

    Private Shared Sub ConfigureTextBox(textBox As TextBox, placeholder As String)
        textBox.Dock = DockStyle.Fill
        textBox.PlaceholderText = placeholder
        textBox.Margin = New Padding(5, 7, 10, 7)
    End Sub

    Private Shared Sub ConfigureButton(button As Button, text As String, width As Integer, backColor As Color, foreColor As Color)
        button.Text = text
        button.Width = width
        button.Height = 34
        button.BackColor = backColor
        button.ForeColor = foreColor
        button.FlatStyle = FlatStyle.Flat
        button.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        button.Margin = New Padding(8, 4, 0, 4)
        button.Cursor = Cursors.Hand
        button.UseVisualStyleBackColor = False
    End Sub
End Class
