Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmPackageMeterControls
    Inherits Form

    Private ReadOnly grid As New DataGridView()
    Private ReadOnly txtSearch As New TextBox()
    Private ReadOnly cboStatus As New ComboBox()
    Private ReadOnly dtpControlDate As New DateTimePicker()
    Private ReadOnly lblSummary As New Label()
    Private currentRows As New List(Of Dictionary(Of String, String))()
    Private currentLines As New List(Of PackageMeterControlLine)()
    Private serialSearchByControl As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

    Public Sub New()
        AuthorizationService.Require(AppState.CanOpenPackageMeterControls, "Paket Sayaç Kontrolleri")
        AppIconService.Apply(Me)
        Text = "Paketten Alınan Sayaçların Kontrolü"
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        Size = New Size(1500, 820)
        MinimumSize = New Size(900, 560)
        BackColor = Color.FromArgb(243, 247, 252)
        Font = New Font("Segoe UI", 9.0F)

        BuildScreen()
        lblSummary.Text = "Kontrol kayıtları yükleniyor..."
        AddHandler Shown, Sub() BeginInvoke(CType(Sub() LoadGrid(), MethodInvoker))
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
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Controls.Add(root)

        Dim toolbar As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .BackColor = Color.White,
            .Padding = New Padding(8, 6, 8, 6),
            .Margin = New Padding(0, 0, 0, 6)
        }
        toolbar.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        toolbar.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        root.Controls.Add(toolbar, 0, 0)

        Dim actions As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .Margin = New Padding(0)
        }
        Dim btnNew As New Button()
        ConfigureButton(btnNew, "Yeni Kontrol", 120, Color.FromArgb(15, 123, 63), Color.White)
        btnNew.Visible = AppState.CanModifyPackageMeterControls
        AddHandler btnNew.Click, AddressOf New_Click

        Dim btnDetail As New Button()
        ConfigureButton(btnDetail, "Detay", 95, Color.FromArgb(31, 71, 126), Color.White)
        AddHandler btnDetail.Click, Sub() OpenSelected()

        Dim btnDelete As New Button()
        ConfigureButton(btnDelete, "Seçili Kaydı Sil", 135, Color.MistyRose, Color.DarkRed)
        btnDelete.Visible = AppState.CanDeletePackageMeterControls
        AddHandler btnDelete.Click, AddressOf Delete_Click

        Dim btnRefresh As New Button()
        ConfigureButton(btnRefresh, "Yenile", 95, Color.White, Color.FromArgb(35, 50, 70))
        AddHandler btnRefresh.Click, Sub() LoadGrid()

        Dim btnEmailRecipients As New Button()
        ConfigureButton(btnEmailRecipients, "Uygun Değil Mail Alıcıları", 190, Color.White, Color.FromArgb(31, 71, 126))
        btnEmailRecipients.Visible = AppState.CanManagePackageMeterEmailRecipients
        AddHandler btnEmailRecipients.Click,
            Sub()
                Try
                    AuthorizationService.Require(AppState.CanManagePackageMeterEmailRecipients, "Paket Sayaç Kontrol Mail Alıcıları")
                    Using frm As New FrmMechanismQualityEmailRecipients(True)
                        frm.ShowDialog(Me)
                    End Using
                Catch ex As UnauthorizedAccessException
                    AuthorizationService.ShowDenied(ex, Me)
                End Try
            End Sub
        actions.Controls.AddRange({btnNew, btnDetail, btnDelete, btnRefresh, btnEmailRecipients})
        toolbar.Controls.Add(actions, 0, 0)

        Dim filters As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 7,
            .RowCount = 1,
            .Margin = New Padding(0),
            .Padding = New Padding(0, 0, 8, 0),
            .BackColor = Color.White
        }
        filters.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 52.0F))
        filters.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        filters.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 52.0F))
        filters.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 145.0F))
        filters.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 96.0F))
        filters.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 155.0F))
        filters.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 130.0F))
        toolbar.Controls.Add(filters, 0, 1)

        filters.Controls.Add(FilterLabel("Arama"), 0, 0)
        txtSearch.Dock = DockStyle.Fill
        txtSearch.PlaceholderText = "kontrol no / model / müşteri / seri no / operatör / kontrol eden"
        txtSearch.Margin = New Padding(4, 4, 12, 4)
        AddHandler txtSearch.TextChanged, Sub() ApplyFilters()
        filters.Controls.Add(txtSearch, 1, 0)

        filters.Controls.Add(FilterLabel("Durum"), 2, 0)
        cboStatus.Dock = DockStyle.Fill
        cboStatus.DropDownStyle = ComboBoxStyle.DropDownList
        cboStatus.Items.AddRange({"TÜMÜ", "TASLAK", "TAMAMLANDI"})
        cboStatus.SelectedIndex = 0
        cboStatus.Margin = New Padding(4, 4, 12, 4)
        AddHandler cboStatus.SelectedIndexChanged, Sub() ApplyFilters()
        filters.Controls.Add(cboStatus, 3, 0)

        filters.Controls.Add(FilterLabel("Kontrol Günü"), 4, 0)
        dtpControlDate.Dock = DockStyle.Fill
        dtpControlDate.Format = DateTimePickerFormat.Custom
        dtpControlDate.CustomFormat = "dd.MM.yyyy"
        dtpControlDate.ShowCheckBox = True
        dtpControlDate.Checked = False
        dtpControlDate.Margin = New Padding(4, 4, 12, 4)
        AddHandler dtpControlDate.ValueChanged, Sub() ApplyFilters()
        filters.Controls.Add(dtpControlDate, 5, 0)

        Dim btnClear As New Button()
        ConfigureButton(btnClear, "Filtreyi Temizle", 120, Color.White, Color.FromArgb(31, 71, 126))
        btnClear.Dock = DockStyle.Fill
        btnClear.Margin = New Padding(4, 2, 6, 2)
        AddHandler btnClear.Click,
            Sub()
                txtSearch.Clear()
                cboStatus.SelectedIndex = 0
                dtpControlDate.Checked = False
                ApplyFilters()
            End Sub
        filters.Controls.Add(btnClear, 6, 0)

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
        grid.AllowUserToResizeRows = False
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
        grid.RowTemplate.Height = 32
        grid.DefaultCellStyle.Padding = New Padding(4, 2, 4, 2)

        grid.Columns.Add(MakeColumn("StatusDisplay", "DURUM", 85, 7))
        grid.Columns.Add(MakeColumn("ControlId", "KONTROL NO", 155, 12))
        grid.Columns.Add(MakeColumn("ControlDateDisplay", "KONTROL TARİHİ", 125, 10))
        grid.Columns.Add(MakeColumn("MeterModel", "SAYAÇ MODELİ", 135, 12))
        grid.Columns.Add(MakeColumn("PulseCount", "PALS SAYISI", 85, 7))
        grid.Columns.Add(MakeColumn("Customer", "MÜŞTERİ", 145, 13))
        grid.Columns.Add(MakeColumn("ProductionPanelNo", "ÜRETİM PANO", 100, 8))
        grid.Columns.Add(MakeColumn("ControlPanelNo", "KONTROL PANO", 100, 8))
        grid.Columns.Add(MakeColumn("OperatorInfo", "OPERATÖR", 120, 10))
        grid.Columns.Add(MakeColumn("ControllerName", "KONTROL EDEN", 120, 10))
        grid.Columns.Add(MakeColumn("MeterCount", "SAYAÇ", 65, 6))
        grid.Columns.Add(MakeColumn("SuitableCount", "KONTROL EDİLEN", 90, 8))
        grid.Columns.Add(MakeColumn("UnsuitableCount", "UYGUN DEĞİL", 90, 8))
        grid.Columns.Add(MakeColumn("IncompleteCount", "EKSİK", 65, 6))
        grid.Columns.Add(MakeColumn("UpdatedAtDisplay", "SON GÜNCELLEME", 125, 10))

        AddHandler grid.CellDoubleClick, Sub(sender, e) If e.RowIndex >= 0 Then OpenSelected()
        AddHandler grid.CellFormatting, AddressOf Grid_CellFormatting
    End Sub

    Private Sub LoadGrid()
        Try
            currentLines = DataService.GetAllPackageMeterControlLines()
            serialSearchByControl = currentLines.
                GroupBy(Function(line) line.ControlId, StringComparer.OrdinalIgnoreCase).
                ToDictionary(Function(group) group.Key,
                             Function(group) String.Join(" ", group.Select(Function(line) line.SerialNumber)),
                             StringComparer.OrdinalIgnoreCase)
            currentRows = DataService.GetPackageMeterControls().
                OrderBy(Function(row) StatusSort(DataService.GetValue(row, "Status"))).
                ThenByDescending(Function(row) DataService.GetValue(row, "ControlDate")).
                ToList()
            ApplyFilters()
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Kontrol kayıtları yüklenemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ApplyFilters()
        If grid Is Nothing Then Return
        Dim rows = currentRows.AsEnumerable()

        Dim statusText = If(cboStatus.SelectedItem Is Nothing, "TÜMÜ", cboStatus.SelectedItem.ToString())
        If statusText = "TASLAK" Then
            rows = rows.Where(Function(row) String.Equals(DataService.GetValue(row, "Status"), "DRAFT", StringComparison.OrdinalIgnoreCase))
        ElseIf statusText = "TAMAMLANDI" Then
            rows = rows.Where(Function(row) String.Equals(DataService.GetValue(row, "Status"), "COMPLETED", StringComparison.OrdinalIgnoreCase))
        End If

        If dtpControlDate.Checked Then
            rows = rows.Where(Function(row) SameDate(DataService.GetValue(row, "ControlDate"), dtpControlDate.Value.Date))
        End If

        Dim search = txtSearch.Text.Trim()
        If search <> "" Then
            Dim tokens = search.Split(New Char() {" "c, ";"c, ","c, "|"c}, StringSplitOptions.RemoveEmptyEntries)
            rows = rows.Where(
                Function(row)
                    Dim controlId = DataService.GetValue(row, "ControlId")
                    Dim serials = If(serialSearchByControl.ContainsKey(controlId), serialSearchByControl(controlId), "")
                    Dim haystack = String.Join(" ", PackageSearchHeaders().Select(Function(header) DataService.GetValue(row, header))) & " " & serials
                    Dim normalized = haystack.ToUpperInvariant()
                    Return tokens.All(Function(token) normalized.Contains(token.ToUpperInvariant()))
                End Function)
        End If

        Dim displayed = rows.ToList()
        grid.Rows.Clear()
        For Each item In displayed
            Dim index = grid.Rows.Add(
                StatusDisplay(DataService.GetValue(item, "Status")),
                DataService.GetValue(item, "ControlId"),
                FormatDateTime(DataService.GetValue(item, "ControlDate")),
                DataService.GetValue(item, "MeterModel"),
                DataService.GetValue(item, "PulseCount"),
                DataService.GetValue(item, "Customer"),
                DataService.GetValue(item, "ProductionPanelNo"),
                DataService.GetValue(item, "ControlPanelNo"),
                DataService.GetValue(item, "OperatorInfo"),
                DataService.GetValue(item, "ControllerName"),
                DataService.GetValue(item, "MeterCount"),
                DataService.GetValue(item, "SuitableCount"),
                DataService.GetValue(item, "UnsuitableCount"),
                DataService.GetValue(item, "IncompleteCount"),
                FormatDateTime(DataService.GetValue(item, "UpdatedAt")))
            grid.Rows(index).Tag = item
        Next

        Dim displayedMeterCount = CountMetersFor(displayed)
        Dim totalMeterCount = CountMetersFor(currentRows)
        Dim draftMeterCount = CountMetersFor(
            currentRows.Where(Function(row) String.Equals(DataService.GetValue(row, "Status"), "DRAFT", StringComparison.OrdinalIgnoreCase)))
        Dim completedMeterCount = CountMetersFor(
            currentRows.Where(Function(row) String.Equals(DataService.GetValue(row, "Status"), "COMPLETED", StringComparison.OrdinalIgnoreCase)))
        Dim todayMeterCount = CountMetersFor(
            currentRows.Where(Function(row) SameDate(DataService.GetValue(row, "ControlDate"), Date.Today)))
        Dim currentControlIds = New HashSet(Of String)(
            currentRows.Select(Function(row) DataService.GetValue(row, "ControlId")),
            StringComparer.OrdinalIgnoreCase)
        Dim unsuitableMeterCount = currentLines.Where(
            Function(line) currentControlIds.Contains(If(line.ControlId, "")) AndAlso
                           String.Equals(If(line.OverallResult, "").Trim(), "UYGUN DEĞİL", StringComparison.OrdinalIgnoreCase)).Count()

        lblSummary.Text = "Gösterilen sayaç: " & displayedMeterCount.ToString() & " / " & totalMeterCount.ToString() &
                          "   |   Bugünkü sayaç: " & todayMeterCount.ToString() &
                          "   |   Taslak sayaç: " & draftMeterCount.ToString() &
                          "   |   Tamamlanan sayaç: " & completedMeterCount.ToString() &
                          "   |   Uygun olmayan sayaç: " & unsuitableMeterCount.ToString()
    End Sub

    Private Function CountMetersFor(records As IEnumerable(Of Dictionary(Of String, String))) As Integer
        If records Is Nothing Then Return 0

        Dim controlIds = New HashSet(Of String)(
            records.Select(Function(row) DataService.GetValue(row, "ControlId")),
            StringComparer.OrdinalIgnoreCase)
        Return currentLines.Where(Function(line) controlIds.Contains(If(line.ControlId, ""))).Count()
    End Function

    Private Sub Grid_CellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If e.RowIndex < 0 Then Return
        Dim row = grid.Rows(e.RowIndex)
        Dim record = TryCast(row.Tag, Dictionary(Of String, String))
        If record Is Nothing Then Return

        Dim unsuitable = ParseInt(DataService.GetValue(record, "UnsuitableCount"))
        Dim status = DataService.GetValue(record, "Status").Trim().ToUpperInvariant()
        If unsuitable > 0 Then
            row.DefaultCellStyle.BackColor = Color.MistyRose
            row.DefaultCellStyle.ForeColor = Color.DarkRed
        ElseIf status = "COMPLETED" Then
            row.DefaultCellStyle.BackColor = Color.Honeydew
            row.DefaultCellStyle.ForeColor = Color.DarkGreen
        Else
            row.DefaultCellStyle.BackColor = Color.LemonChiffon
            row.DefaultCellStyle.ForeColor = Color.FromArgb(112, 71, 0)
        End If
    End Sub

    Private Sub New_Click(sender As Object, e As EventArgs)
        Using detail As New FrmPackageMeterControlDetail()
            If detail.ShowDialog(Me) = DialogResult.OK OrElse detail.HasChanges Then LoadGrid()
        End Using
    End Sub

    Private Sub OpenSelected()
        If grid.CurrentRow Is Nothing Then
            MessageBox.Show("Lütfen bir kontrol kaydı seçin.", "Kayıt seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Dim record = TryCast(grid.CurrentRow.Tag, Dictionary(Of String, String))
        If record Is Nothing Then Return
        Using detail As New FrmPackageMeterControlDetail(record)
            If detail.ShowDialog(Me) = DialogResult.OK OrElse detail.HasChanges Then LoadGrid()
        End Using
    End Sub

    Private Sub Delete_Click(sender As Object, e As EventArgs)
        If grid.CurrentRow Is Nothing Then
            MessageBox.Show("Lütfen silinecek kontrol kaydını seçin.", "Kayıt seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Dim record = TryCast(grid.CurrentRow.Tag, Dictionary(Of String, String))
        If record Is Nothing Then Return
        Dim controlId = DataService.GetValue(record, "ControlId")
        If MessageBox.Show(controlId & " numaralı kontrol kaydı ve tüm sayaç satırları kalıcı olarak silinecek. Devam edilsin mi?",
                           "Kontrol kaydını sil", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) <> DialogResult.Yes Then Return
        Try
            DataService.DeletePackageMeterControl(controlId)
            LoadGrid()
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Kayıt silinemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Shared Function FilterLabel(text As String) As Label
        Return New Label() With {.Text = text, .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Font = New Font("Segoe UI", 8.5F, FontStyle.Bold)}
    End Function

    Private Shared Function MakeColumn(name As String, header As String, minimumWidth As Integer, fillWeight As Single) As DataGridViewTextBoxColumn
        Return New DataGridViewTextBoxColumn() With {.Name = name, .HeaderText = header, .MinimumWidth = minimumWidth, .FillWeight = fillWeight, .SortMode = DataGridViewColumnSortMode.Automatic}
    End Function

    Private Shared Sub ConfigureButton(button As Button, text As String, width As Integer, backColor As Color, foreColor As Color)
        button.Text = text
        button.Width = width
        button.Height = 32
        button.BackColor = backColor
        button.ForeColor = foreColor
        button.FlatStyle = FlatStyle.Flat
        button.Font = New Font("Segoe UI", 8.7F, FontStyle.Bold)
        button.Margin = New Padding(4, 0, 4, 0)
        button.Cursor = Cursors.Hand
        button.UseVisualStyleBackColor = False
    End Sub

    Private Shared Function PackageSearchHeaders() As String()
        Return {"ControlId", "MeterModel", "PulseCount", "Customer", "OperatorInfo", "ControllerName", "ProductionPanelNo", "ControlPanelNo", "Explanation", "CreatedBy", "CompletedBy"}
    End Function

    Private Shared Function StatusDisplay(value As String) As String
        If String.Equals(If(value, "").Trim(), "COMPLETED", StringComparison.OrdinalIgnoreCase) Then Return "TAMAMLANDI"
        Return "TASLAK"
    End Function

    Private Shared Function StatusSort(value As String) As Integer
        Return If(String.Equals(If(value, "").Trim(), "DRAFT", StringComparison.OrdinalIgnoreCase), 0, 1)
    End Function

    Private Shared Function FormatDateTime(value As String) As String
        Dim parsed As DateTime
        If DateTime.TryParse(value, parsed) Then Return parsed.ToString("dd.MM.yyyy HH:mm")
        Return "-"
    End Function

    Private Shared Function SameDate(value As String, expected As Date) As Boolean
        Dim parsed As DateTime
        Return DateTime.TryParse(value, parsed) AndAlso parsed.Date = expected.Date
    End Function

    Private Shared Function ParseInt(value As String) As Integer
        Dim parsed As Integer
        Integer.TryParse(If(value, "").Trim(), parsed)
        Return parsed
    End Function
End Class
