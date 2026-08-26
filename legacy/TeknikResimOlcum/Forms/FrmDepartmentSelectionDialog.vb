Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmDepartmentSelectionDialog
    Inherits Form

    Private ReadOnly departmentList As New CheckedListBox()
    Private ReadOnly availableDepartments As List(Of String)

    Public ReadOnly Property SelectedDepartments As List(Of String)
        Get
            Return departmentList.CheckedItems.
                Cast(Of Object)().
                Select(Function(item) Convert.ToString(item).Trim()).
                Where(Function(item) item <> "").
                ToList()
        End Get
    End Property

    Public Sub New(options As IEnumerable(Of String), selected As IEnumerable(Of String))
        AppIconService.Apply(Me)
        availableDepartments = If(options, Enumerable.Empty(Of String)()).
            Select(Function(item) If(item, "").Trim()).
            Where(Function(item) item <> "").
            Distinct(StringComparer.OrdinalIgnoreCase).
            ToList()

        Text = "Talep Eden Bölümleri Seç"
        StartPosition = FormStartPosition.CenterParent
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ShowInTaskbar = False
        ClientSize = New Size(460, 390)
        MinimumSize = New Size(420, 340)
        BackColor = Color.FromArgb(244, 247, 251)
        Font = New Font("Segoe UI", 9.0F)

        BuildScreen(selected)
    End Sub

    Private Sub BuildScreen(selected As IEnumerable(Of String))
        Dim selectedSet As New HashSet(Of String)(If(selected, Enumerable.Empty(Of String)()), StringComparer.OrdinalIgnoreCase)
        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .Padding = New Padding(12),
            .BackColor = BackColor
        }
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 54.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 50.0F))
        Controls.Add(root)

        Dim info As New Label() With {
            .Text = "Talebe ortak olan bir veya daha fazla bölümü işaretleyin.",
            .Dock = DockStyle.Fill,
            .BackColor = Color.FromArgb(231, 239, 249),
            .ForeColor = Color.FromArgb(31, 71, 126),
            .Padding = New Padding(12, 0, 12, 0),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        }
        root.Controls.Add(info, 0, 0)

        departmentList.Dock = DockStyle.Fill
        departmentList.CheckOnClick = True
        departmentList.IntegralHeight = False
        departmentList.BorderStyle = BorderStyle.FixedSingle
        departmentList.Font = New Font("Segoe UI", 10.0F)
        departmentList.ItemHeight = 30
        departmentList.Margin = New Padding(0, 10, 0, 10)
        For Each department In availableDepartments
            Dim index = departmentList.Items.Add(department)
            departmentList.SetItemChecked(index, selectedSet.Contains(department))
        Next
        root.Controls.Add(departmentList, 0, 1)

        Dim actions As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.RightToLeft,
            .WrapContents = False,
            .Padding = New Padding(0, 8, 0, 0)
        }
        Dim btnApply As New Button()
        ConfigureButton(btnApply, "Seçimi Uygula", 135, Color.FromArgb(31, 71, 126), Color.White)
        Dim btnCancel As New Button()
        ConfigureButton(btnCancel, "Vazgeç", 90, Color.White, Color.FromArgb(35, 45, 60))
        Dim btnClear As New Button()
        ConfigureButton(btnClear, "Temizle", 90, Color.White, Color.FromArgb(35, 45, 60))
        actions.Controls.AddRange({btnApply, btnCancel, btnClear})
        root.Controls.Add(actions, 0, 2)

        AddHandler btnApply.Click,
            Sub()
                If SelectedDepartments.Count = 0 Then
                    MessageBox.Show("En az bir talep eden bölüm seçilmelidir.", "Bölüm seçimi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    Return
                End If
                DialogResult = DialogResult.OK
                Close()
            End Sub
        AddHandler btnCancel.Click, Sub() DialogResult = DialogResult.Cancel
        AddHandler btnClear.Click,
            Sub()
                For index As Integer = 0 To departmentList.Items.Count - 1
                    departmentList.SetItemChecked(index, False)
                Next
            End Sub

        AcceptButton = btnApply
        CancelButton = btnCancel
    End Sub

    Private Shared Sub ConfigureButton(button As Button, text As String, width As Integer, backColor As Color, foreColor As Color)
        button.Text = text
        button.Width = width
        button.Height = 34
        button.BackColor = backColor
        button.ForeColor = foreColor
        button.FlatStyle = FlatStyle.Flat
        button.FlatAppearance.BorderColor = Color.FromArgb(150, 165, 185)
        button.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        button.Margin = New Padding(8, 0, 0, 0)
        button.UseVisualStyleBackColor = False
    End Sub
End Class
