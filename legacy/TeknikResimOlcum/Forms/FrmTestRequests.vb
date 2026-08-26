Imports System.Data
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmTestRequests
    Inherits Form

    Private ReadOnly grid As New DataGridView()
    Private ReadOnly txtSearch As New TextBox()
    Private ReadOnly cboStatus As New ComboBox()
    Private ReadOnly cboDepartment As New ComboBox()
    Private ReadOnly lblSummary As New Label()
    Private currentRows As New List(Of Dictionary(Of String, String))()

    Public Sub New()
        AuthorizationService.Require(AppState.CanOpenTestRequests, "Test Talep Yönetimi")
        AppIconService.Apply(Me)
        Text = "Test / Talep Yönetimi"
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        Size = New Size(1420, 760)
        MinimumSize = New Size(900, 540)
        BackColor = Color.FromArgb(243, 247, 252)
        Font = New Font("Segoe UI", 9.0F)

        BuildScreen()
        lblSummary.Text = "Test talepleri yükleniyor..."
        AddHandler Shown, AddressOf FrmTestRequests_Shown
    End Sub

    Private Sub FrmTestRequests_Shown(sender As Object, e As EventArgs)
        BeginInvoke(CType(Sub() LoadGrid(), MethodInvoker))
    End Sub

    Private Sub BuildScreen()
        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .Padding = New Padding(10),
            .BackColor = BackColor
        }
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 92.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 46.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Controls.Add(root)

        Dim toolbar As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = True,
            .AutoScroll = False,
            .Padding = New Padding(8, 8, 8, 5),
            .BackColor = Color.White,
            .Margin = New Padding(0, 0, 0, 6)
        }
        Dim btnNew As New Button()
        ConfigureButton(btnNew, "Yeni Test Talebi", 145, Color.FromArgb(15, 123, 63), Color.White)
        btnNew.Visible = AppState.CanCreateTestRequest
        AddHandler btnNew.Click, AddressOf New_Click

        Dim btnDetail As New Button()
        ConfigureButton(btnDetail, "Detay", 95, Color.FromArgb(31, 71, 126), Color.White)
        AddHandler btnDetail.Click, AddressOf Detail_Click

        Dim btnDelete As New Button()
        ConfigureButton(btnDelete, "Seçili Kaydı Sil", 125, Color.FromArgb(255, 230, 230), Color.DarkRed)
        btnDelete.Visible = AppState.CanDeleteTestRequests
        AddHandler btnDelete.Click, AddressOf Delete_Click

        Dim btnRefresh As New Button()
        ConfigureButton(btnRefresh, "Yenile", 95, Color.White, Color.FromArgb(35, 50, 70))
        AddHandler btnRefresh.Click, Sub() LoadGrid()

        Dim btnCatalog As New Button()
        ConfigureButton(btnCatalog, "Test Listesi", 115, Color.FromArgb(232, 242, 255), Color.FromArgb(31, 71, 126))
        btnCatalog.Visible = AppState.IsAdmin
        AddHandler btnCatalog.Click, AddressOf Catalog_Click

        Dim btnEmailRecipients As New Button()
        ConfigureButton(btnEmailRecipients, "Mail Alıcıları", 125, Color.FromArgb(255, 247, 230), Color.FromArgb(120, 70, 0))
        btnEmailRecipients.Visible = AppState.CanManageTestRequestEmailRecipients
        AddHandler btnEmailRecipients.Click, AddressOf EmailRecipients_Click

        Dim lblSearch As New Label() With {.Text = "Arama", .Width = 48, .Height = 34, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(16, 0, 0, 0)}
        txtSearch.Width = 300
        txtSearch.Height = 34
        txtSearch.PlaceholderText = "talep no / ürün / test / kullanıcı / rapor"
        txtSearch.Margin = New Padding(4, 3, 10, 0)
        AddHandler txtSearch.TextChanged, Sub() LoadGrid()

        Dim lblStatus As New Label() With {.Text = "Durum", .Width = 48, .Height = 34, .TextAlign = ContentAlignment.MiddleLeft}
        cboStatus.Width = 145
        cboStatus.DropDownStyle = ComboBoxStyle.DropDownList
        cboStatus.Items.AddRange({"TÜMÜ", "YENİ", "İŞLEMDE", "TAMAMLANDI", "İPTAL"})
        cboStatus.SelectedIndex = 0
        cboStatus.Margin = New Padding(4, 3, 10, 0)
        AddHandler cboStatus.SelectedIndexChanged, Sub() LoadGrid()

        Dim lblDepartment As New Label() With {.Text = "Bölüm", .Width = 48, .Height = 34, .TextAlign = ContentAlignment.MiddleLeft}
        cboDepartment.Width = 170
        cboDepartment.DropDownStyle = ComboBoxStyle.DropDownList
        cboDepartment.Items.AddRange({"TÜMÜ", "GKK", "MEKANİZMA", "PLASTİKHANE", "KARTLI SAYAÇ", "ELEKTRİK SAYACI", "KALİTE LAB.", "SAYAÇ MONTAJ", "TALAŞLI İMALAT", "DİĞER"})
        cboDepartment.SelectedIndex = 0
        cboDepartment.Margin = New Padding(4, 3, 5, 0)
        AddHandler cboDepartment.SelectedIndexChanged, Sub() LoadGrid()

        toolbar.Controls.AddRange({btnNew, btnDetail, btnDelete, btnRefresh, btnCatalog, btnEmailRecipients, lblSearch, txtSearch, lblStatus, cboStatus, lblDepartment, cboDepartment})
        root.Controls.Add(toolbar, 0, 0)

        lblSummary.Dock = DockStyle.Fill
        lblSummary.BackColor = Color.FromArgb(229, 238, 249)
        lblSummary.ForeColor = Color.FromArgb(31, 71, 126)
        lblSummary.Font = New Font("Segoe UI", 9.5F, FontStyle.Bold)
        lblSummary.TextAlign = ContentAlignment.MiddleLeft
        lblSummary.Padding = New Padding(12, 0, 0, 0)
        lblSummary.Margin = New Padding(0, 0, 0, 6)
        root.Controls.Add(lblSummary, 0, 1)

        ConfigureGrid()
        root.Controls.Add(grid, 0, 2)
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
        grid.GridColor = Color.FromArgb(220, 226, 234)
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(220, 232, 247)
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(31, 50, 75)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter
        grid.ColumnHeadersHeight = 42
        grid.RowTemplate.Height = 31
        grid.DefaultCellStyle.Padding = New Padding(4, 2, 4, 2)

        grid.Columns.Add(MakeColumn("StatusDisplay", "DURUM", 75, 7))
        grid.Columns.Add(MakeColumn("RequestId", "TALEP NO", 145, 12))
        grid.Columns.Add(MakeColumn("CreatedAtDisplay", "TALEP TARİHİ", 125, 10))
        grid.Columns.Add(MakeColumn("Priority", "ÖNCELİK", 80, 7))
        grid.Columns.Add(MakeColumn("RequestingDepartment", "TALEP EDEN BÖLÜM", 125, 10))
        grid.Columns.Add(MakeColumn("RequestedDepartment", "TALEP EDİLEN BÖLÜM", 125, 10))
        grid.Columns.Add(MakeColumn("ProductNameTrCode", "ÜRÜN ADI / TR NO", 175, 15))
        grid.Columns.Add(MakeColumn("RequestReason", "TALEP NEDENİ", 145, 12))
        grid.Columns.Add(MakeColumn("RequestedTests", "TALEP EDİLEN TEST", 210, 18))
        grid.Columns.Add(MakeColumn("DueDateDisplay", "TERMİN", 90, 8))
        grid.Columns.Add(MakeColumn("RequesterReportNo", "RAPOR / REF. NO", 115, 9))
        grid.Columns.Add(MakeColumn("CreatedBy", "TALEP EDEN", 105, 9))
        grid.Columns.Add(MakeColumn("AcceptedBy", "İŞLEME ALAN", 105, 9))
        grid.Columns.Add(MakeColumn("Result", "SONUÇ", 105, 9))
        grid.Columns.Add(MakeColumn("LabReportNo", "LAB. RAPOR NO", 110, 9))

        AddHandler grid.CellDoubleClick,
            Sub(sender, e)
                If e.RowIndex >= 0 Then OpenSelected()
            End Sub
        AddHandler grid.CellFormatting, AddressOf Grid_CellFormatting
    End Sub

    Private Sub LoadGrid(Optional requestIdToSelect As String = "")
        Try
            Dim selectedRequestId = If(requestIdToSelect, "").Trim()
            If selectedRequestId = "" AndAlso grid.CurrentRow IsNot Nothing Then
                Dim selectedRecord = TryCast(grid.CurrentRow.Tag, Dictionary(Of String, String))
                If selectedRecord IsNot Nothing Then
                    selectedRequestId = DataService.GetValue(selectedRecord, "RequestId").Trim()
                End If
            End If

            Dim allRows = DataService.GetTestRequests()
            Dim rows = allRows.AsEnumerable()

            Dim statusFilter = If(cboStatus.SelectedItem Is Nothing, "TÜMÜ", cboStatus.SelectedItem.ToString())
            If statusFilter <> "TÜMÜ" Then
                Dim wantedStatus = StatusCode(statusFilter)
                rows = rows.Where(Function(row) String.Equals(DataService.GetValue(row, "Status"), wantedStatus, StringComparison.OrdinalIgnoreCase))
            End If

            Dim departmentFilter = If(cboDepartment.SelectedItem Is Nothing, "TÜMÜ", cboDepartment.SelectedItem.ToString())
            If departmentFilter <> "TÜMÜ" Then
                rows = rows.Where(Function(row)
                                      Return TestRequestEmailNotificationService.ContainsDepartment(DataService.GetValue(row, "RequestingDepartment"), departmentFilter) OrElse
                                             String.Equals(DataService.GetValue(row, "RequestedDepartment"), departmentFilter, StringComparison.OrdinalIgnoreCase)
                                  End Function)
            End If

            Dim searchText = txtSearch.Text.Trim()
            If searchText <> "" Then
                Dim tokens = searchText.Split(New Char() {" "c, ";"c, ","c, "|"c}, StringSplitOptions.RemoveEmptyEntries)
                rows = rows.Where(
                    Function(row)
                        Dim haystack = String.Join(" ", DataService.TestRequestHeaders.Select(Function(header) DataService.GetValue(row, header))).ToUpperInvariant()
                        Return tokens.All(Function(token) haystack.Contains(token.ToUpperInvariant()))
                    End Function)
            End If

            currentRows = rows.
                OrderBy(Function(row) StatusSort(DataService.GetValue(row, "Status"))).
                ThenByDescending(Function(row) DataService.GetValue(row, "CreatedAt")).
                ToList()

            grid.Rows.Clear()
            For Each item In currentRows
                Dim index = grid.Rows.Add(
                    StatusDisplay(DataService.GetValue(item, "Status")),
                    DataService.GetValue(item, "RequestId"),
                    FormatDateTime(DataService.GetValue(item, "CreatedAt")),
                    DataService.GetValue(item, "Priority"),
                    TestRequestEmailNotificationService.FormatDepartmentList(DataService.GetValue(item, "RequestingDepartment")),
                    DataService.GetValue(item, "RequestedDepartment"),
                    DataService.GetValue(item, "ProductNameTrCode"),
                    DataService.GetValue(item, "RequestReason"),
                    DataService.GetValue(item, "RequestedTests"),
                    FormatDate(DataService.GetValue(item, "DueDate")),
                    DataService.GetValue(item, "RequesterReportNo"),
                    DataService.GetValue(item, "CreatedBy"),
                    DataService.GetValue(item, "AcceptedBy"),
                    DataService.GetValue(item, "Result"),
                    DataService.GetValue(item, "LabReportNo"))
                grid.Rows(index).Tag = item
            Next

            grid.ClearSelection()
            If selectedRequestId <> "" Then
                For Each gridRow As DataGridViewRow In grid.Rows
                    Dim rowRecord = TryCast(gridRow.Tag, Dictionary(Of String, String))
                    If rowRecord Is Nothing Then Continue For
                    If Not String.Equals(DataService.GetValue(rowRecord, "RequestId"),
                                         selectedRequestId,
                                         StringComparison.OrdinalIgnoreCase) Then Continue For

                    gridRow.Selected = True
                    If gridRow.Cells.Count > 0 Then grid.CurrentCell = gridRow.Cells(0)
                    Exit For
                Next
            End If

            Dim openCount = allRows.Where(Function(row) String.Equals(DataService.GetValue(row, "Status"), "OPEN", StringComparison.OrdinalIgnoreCase)).Count()
            Dim acceptedCount = allRows.Where(Function(row) String.Equals(DataService.GetValue(row, "Status"), "ACCEPTED", StringComparison.OrdinalIgnoreCase)).Count()
            Dim completedCount = allRows.Where(Function(row) String.Equals(DataService.GetValue(row, "Status"), "COMPLETED", StringComparison.OrdinalIgnoreCase)).Count()
            Dim overdueCount = allRows.Where(Function(row) IsOverdue(row)).Count()
            lblSummary.Text = "Gösterilen: " & currentRows.Count.ToString() & " / " & allRows.Count.ToString() &
                              "   |   Yeni: " & openCount.ToString() &
                              "   |   İşlemde: " & acceptedCount.ToString() &
                              "   |   Tamamlanan: " & completedCount.ToString() &
                              "   |   Termin Geçen: " & overdueCount.ToString()
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Test talepleri yüklenemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub Grid_CellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If e.RowIndex < 0 Then Return
        Dim row = grid.Rows(e.RowIndex)
        Dim record = TryCast(row.Tag, Dictionary(Of String, String))
        If record Is Nothing Then Return

        Dim status = DataService.GetValue(record, "Status").Trim().ToUpperInvariant()
        If status = "OPEN" Then
            row.DefaultCellStyle.BackColor = Color.FromArgb(255, 249, 225)
            row.DefaultCellStyle.ForeColor = Color.FromArgb(110, 79, 12)
        ElseIf status = "ACCEPTED" Then
            row.DefaultCellStyle.BackColor = Color.FromArgb(232, 242, 255)
            row.DefaultCellStyle.ForeColor = Color.FromArgb(31, 71, 126)
        ElseIf status = "COMPLETED" Then
            row.DefaultCellStyle.BackColor = Color.FromArgb(232, 247, 236)
            row.DefaultCellStyle.ForeColor = Color.DarkGreen
        Else
            row.DefaultCellStyle.BackColor = Color.FromArgb(241, 243, 245)
            row.DefaultCellStyle.ForeColor = Color.DimGray
        End If

        If IsOverdue(record) Then
            row.Cells("DueDateDisplay").Style.BackColor = Color.MistyRose
            row.Cells("DueDateDisplay").Style.ForeColor = Color.DarkRed
            row.Cells("DueDateDisplay").Style.Font = New Font(grid.Font, FontStyle.Bold)
        End If

        If IsUnsuitableResult(DataService.GetValue(record, "Result")) Then
            ApplyUnsuitableRowStyle(row)
        End If
    End Sub

    Private Sub ApplyUnsuitableRowStyle(row As DataGridViewRow)
        If row Is Nothing Then Return

        row.DefaultCellStyle.BackColor = Color.FromArgb(220, 53, 69)
        row.DefaultCellStyle.ForeColor = Color.White
        row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(160, 28, 42)
        row.DefaultCellStyle.SelectionForeColor = Color.White
        row.DefaultCellStyle.Font = New Font(grid.Font, FontStyle.Bold)

        For Each cell As DataGridViewCell In row.Cells
            cell.Style.BackColor = Color.FromArgb(220, 53, 69)
            cell.Style.ForeColor = Color.White
            cell.Style.SelectionBackColor = Color.FromArgb(160, 28, 42)
            cell.Style.SelectionForeColor = Color.White
            cell.Style.Font = New Font(grid.Font, FontStyle.Bold)
        Next
    End Sub

    Private Sub New_Click(sender As Object, e As EventArgs)
        Using detail As New FrmTestRequestDetail()
            detail.ShowDialog(Me)
            LoadGrid(detail.AffectedRequestId)
        End Using
    End Sub

    Private Sub Detail_Click(sender As Object, e As EventArgs)
        OpenSelected()
    End Sub

    Private Sub Delete_Click(sender As Object, e As EventArgs)
        Try
            AuthorizationService.Require(AppState.CanDeleteTestRequests, "Test Talebi Silme")

            If grid.CurrentRow Is Nothing Then
                MessageBox.Show("Lütfen silinecek test talebini seçin.", "Kayıt seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim record = TryCast(grid.CurrentRow.Tag, Dictionary(Of String, String))
            If record Is Nothing Then Return

            Dim requestId = DataService.GetValue(record, "RequestId")
            Dim product = DataService.GetValue(record, "ProductNameTrCode")
            Dim statusText = StatusDisplay(DataService.GetValue(record, "Status"))
            Dim confirmText = "Seçili test talebi silinecek." & Environment.NewLine & Environment.NewLine &
                              "Talep No: " & requestId & Environment.NewLine &
                              "Ürün / TR: " & If(product = "", "-", product) & Environment.NewLine &
                              "Durum: " & statusText & Environment.NewLine & Environment.NewLine &
                              "Bu işlem geri alınamaz. Devam edilsin mi?"

            If MessageBox.Show(confirmText, "Test talebi silinsin mi?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) <> DialogResult.Yes Then
                Return
            End If

            DataService.DeleteTestRequest(requestId)
            LoadGrid()
            MessageBox.Show("Test talebi silindi.", "Silme tamamlandı", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Test talebi silinemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub Catalog_Click(sender As Object, e As EventArgs)
        Using catalog As New FrmTestCatalogAdmin()
            catalog.ShowDialog(Me)
        End Using
    End Sub

    Private Sub EmailRecipients_Click(sender As Object, e As EventArgs)
        Try
            AuthorizationService.Require(AppState.CanManageTestRequestEmailRecipients, "Test Talep Mail Alıcıları")
            Using recipientsForm As New FrmTestRequestEmailRecipients()
                recipientsForm.ShowDialog(Me)
            End Using
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Mail alıcıları açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub OpenSelected()
        If grid.CurrentRow Is Nothing Then
            MessageBox.Show("Lütfen bir test talebi seçin.", "Kayıt seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Dim record = TryCast(grid.CurrentRow.Tag, Dictionary(Of String, String))
        If record Is Nothing Then Return
        Dim requestId = DataService.GetValue(record, "RequestId").Trim()
        Using detail As New FrmTestRequestDetail(record)
            detail.ShowDialog(Me)
            ' Detay penceresinde admin düzenlemesi, test ataması veya durum değişikliği
            ' yapılmış olabilir. Pencere hangi düğmeyle kapanırsa kapansın veriyi
            ' yeniden diskten okuyarak listede eski satır nesnesinin kalmasını önle.
            LoadGrid(requestId)
        End Using
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

    Private Shared Function StatusCode(display As String) As String
        Select Case display
            Case "YENİ" : Return "OPEN"
            Case "İŞLEMDE" : Return "ACCEPTED"
            Case "TAMAMLANDI" : Return "COMPLETED"
            Case "İPTAL" : Return "CANCELLED"
            Case Else : Return ""
        End Select
    End Function

    Private Shared Function StatusDisplay(status As String) As String
        Select Case If(status, "").Trim().ToUpperInvariant()
            Case "OPEN" : Return "YENİ"
            Case "ACCEPTED" : Return "İŞLEMDE"
            Case "COMPLETED" : Return "TAMAMLANDI"
            Case "CANCELLED" : Return "İPTAL"
            Case Else : Return "-"
        End Select
    End Function

    Private Shared Function StatusSort(status As String) As Integer
        Select Case If(status, "").Trim().ToUpperInvariant()
            Case "OPEN" : Return 0
            Case "ACCEPTED" : Return 1
            Case "COMPLETED" : Return 2
            Case Else : Return 3
        End Select
    End Function

    Private Shared Function IsOverdue(row As Dictionary(Of String, String)) As Boolean
        Dim status = DataService.GetValue(row, "Status").Trim().ToUpperInvariant()
        If status = "COMPLETED" OrElse status = "CANCELLED" Then Return False
        Dim dueDate As DateTime
        Return DateTime.TryParse(DataService.GetValue(row, "DueDate"), dueDate) AndAlso dueDate.Date < Date.Today
    End Function

    Private Shared Function IsUnsuitableResult(value As String) As Boolean
        Dim text = If(value, "").Trim()
        If text = "" Then Return False

        Return String.Equals(text, "UYGUN DEĞİL", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(text, "UYGUN DEGIL", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Shared Function FormatDateTime(value As String) As String
        Dim parsed As DateTime
        If DateTime.TryParse(value, parsed) Then Return parsed.ToString("dd.MM.yyyy HH:mm")
        Return "-"
    End Function

    Private Shared Function FormatDate(value As String) As String
        Dim parsed As DateTime
        If DateTime.TryParse(value, parsed) Then Return parsed.ToString("dd.MM.yyyy")
        Return "-"
    End Function
End Class
