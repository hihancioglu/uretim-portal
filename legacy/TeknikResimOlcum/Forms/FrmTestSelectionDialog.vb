Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmTestSelectionDialog
    Inherits Form

    Private ReadOnly txtSearch As New TextBox()
    Private ReadOnly gridCatalog As New DataGridView()
    Private ReadOnly gridSelected As New DataGridView()
    Private ReadOnly txtExtra As New TextBox()
    Private ReadOnly lblSelected As New Label()
    Private ReadOnly cboGroups As New ComboBox()
    Private ReadOnly catalogItems As List(Of TestCatalogItem)
    Private ReadOnly groupItems As List(Of TestGroupItem)
    Private ReadOnly selectedTestNames As New List(Of String)()
    Private dragSourceIndex As Integer = -1
    Private dragStartPoint As Point = Point.Empty

    Public Property SelectedTestsText As String = ""

    Public Sub New(initialValue As String)
        AppIconService.Apply(Me)

        Text = "Test Seçimi"
        StartPosition = FormStartPosition.CenterParent
        Size = New Size(1120, 700)
        MinimumSize = New Size(900, 560)
        BackColor = Color.FromArgb(244, 247, 251)
        Font = New Font("Segoe UI", 9.0F)

        catalogItems = DataService.GetTestCatalog(True).
            Where(Function(item) Not String.IsNullOrWhiteSpace(item.TestName)).
            ToList()

        groupItems = DataService.GetTestGroups(True).
            Where(Function(item) Not String.IsNullOrWhiteSpace(item.GroupName)).
            ToList()

        BuildScreen()
        LoadInitialSelection(initialValue)
        RefreshCatalogGrid()
        RefreshSelectedGrid()
    End Sub

    Private Sub BuildScreen()
        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 5,
            .Padding = New Padding(10),
            .BackColor = BackColor
        }
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 48.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 88.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 54.0F))
        Controls.Add(root)

        Dim header As New Label() With {
            .Dock = DockStyle.Fill,
            .Text = "Talep Edilen Testleri Seç",
            .BackColor = Color.FromArgb(31, 71, 126),
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 12.5F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(16, 0, 0, 0),
            .Margin = New Padding(0, 0, 0, 8)
        }
        root.Controls.Add(header, 0, 0)

        Dim topRow As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 4,
            .RowCount = 1,
            .BackColor = Color.White,
            .Padding = New Padding(8, 4, 8, 4),
            .Margin = New Padding(0, 0, 0, 8)
        }
        topRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 55.0F))
        topRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 90.0F))
        topRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 45.0F))
        topRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 150.0F))
        root.Controls.Add(topRow, 0, 1)

        txtSearch.Dock = DockStyle.Fill
        txtSearch.PlaceholderText = "Test adında veya açıklamada ara..."
        txtSearch.Margin = New Padding(0, 3, 10, 3)
        AddHandler txtSearch.TextChanged, Sub() RefreshCatalogGrid()
        topRow.Controls.Add(txtSearch, 0, 0)

        topRow.Controls.Add(New Label() With {
            .Dock = DockStyle.Fill,
            .Text = "Test Grubu",
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft,
            .ForeColor = Color.FromArgb(31, 50, 75)
        }, 1, 0)

        cboGroups.Dock = DockStyle.Fill
        cboGroups.DropDownStyle = ComboBoxStyle.DropDownList
        cboGroups.DisplayMember = "GroupName"
        cboGroups.Margin = New Padding(0, 3, 10, 3)
        For Each groupItem In groupItems
            cboGroups.Items.Add(groupItem)
        Next
        If cboGroups.Items.Count > 0 Then cboGroups.SelectedIndex = 0
        topRow.Controls.Add(cboGroups, 2, 0)

        lblSelected.Dock = DockStyle.Fill
        lblSelected.ForeColor = Color.FromArgb(31, 71, 126)
        lblSelected.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        lblSelected.TextAlign = ContentAlignment.MiddleLeft
        topRow.Controls.Add(lblSelected, 3, 0)

        Dim body As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 3,
            .RowCount = 1,
            .BackColor = BackColor,
            .Margin = New Padding(0, 0, 0, 8)
        }
        body.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        body.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 150.0F))
        body.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        root.Controls.Add(body, 0, 2)

        Dim leftPanel = BuildTitledPanel("Test Kataloğu")
        ConfigureCatalogGrid()
        leftPanel.Controls.Add(gridCatalog, 0, 1)
        body.Controls.Add(leftPanel, 0, 0)

        Dim buttonPanel As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 8,
            .BackColor = BackColor,
            .Padding = New Padding(8, 24, 8, 24)
        }
        For i As Integer = 0 To 7
            buttonPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Next
        body.Controls.Add(buttonPanel, 1, 0)

        Dim btnAdd As New Button()
        ConfigureButton(btnAdd, "Test Ekle →", 124, Color.FromArgb(31, 71, 126), Color.White)
        btnAdd.Dock = DockStyle.Fill
        AddHandler btnAdd.Click, Sub() AddCatalogSelection()
        buttonPanel.Controls.Add(btnAdd, 0, 0)

        Dim btnAddGroup As New Button()
        ConfigureButton(btnAddGroup, "Grubu Ekle →", 124, Color.FromArgb(13, 126, 68), Color.White)
        btnAddGroup.Dock = DockStyle.Fill
        AddHandler btnAddGroup.Click, Sub() AddGroupSelection()
        buttonPanel.Controls.Add(btnAddGroup, 0, 1)

        Dim btnRemove As New Button()
        ConfigureButton(btnRemove, "← Çıkar", 124, Color.MistyRose, Color.DarkRed)
        btnRemove.Dock = DockStyle.Fill
        AddHandler btnRemove.Click, Sub() RemoveSelected()
        buttonPanel.Controls.Add(btnRemove, 0, 3)

        Dim btnUp As New Button()
        ConfigureButton(btnUp, "Yukarı", 124, Color.White, Color.FromArgb(31, 71, 126))
        btnUp.Dock = DockStyle.Fill
        AddHandler btnUp.Click, Sub() MoveSelected(-1)
        buttonPanel.Controls.Add(btnUp, 0, 4)

        Dim btnDown As New Button()
        ConfigureButton(btnDown, "Aşağı", 124, Color.White, Color.FromArgb(31, 71, 126))
        btnDown.Dock = DockStyle.Fill
        AddHandler btnDown.Click, Sub() MoveSelected(1)
        buttonPanel.Controls.Add(btnDown, 0, 5)

        Dim btnTop As New Button()
        ConfigureButton(btnTop, "En Üste Al", 124, Color.White, Color.FromArgb(31, 71, 126))
        btnTop.Dock = DockStyle.Fill
        AddHandler btnTop.Click, Sub() MoveSelectedToEdge(True)
        buttonPanel.Controls.Add(btnTop, 0, 6)

        Dim btnBottom As New Button()
        ConfigureButton(btnBottom, "En Alta Al", 124, Color.White, Color.FromArgb(31, 71, 126))
        btnBottom.Dock = DockStyle.Fill
        AddHandler btnBottom.Click, Sub() MoveSelectedToEdge(False)
        buttonPanel.Controls.Add(btnBottom, 0, 7)

        Dim rightPanel = BuildTitledPanel("Seçilen Test Akışı")
        ConfigureSelectedGrid()
        rightPanel.Controls.Add(gridSelected, 0, 1)
        body.Controls.Add(rightPanel, 2, 0)

        Dim extraPanel As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .BackColor = Color.White,
            .Padding = New Padding(8),
            .Margin = New Padding(0, 0, 0, 8)
        }
        extraPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 22.0F))
        extraPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        root.Controls.Add(extraPanel, 0, 3)

        extraPanel.Controls.Add(New Label() With {
            .Dock = DockStyle.Fill,
            .Text = "Listede yoksa ek test / kriter açıklaması",
            .ForeColor = Color.FromArgb(45, 65, 92),
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0)

        txtExtra.Dock = DockStyle.Fill
        txtExtra.Multiline = True
        txtExtra.ScrollBars = ScrollBars.Vertical
        txtExtra.PlaceholderText = "Örn. özel test adı, kriter veya yöntem..."
        txtExtra.Margin = New Padding(0)
        AddHandler txtExtra.TextChanged, Sub() UpdateSelectedLabel()
        extraPanel.Controls.Add(txtExtra, 0, 1)

        Dim footer As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.RightToLeft,
            .WrapContents = False,
            .BackColor = Color.White,
            .Padding = New Padding(8, 10, 8, 8),
            .Margin = New Padding(0)
        }
        root.Controls.Add(footer, 0, 4)

        Dim btnOk As New Button()
        ConfigureButton(btnOk, "Seçimi Uygula", 135, Color.FromArgb(31, 71, 126), Color.White)
        AddHandler btnOk.Click, AddressOf Ok_Click

        Dim btnCancel As New Button()
        ConfigureButton(btnCancel, "Vazgeç", 100, Color.White, Color.FromArgb(35, 50, 70))
        btnCancel.DialogResult = DialogResult.Cancel

        Dim btnClear As New Button()
        ConfigureButton(btnClear, "Temizle", 100, Color.White, Color.DarkRed)
        AddHandler btnClear.Click, AddressOf Clear_Click

        footer.Controls.AddRange({btnCancel, btnOk, btnClear})
        AcceptButton = btnOk
        CancelButton = btnCancel
    End Sub

    Private Function BuildTitledPanel(title As String) As TableLayoutPanel
        Dim panel As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .BackColor = Color.White,
            .Padding = New Padding(8),
            .Margin = New Padding(0)
        }
        panel.RowStyles.Add(New RowStyle(SizeType.Absolute, 30.0F))
        panel.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        panel.Controls.Add(New Label() With {
            .Dock = DockStyle.Fill,
            .Text = title,
            .ForeColor = Color.FromArgb(31, 71, 126),
            .Font = New Font("Segoe UI", 9.5F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0)
        Return panel
    End Function

    Private Sub ConfigureCatalogGrid()
        ConfigureGridBase(gridCatalog)
        gridCatalog.Columns.Add(New DataGridViewTextBoxColumn() With {
            .Name = "SortNo",
            .HeaderText = "Sıra",
            .Width = 62,
            .ReadOnly = True
        })
        gridCatalog.Columns.Add(New DataGridViewTextBoxColumn() With {
            .Name = "TestName",
            .HeaderText = "Test",
            .Width = 190,
            .ReadOnly = True
        })
        gridCatalog.Columns.Add(New DataGridViewTextBoxColumn() With {
            .Name = "Description",
            .HeaderText = "Açıklama / Kriter",
            .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            .ReadOnly = True
        })
        AddHandler gridCatalog.CellDoubleClick,
            Sub(sender, e)
                If e.RowIndex >= 0 Then AddCatalogSelection()
            End Sub
    End Sub

    Private Sub ConfigureSelectedGrid()
        ConfigureGridBase(gridSelected)
        gridSelected.Columns.Add(New DataGridViewTextBoxColumn() With {
            .Name = "SortNo",
            .HeaderText = "Sıra",
            .Width = 62,
            .ReadOnly = True
        })
        gridSelected.Columns.Add(New DataGridViewTextBoxColumn() With {
            .Name = "TestName",
            .HeaderText = "Seçilen Test",
            .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            .ReadOnly = True
        })
        AddHandler gridSelected.CellDoubleClick,
            Sub(sender, e)
                If e.RowIndex >= 0 Then RemoveSelected()
            End Sub
        gridSelected.AllowDrop = True
        AddHandler gridSelected.MouseDown, AddressOf SelectedGrid_MouseDown
        AddHandler gridSelected.MouseMove, AddressOf SelectedGrid_MouseMove
        AddHandler gridSelected.MouseUp, AddressOf SelectedGrid_MouseUp
        AddHandler gridSelected.DragOver, AddressOf SelectedGrid_DragOver
        AddHandler gridSelected.DragDrop, AddressOf SelectedGrid_DragDrop
        AddHandler gridSelected.KeyDown, AddressOf SelectedGrid_KeyDown
    End Sub

    Private Shared Sub ConfigureGridBase(targetGrid As DataGridView)
        targetGrid.Dock = DockStyle.Fill
        targetGrid.AllowUserToAddRows = False
        targetGrid.AllowUserToDeleteRows = False
        targetGrid.AllowUserToResizeRows = False
        targetGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells
        targetGrid.BackgroundColor = Color.White
        targetGrid.BorderStyle = BorderStyle.FixedSingle
        targetGrid.ColumnHeadersHeight = 34
        targetGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
        targetGrid.MultiSelect = False
        targetGrid.ReadOnly = True
        targetGrid.RowHeadersVisible = False
        targetGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        targetGrid.EnableHeadersVisualStyles = False
        targetGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(220, 232, 247)
        targetGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(31, 50, 75)
        targetGrid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        targetGrid.Columns.Clear()
    End Sub

    Private Sub LoadInitialSelection(initialValue As String)
        selectedTestNames.Clear()
        Dim extras As New List(Of String)()

        For Each part In SplitTests(initialValue)
            Dim catalogItem = catalogItems.FirstOrDefault(Function(item) String.Equals(item.TestName.Trim(), part, StringComparison.OrdinalIgnoreCase))
            If catalogItem IsNot Nothing Then
                selectedTestNames.Add(catalogItem.TestName.Trim())
            Else
                extras.Add(part)
            End If
        Next

        txtExtra.Text = String.Join("; ", extras)
    End Sub

    Private Sub RefreshCatalogGrid()
        If gridCatalog Is Nothing Then Return

        Dim filter = txtSearch.Text.Trim().ToUpperInvariant()
        gridCatalog.Rows.Clear()

        For Each item In catalogItems
            Dim testName = item.TestName.Trim()
            Dim description = If(item.Description, "").Trim()
            If filter <> "" AndAlso
               Not testName.ToUpperInvariant().Contains(filter) AndAlso
               Not description.ToUpperInvariant().Contains(filter) Then
                Continue For
            End If

            Dim rowIndex = gridCatalog.Rows.Add(item.SortNo, testName, description)
            gridCatalog.Rows(rowIndex).Tag = item
        Next
    End Sub

    Private Sub RefreshSelectedGrid(Optional preferredIndex As Integer = -1)
        gridSelected.Rows.Clear()
        For index As Integer = 0 To selectedTestNames.Count - 1
            Dim rowIndex = gridSelected.Rows.Add((index + 1).ToString(), selectedTestNames(index))
            gridSelected.Rows(rowIndex).Tag = index
        Next

        If gridSelected.Rows.Count > 0 Then
            Dim safeIndex = Math.Max(0, Math.Min(gridSelected.Rows.Count - 1, preferredIndex))
            If preferredIndex < 0 Then safeIndex = gridSelected.Rows.Count - 1
            gridSelected.CurrentCell = gridSelected.Rows(safeIndex).Cells(0)
        End If
        UpdateSelectedLabel()
    End Sub

    Private Sub AddCatalogSelection()
        Dim item = SelectedCatalogItem()
        If item Is Nothing Then
            MessageBox.Show("Eklenecek testi seçin.", "Test Seçimi", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim insertIndex = GetInsertIndexAfterCurrentSelection()
        selectedTestNames.Insert(insertIndex, item.TestName.Trim())
        RefreshSelectedGrid(insertIndex)
    End Sub

    Private Sub AddGroupSelection()
        Dim groupItem = TryCast(cboGroups.SelectedItem, TestGroupItem)
        If groupItem Is Nothing Then
            MessageBox.Show("Eklenecek test grubunu seçin.", "Test Grubu", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim groupTests = SplitTests(groupItem.TestsText)
        If groupTests.Count = 0 Then
            MessageBox.Show("Seçili grubun içinde test bulunamadı.", "Test Grubu", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim insertIndex = GetInsertIndexAfterCurrentSelection()
        selectedTestNames.InsertRange(insertIndex, groupTests)
        RefreshSelectedGrid(insertIndex + groupTests.Count - 1)
    End Sub

    Private Function SelectedCatalogItem() As TestCatalogItem
        If gridCatalog.CurrentRow Is Nothing Then Return Nothing
        Return TryCast(gridCatalog.CurrentRow.Tag, TestCatalogItem)
    End Function

    Private Function SelectedSequenceIndex() As Integer
        If gridSelected.CurrentRow Is Nothing OrElse gridSelected.CurrentRow.Tag Is Nothing Then Return -1
        Return CInt(gridSelected.CurrentRow.Tag)
    End Function

    Private Function GetInsertIndexAfterCurrentSelection() As Integer
        Dim index = SelectedSequenceIndex()
        If index < 0 Then Return selectedTestNames.Count
        Return Math.Max(0, Math.Min(selectedTestNames.Count, index + 1))
    End Function

    Private Sub RemoveSelected()
        Dim index = SelectedSequenceIndex()
        If index < 0 OrElse index >= selectedTestNames.Count Then Return

        selectedTestNames.RemoveAt(index)
        RefreshSelectedGrid(Math.Min(index, selectedTestNames.Count - 1))
    End Sub

    Private Sub MoveSelected(direction As Integer)
        Dim index = SelectedSequenceIndex()
        If index < 0 OrElse index >= selectedTestNames.Count Then Return

        Dim newIndex = index + If(direction < 0, -1, 1)
        If newIndex < 0 OrElse newIndex >= selectedTestNames.Count Then Return

        Dim value = selectedTestNames(index)
        selectedTestNames.RemoveAt(index)
        selectedTestNames.Insert(newIndex, value)
        RefreshSelectedGrid(newIndex)
    End Sub

    Private Sub MoveSelectedToEdge(moveToTop As Boolean)
        Dim index = SelectedSequenceIndex()
        If index < 0 OrElse index >= selectedTestNames.Count Then Return
        MoveSelectedToIndex(index, If(moveToTop, 0, selectedTestNames.Count))
    End Sub

    Private Sub MoveSelectedToIndex(sourceIndex As Integer, targetInsertIndex As Integer)
        If sourceIndex < 0 OrElse sourceIndex >= selectedTestNames.Count Then Return

        targetInsertIndex = Math.Max(0, Math.Min(selectedTestNames.Count, targetInsertIndex))
        If targetInsertIndex > sourceIndex Then targetInsertIndex -= 1
        If targetInsertIndex = sourceIndex Then Return

        Dim value = selectedTestNames(sourceIndex)
        selectedTestNames.RemoveAt(sourceIndex)
        selectedTestNames.Insert(targetInsertIndex, value)
        RefreshSelectedGrid(targetInsertIndex)
    End Sub

    Private Sub SelectedGrid_MouseDown(sender As Object, e As MouseEventArgs)
        dragSourceIndex = -1
        dragStartPoint = Point.Empty
        If e.Button <> MouseButtons.Left Then Return

        Dim hit = gridSelected.HitTest(e.X, e.Y)
        If hit.RowIndex < 0 Then Return

        dragSourceIndex = hit.RowIndex
        dragStartPoint = e.Location
        gridSelected.CurrentCell = gridSelected.Rows(hit.RowIndex).Cells(0)
    End Sub

    Private Sub SelectedGrid_MouseMove(sender As Object, e As MouseEventArgs)
        If e.Button <> MouseButtons.Left OrElse dragSourceIndex < 0 Then Return

        Dim dragSize = SystemInformation.DragSize
        Dim dragRectangle As New Rectangle(
            dragStartPoint.X - (dragSize.Width \ 2),
            dragStartPoint.Y - (dragSize.Height \ 2),
            dragSize.Width,
            dragSize.Height)

        If Not dragRectangle.Contains(e.Location) Then
            gridSelected.DoDragDrop(dragSourceIndex, DragDropEffects.Move)
        End If
    End Sub

    Private Sub SelectedGrid_MouseUp(sender As Object, e As MouseEventArgs)
        dragSourceIndex = -1
        dragStartPoint = Point.Empty
    End Sub

    Private Sub SelectedGrid_DragOver(sender As Object, e As DragEventArgs)
        e.Effect = If(e.Data IsNot Nothing AndAlso e.Data.GetDataPresent(GetType(Integer)), DragDropEffects.Move, DragDropEffects.None)
    End Sub

    Private Sub SelectedGrid_DragDrop(sender As Object, e As DragEventArgs)
        If e.Data Is Nothing OrElse Not e.Data.GetDataPresent(GetType(Integer)) Then Return

        Dim sourceIndex = CInt(e.Data.GetData(GetType(Integer)))
        Dim clientPoint = gridSelected.PointToClient(New Point(e.X, e.Y))
        Dim hit = gridSelected.HitTest(clientPoint.X, clientPoint.Y)
        Dim targetInsertIndex = If(hit.RowIndex >= 0, hit.RowIndex, selectedTestNames.Count)

        MoveSelectedToIndex(sourceIndex, targetInsertIndex)
        dragSourceIndex = -1
        dragStartPoint = Point.Empty
    End Sub

    Private Sub SelectedGrid_KeyDown(sender As Object, e As KeyEventArgs)
        If e.Alt AndAlso e.KeyCode = Keys.Up Then
            MoveSelected(-1)
            e.Handled = True
        ElseIf e.Alt AndAlso e.KeyCode = Keys.Down Then
            MoveSelected(1)
            e.Handled = True
        ElseIf e.Control AndAlso e.KeyCode = Keys.Home Then
            MoveSelectedToEdge(True)
            e.Handled = True
        ElseIf e.Control AndAlso e.KeyCode = Keys.End Then
            MoveSelectedToEdge(False)
            e.Handled = True
        ElseIf e.KeyCode = Keys.Delete Then
            RemoveSelected()
            e.Handled = True
        End If
    End Sub

    Private Sub Ok_Click(sender As Object, e As EventArgs)
        SelectedTestsText = BuildSelectedText()
        DialogResult = DialogResult.OK
        Close()
    End Sub

    Private Sub Clear_Click(sender As Object, e As EventArgs)
        selectedTestNames.Clear()
        txtExtra.Clear()
        RefreshSelectedGrid()
    End Sub

    Private Function BuildSelectedText() As String
        Dim result As New List(Of String)(selectedTestNames)
        result.AddRange(SplitTests(txtExtra.Text))
        Return String.Join("; ", result)
    End Function

    Private Sub UpdateSelectedLabel()
        Dim extraCount = SplitTests(txtExtra.Text).Count
        lblSelected.Text = "Seçili adım: " & (selectedTestNames.Count + extraCount).ToString()
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

    Private Shared Sub ConfigureButton(button As Button, text As String, width As Integer, backColor As Color, foreColor As Color)
        button.Text = text
        button.Width = width
        button.Height = 34
        button.BackColor = backColor
        button.ForeColor = foreColor
        button.FlatStyle = FlatStyle.Flat
        button.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        button.Margin = New Padding(0, 4, 0, 4)
        button.Cursor = Cursors.Hand
        button.UseVisualStyleBackColor = False
    End Sub
End Class
