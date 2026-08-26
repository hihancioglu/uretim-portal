Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Public Class EmailDraftSelectionForm
    Inherits Form

    Private ReadOnly checkedList As CheckedListBox
    Private ReadOnly txtFilter As TextBox
    Private ReadOnly allItems As List(Of MailDraftRowItem)
    Private ReadOnly checkedIds As New HashSet(Of Integer)()
    Private isLoadingList As Boolean = False

    Public ReadOnly Property SelectedInternalIds As List(Of Integer)

    Public Sub New(sourceItems As IEnumerable(Of MailDraftRowItem))
        Me.Text = "E-posta İçin Satır Seç"
        Me.StartPosition = FormStartPosition.CenterParent
        Me.Size = New Size(820, 570)
        Me.MinimumSize = New Size(720, 500)
        Me.Font = New Font("Segoe UI", 9.0F)
        Me.BackColor = Color.FromArgb(243, 246, 250)

        AppIconHelper.ApplyIcon(Me)
        ' Liste son kayıtlardan başlayarak gösterilir.
        allItems = sourceItems.Reverse().ToList()

        SelectedInternalIds = New List(Of Integer)()
        checkedList = New CheckedListBox()
        txtFilter = New TextBox()

        BuildUi()
    End Sub

    Private Sub BuildUi()
        Dim root As New TableLayoutPanel()
        root.Dock = DockStyle.Fill
        root.ColumnCount = 1
        root.RowCount = 4
        root.Padding = New Padding(14)
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 42))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 36))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 48))
        Me.Controls.Add(root)

        Dim lblTitle As New Label()
        lblTitle.Text = "E-posta taslağına eklenecek kayıtları seçin"
        lblTitle.Dock = DockStyle.Fill
        lblTitle.Font = New Font("Segoe UI", 10.0F, FontStyle.Bold)
        lblTitle.ForeColor = Color.FromArgb(31, 78, 121)
        lblTitle.TextAlign = ContentAlignment.MiddleLeft
        root.Controls.Add(lblTitle, 0, 0)

        txtFilter.Dock = DockStyle.Fill
        txtFilter.PlaceholderText = "Sayaç adı, sipariş yeri, iş emri no veya sıra ara..."
        txtFilter.Margin = New Padding(0, 0, 0, 8)
        AddHandler txtFilter.TextChanged, AddressOf TxtFilter_TextChanged
        root.Controls.Add(txtFilter, 0, 1)

        checkedList.Dock = DockStyle.Fill
        checkedList.CheckOnClick = True
        checkedList.BorderStyle = BorderStyle.FixedSingle
        checkedList.HorizontalScrollbar = True
        AddHandler checkedList.ItemCheck, AddressOf CheckedList_ItemCheck
        root.Controls.Add(checkedList, 0, 2)

        Dim footer As New FlowLayoutPanel()
        footer.Dock = DockStyle.Fill
        footer.FlowDirection = FlowDirection.RightToLeft
        footer.WrapContents = False
        footer.Padding = New Padding(0, 8, 0, 0)
        root.Controls.Add(footer, 0, 3)

        Dim btnOk As New Button()
        btnOk.Text = "Taslak Hazırla"
        btnOk.Size = New Size(120, 32)
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

        RebuildList()

        AddHandler Me.Shown, Sub()
                                 txtFilter.Focus()
                             End Sub
    End Sub

    Private Sub TxtFilter_TextChanged(sender As Object, e As EventArgs)
        SyncCheckedStateFromVisibleList()
        RebuildList()
    End Sub

    Private Sub CheckedList_ItemCheck(sender As Object, e As ItemCheckEventArgs)
        If isLoadingList Then Return

        Dim item = TryCast(checkedList.Items(e.Index), MailDraftRowItem)
        If item Is Nothing Then Return

        If e.NewValue = CheckState.Checked Then
            checkedIds.Add(item.InternalId)
        Else
            checkedIds.Remove(item.InternalId)
        End If
    End Sub

    Private Sub SyncCheckedStateFromVisibleList()
        If checkedList Is Nothing Then Return

        For i As Integer = 0 To checkedList.Items.Count - 1
            Dim item = TryCast(checkedList.Items(i), MailDraftRowItem)
            If item Is Nothing Then Continue For

            If checkedList.GetItemChecked(i) Then
                checkedIds.Add(item.InternalId)
            Else
                checkedIds.Remove(item.InternalId)
            End If
        Next
    End Sub

    Private Sub RebuildList()
        isLoadingList = True

        Try
            checkedList.Items.Clear()

            Dim q = If(txtFilter.Text, "").Trim()
            Dim filtered = allItems.AsEnumerable()

            If q.Length > 0 Then
                filtered = filtered.Where(Function(x) TextContains(x.ToString(), q))
            End If

            For Each item In filtered
                Dim idx = checkedList.Items.Add(item)
                checkedList.SetItemChecked(idx, checkedIds.Contains(item.InternalId))
            Next

            ' Hiçbir satır işaretli başlamaz. Liste son kayıtlardan başladığı için TopIndex 0 yeterlidir.
            If checkedList.Items.Count > 0 Then
                checkedList.TopIndex = 0
                checkedList.SelectedIndex = -1
            End If
        Finally
            isLoadingList = False
        End Try
    End Sub

    Private Function TextContains(source As String, query As String) As Boolean
        If source Is Nothing Then source = ""
        If query Is Nothing Then query = ""

        Return source.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
    End Function

    Private Sub BtnOk_Click(sender As Object, e As EventArgs)
        SyncCheckedStateFromVisibleList()

        SelectedInternalIds.Clear()
        SelectedInternalIds.AddRange(checkedIds)

        If SelectedInternalIds.Count = 0 Then
            MessageBox.Show("En az bir satır seçmelisiniz.", "Satır Seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Me.DialogResult = DialogResult.OK
        Me.Close()
    End Sub
End Class
