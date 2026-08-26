Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Public Class ColumnSelectorForm
    Inherits Form

    Private ReadOnly checkedList As CheckedListBox
    Public ReadOnly Property SelectedColumns As List(Of String)

    Public Sub New(allColumns As IEnumerable(Of String), visibleColumns As HashSet(Of String))
        Me.Text = "Görüntülenecek Sütunları Seç"
        Me.StartPosition = FormStartPosition.CenterParent
        Me.Size = New Size(520, 610)
        Me.MinimumSize = New Size(500, 560)
        Me.Font = New Font("Segoe UI", 9.0F)
        Me.BackColor = Color.FromArgb(243, 246, 250)

        AppIconHelper.ApplyIcon(Me)
        SelectedColumns = New List(Of String)()
        checkedList = New CheckedListBox()

        BuildUi(allColumns, visibleColumns)
    End Sub

    Private Sub BuildUi(allColumns As IEnumerable(Of String), visibleColumns As HashSet(Of String))
        Dim root As New TableLayoutPanel()
        root.Dock = DockStyle.Fill
        root.ColumnCount = 1
        root.RowCount = 3
        root.Padding = New Padding(14)
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 52))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 48))
        Me.Controls.Add(root)

        Dim lblTitle As New Label()
        lblTitle.Text = "Ana listede görünecek sütunları seçin ve sırasını ayarlayın"
        lblTitle.Dock = DockStyle.Fill
        lblTitle.Font = New Font("Segoe UI", 10.0F, FontStyle.Bold)
        lblTitle.ForeColor = Color.FromArgb(31, 78, 121)
        lblTitle.TextAlign = ContentAlignment.MiddleLeft
        root.Controls.Add(lblTitle, 0, 0)

        Dim middle As New TableLayoutPanel()
        middle.Dock = DockStyle.Fill
        middle.ColumnCount = 2
        middle.RowCount = 1
        middle.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        middle.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 92))
        root.Controls.Add(middle, 0, 1)

        checkedList.Dock = DockStyle.Fill
        checkedList.CheckOnClick = True
        checkedList.BorderStyle = BorderStyle.FixedSingle

        For Each col In allColumns
            Dim idx = checkedList.Items.Add(col)
            checkedList.SetItemChecked(idx, visibleColumns.Contains(col))
        Next

        middle.Controls.Add(checkedList, 0, 0)

        Dim orderPanel As New FlowLayoutPanel()
        orderPanel.Dock = DockStyle.Fill
        orderPanel.FlowDirection = FlowDirection.TopDown
        orderPanel.WrapContents = False
        orderPanel.Padding = New Padding(8, 0, 0, 0)
        middle.Controls.Add(orderPanel, 1, 0)

        Dim btnUp As New Button()
        btnUp.Text = "Yukarı"
        btnUp.Size = New Size(76, 34)
        btnUp.BackColor = Color.FromArgb(238, 242, 247)
        btnUp.ForeColor = Color.FromArgb(52, 64, 84)
        btnUp.FlatStyle = FlatStyle.Flat
        btnUp.FlatAppearance.BorderSize = 0
        AddHandler btnUp.Click, Sub() MoveSelectedItem(-1)
        orderPanel.Controls.Add(btnUp)

        Dim btnDown As New Button()
        btnDown.Text = "Aşağı"
        btnDown.Size = New Size(76, 34)
        btnDown.Margin = New Padding(3, 8, 3, 3)
        btnDown.BackColor = Color.FromArgb(238, 242, 247)
        btnDown.ForeColor = Color.FromArgb(52, 64, 84)
        btnDown.FlatStyle = FlatStyle.Flat
        btnDown.FlatAppearance.BorderSize = 0
        AddHandler btnDown.Click, Sub() MoveSelectedItem(1)
        orderPanel.Controls.Add(btnDown)

        Dim lblHint As New Label()
        lblHint.Text = "Seçili sütunu yukarı/aşağı taşıyın."
        lblHint.Size = New Size(80, 80)
        lblHint.Margin = New Padding(3, 12, 3, 3)
        lblHint.ForeColor = Color.FromArgb(102, 112, 133)
        orderPanel.Controls.Add(lblHint)

        Dim footer As New FlowLayoutPanel()
        footer.Dock = DockStyle.Fill
        footer.FlowDirection = FlowDirection.RightToLeft
        footer.WrapContents = False
        footer.Padding = New Padding(0, 8, 0, 0)
        root.Controls.Add(footer, 0, 2)

        Dim btnOk As New Button()
        btnOk.Text = "Uygula"
        btnOk.Size = New Size(100, 32)
        btnOk.BackColor = Color.FromArgb(15, 123, 63)
        btnOk.ForeColor = Color.White
        btnOk.FlatStyle = FlatStyle.Flat
        btnOk.FlatAppearance.BorderSize = 0
        AddHandler btnOk.Click, AddressOf BtnOk_Click
        footer.Controls.Add(btnOk)

        Dim btnCancel As New Button()
        btnCancel.Text = "Vazgeç"
        btnCancel.Size = New Size(100, 32)
        btnCancel.Margin = New Padding(0, 0, 10, 0)
        btnCancel.BackColor = Color.FromArgb(238, 242, 247)
        btnCancel.ForeColor = Color.FromArgb(52, 64, 84)
        btnCancel.FlatStyle = FlatStyle.Flat
        btnCancel.FlatAppearance.BorderSize = 0
        btnCancel.DialogResult = DialogResult.Cancel
        footer.Controls.Add(btnCancel)

        Me.AcceptButton = btnOk
        Me.CancelButton = btnCancel
    End Sub

    Private Sub MoveSelectedItem(direction As Integer)
        Dim currentIndex = checkedList.SelectedIndex

        If currentIndex < 0 Then Return

        Dim newIndex = currentIndex + direction

        If newIndex < 0 OrElse newIndex >= checkedList.Items.Count Then Return

        Dim itemText = Convert.ToString(checkedList.Items(currentIndex))
        Dim isChecked = checkedList.GetItemChecked(currentIndex)

        checkedList.Items.RemoveAt(currentIndex)
        checkedList.Items.Insert(newIndex, itemText)
        checkedList.SetItemChecked(newIndex, isChecked)
        checkedList.SelectedIndex = newIndex
    End Sub

    Private Sub BtnOk_Click(sender As Object, e As EventArgs)
        SelectedColumns.Clear()

        For i As Integer = 0 To checkedList.Items.Count - 1
            If checkedList.GetItemChecked(i) Then
                SelectedColumns.Add(Convert.ToString(checkedList.Items(i)))
            End If
        Next

        If SelectedColumns.Count = 0 Then
            MessageBox.Show("En az bir sütun seçmelisiniz.", "Sütun Seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Me.DialogResult = DialogResult.OK
        Me.Close()
    End Sub
End Class
