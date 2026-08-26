Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmPlasticShiftErrorReports
    Inherits Form

    Private ReadOnly txtSearch As New TextBox()
    Private ReadOnly cboStatus As New ComboBox()
    Private ReadOnly chkReportDate As New CheckBox()
    Private ReadOnly dtpReportDate As New DateTimePicker()
    Private ReadOnly btnOpenDetail As New Button()
    Private ReadOnly btnRefresh As New Button()
    Private ReadOnly btnAssignments As New Button()
    Private ReadOnly btnDelete As New Button()
    Private ReadOnly grid As New DataGridView()
    Private ReadOnly lblShownCount As New Label()
    Private ReadOnly lblPendingCount As New Label()
    Private ReadOnly lblRevisionCount As New Label()
    Private ReadOnly lblApprovedCount As New Label()
    Private ReadOnly lblClosedCount As New Label()
    Private ReadOnly emptyStatePanel As New Panel()
    Private ReadOnly lblHint As New Label()

    Private allReports As New List(Of Dictionary(Of String, String))()
    Private shiftRowsById As New Dictionary(Of String, Dictionary(Of String, String))(StringComparer.OrdinalIgnoreCase)
    Private evaluationsByReportId As New Dictionary(Of String, List(Of PlasticShiftErrorReportEvaluation))(StringComparer.OrdinalIgnoreCase)

    Public Sub New()
        AuthorizationService.Require(AppState.CanOpenPlasticShiftErrorReport, "Hata Raporları")
        BuildScreen()
        AddHandler Shown, AddressOf Form_Shown
    End Sub

    Private Sub BuildScreen()
        AppIconService.Apply(Me)
        Text = "Hata Raporları"
        StartPosition = FormStartPosition.CenterScreen
        WindowState = FormWindowState.Maximized
        MinimumSize = New Size(980, 600)
        BackColor = Color.FromArgb(244, 247, 251)
        Font = New Font("Segoe UI", 9.0F)

        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 5,
            .Padding = New Padding(12),
            .BackColor = BackColor
        }
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 70))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 66))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 30))
        Controls.Add(root)

        Dim header As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .BackColor = Color.FromArgb(37, 82, 134),
            .Margin = New Padding(0, 0, 0, 6),
            .RowCount = 1,
            .ColumnCount = If(AppState.IsAdmin, 2, 1)
        }
        header.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        header.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        If AppState.IsAdmin Then
            header.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 285))
        End If
        Dim title As New Label() With {
            .Text = "Hata Raporları",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 14.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(18, 0, 0, 0)
        }
        header.Controls.Add(title, 0, 0)
        If AppState.IsAdmin Then
            Dim assignmentHost As New Panel() With {
                .Dock = DockStyle.Fill,
                .Padding = New Padding(8, 10, 10, 10),
                .BackColor = header.BackColor
            }
            btnAssignments.Text = "Değerlendirme Atamaları"
            btnAssignments.Dock = DockStyle.Fill
            btnAssignments.FlatStyle = FlatStyle.Flat
            btnAssignments.FlatAppearance.BorderColor = Color.FromArgb(183, 207, 235)
            btnAssignments.BackColor = Color.FromArgb(53, 101, 156)
            btnAssignments.ForeColor = Color.White
            btnAssignments.Font = New Font("Segoe UI", 8.7F, FontStyle.Bold)
            assignmentHost.Controls.Add(btnAssignments)
            header.Controls.Add(assignmentHost, 1, 0)
            AddHandler btnAssignments.Click, AddressOf OpenAssignments
        End If
        root.Controls.Add(header, 0, 0)

        Dim filterPanel As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = If(AppState.CanDeletePlasticShiftErrorReport, 6, 5),
            .RowCount = 1,
            .BackColor = Color.White,
            .Padding = New Padding(12, 8, 12, 8),
            .Margin = New Padding(0, 0, 0, 6)
        }
        filterPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        filterPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 185))
        filterPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 220))
        filterPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 125))
        filterPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 105))
        If AppState.CanDeletePlasticShiftErrorReport Then
            filterPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 165))
        End If
        filterPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        root.Controls.Add(filterPanel, 0, 1)

        txtSearch.Dock = DockStyle.Fill
        txtSearch.PlaceholderText = "rapor no / ürün / TR / bölüm / oluşturan"
        filterPanel.Controls.Add(CreateFieldHost("Arama", txtSearch), 0, 0)

        cboStatus.Dock = DockStyle.Fill
        cboStatus.DropDownStyle = ComboBoxStyle.DropDownList
        cboStatus.Items.AddRange({
            "TÜMÜ",
            "AÇIK / İŞLEMDE",
            "DEĞERLENDİRME BEKLİYOR",
            "REVİZYON GEREKLİ",
            "ONAYLANDI",
            "KAPALI"
        })
        cboStatus.SelectedIndex = 0
        filterPanel.Controls.Add(CreateFieldHost("Durum", cboStatus), 1, 0)

        dtpReportDate.Dock = DockStyle.Fill
        dtpReportDate.Format = DateTimePickerFormat.Custom
        dtpReportDate.CustomFormat = "dd.MM.yyyy"
        dtpReportDate.ShowCheckBox = False
        dtpReportDate.Enabled = False
        chkReportDate.Checked = False
        chkReportDate.AutoSize = False
        chkReportDate.Dock = DockStyle.Fill
        chkReportDate.Text = ""
        chkReportDate.Margin = New Padding(0)
        filterPanel.Controls.Add(CreateFieldHost("Rapor Günü", CreateDateSelector()), 2, 0)

        ConfigureButton(btnOpenDetail, "Detayı Aç", Color.FromArgb(37, 82, 134), Color.White)
        btnOpenDetail.Enabled = False
        btnOpenDetail.Margin = New Padding(6, 20, 6, 2)
        filterPanel.Controls.Add(btnOpenDetail, 3, 0)

        ConfigureButton(btnRefresh, "Yenile", Color.White, Color.FromArgb(37, 82, 134))
        btnRefresh.Margin = New Padding(6, 20, 6, 2)
        filterPanel.Controls.Add(btnRefresh, 4, 0)

        If AppState.CanDeletePlasticShiftErrorReport Then
            ConfigureButton(btnDelete, "Seçili Raporu Sil", Color.FromArgb(255, 231, 231), Color.FromArgb(174, 30, 30))
            btnDelete.FlatAppearance.BorderColor = Color.FromArgb(206, 68, 68)
            btnDelete.Enabled = False
            btnDelete.Margin = New Padding(6, 20, 0, 2)
            filterPanel.Controls.Add(btnDelete, 5, 0)
            AddHandler btnDelete.Click, AddressOf DeleteSelectedReport
        End If

        Dim summaryHost As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 5,
            .RowCount = 1,
            .Margin = New Padding(0, 0, 0, 6),
            .Padding = New Padding(0, 2, 0, 2),
            .BackColor = BackColor
        }
        For index = 0 To 4
            summaryHost.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 20))
        Next
        summaryHost.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        summaryHost.Controls.Add(CreateSummaryCard("GÖSTERİLEN", lblShownCount, Color.FromArgb(37, 82, 134)), 0, 0)
        summaryHost.Controls.Add(CreateSummaryCard("BEKLEYEN / İŞLEMDE", lblPendingCount, Color.FromArgb(215, 145, 22)), 1, 0)
        summaryHost.Controls.Add(CreateSummaryCard("REVİZYON", lblRevisionCount, Color.FromArgb(191, 55, 55)), 2, 0)
        summaryHost.Controls.Add(CreateSummaryCard("ONAYLANAN", lblApprovedCount, Color.FromArgb(31, 143, 78)), 3, 0)
        summaryHost.Controls.Add(CreateSummaryCard("KAPALI", lblClosedCount, Color.FromArgb(102, 112, 126)), 4, 0)
        root.Controls.Add(summaryHost, 0, 2)

        ConfigureGrid()
        Dim gridHost As New Panel() With {
            .Dock = DockStyle.Fill,
            .Margin = New Padding(0),
            .BackColor = Color.White
        }
        gridHost.Controls.Add(grid)
        ConfigureEmptyState()
        gridHost.Controls.Add(emptyStatePanel)
        root.Controls.Add(gridHost, 0, 3)

        lblHint.Dock = DockStyle.Fill
        lblHint.Text = "Bir raporu seçin ve Detayı Aç'a basın veya satıra çift tıklayın."
        lblHint.ForeColor = Color.FromArgb(80, 92, 108)
        lblHint.BackColor = Color.White
        lblHint.TextAlign = ContentAlignment.MiddleLeft
        lblHint.Padding = New Padding(12, 0, 0, 0)
        lblHint.Margin = New Padding(0, 6, 0, 0)
        root.Controls.Add(lblHint, 0, 4)

        AddHandler txtSearch.TextChanged, Sub(sender, e) ApplyFilters()
        AddHandler cboStatus.SelectedIndexChanged, Sub(sender, e) ApplyFilters()
        AddHandler chkReportDate.CheckedChanged,
            Sub(sender, e)
                dtpReportDate.Enabled = chkReportDate.Checked
                ApplyFilters()
            End Sub
        AddHandler dtpReportDate.ValueChanged,
            Sub(sender, e)
                If chkReportDate.Checked Then ApplyFilters()
            End Sub
        AddHandler btnRefresh.Click, Sub(sender, e) LoadData(GetSelectedReportId())
        AddHandler btnOpenDetail.Click, AddressOf OpenSelectedReport
        AddHandler grid.CellDoubleClick, AddressOf Grid_CellDoubleClick
        AddHandler grid.SelectionChanged, AddressOf Grid_SelectionChanged
    End Sub

    Private Shared Function CreateFieldHost(title As String, control As Control) As Control
        Dim host As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .Margin = New Padding(0, 0, 10, 0),
            .BackColor = Color.Transparent
        }
        host.RowStyles.Add(New RowStyle(SizeType.Absolute, 20))
        host.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        Dim label As New Label() With {
            .Text = title,
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Font = New Font("Segoe UI", 8.5F, FontStyle.Bold),
            .ForeColor = Color.FromArgb(28, 45, 67),
            .Margin = New Padding(0)
        }
        control.Dock = DockStyle.Fill
        control.Margin = New Padding(0)
        host.Controls.Add(label, 0, 0)
        host.Controls.Add(control, 0, 1)
        Return host
    End Function

    Private Function CreateDateSelector() As Control
        Dim selector As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 1,
            .Margin = New Padding(0),
            .Padding = New Padding(0),
            .BackColor = Color.Transparent
        }
        selector.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 25))
        selector.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        selector.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        dtpReportDate.Margin = New Padding(0)
        selector.Controls.Add(chkReportDate, 0, 0)
        selector.Controls.Add(dtpReportDate, 1, 0)
        Return selector
    End Function

    Private Shared Function CreateSummaryCard(title As String, valueLabel As Label, accentColor As Color) As Control
        Dim card As New Panel() With {
            .Dock = DockStyle.Fill,
            .Margin = New Padding(0, 0, 8, 0),
            .BackColor = Color.White,
            .Padding = New Padding(7, 5, 7, 5)
        }
        Dim accent As New Panel() With {
            .Dock = DockStyle.Left,
            .Width = 4,
            .BackColor = accentColor
        }
        Dim titleLabel As New Label() With {
            .Text = title,
            .Dock = DockStyle.Top,
            .Height = 19,
            .Padding = New Padding(9, 0, 0, 0),
            .ForeColor = Color.FromArgb(82, 94, 110),
            .Font = New Font("Segoe UI", 7.8F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft
        }
        valueLabel.Dock = DockStyle.Fill
        valueLabel.Padding = New Padding(9, 0, 0, 0)
        valueLabel.ForeColor = accentColor
        valueLabel.Font = New Font("Segoe UI", 12.0F, FontStyle.Bold)
        valueLabel.TextAlign = ContentAlignment.MiddleLeft
        valueLabel.Text = "0"
        card.Controls.Add(valueLabel)
        card.Controls.Add(titleLabel)
        card.Controls.Add(accent)
        Return card
    End Function

    Private Sub ConfigureEmptyState()
        emptyStatePanel.Dock = DockStyle.Fill
        emptyStatePanel.BackColor = Color.White
        emptyStatePanel.Visible = False

        Dim content As New TableLayoutPanel() With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .ColumnCount = 1,
            .RowCount = 2,
            .BackColor = Color.Transparent
        }
        content.RowStyles.Add(New RowStyle(SizeType.Absolute, 34))
        content.RowStyles.Add(New RowStyle(SizeType.Absolute, 28))
        Dim title As New Label() With {
            .Text = "Gösterilecek hata raporu bulunamadı",
            .Dock = DockStyle.Fill,
            .AutoSize = False,
            .Width = 420,
            .Font = New Font("Segoe UI", 12.0F, FontStyle.Bold),
            .ForeColor = Color.FromArgb(48, 68, 92),
            .TextAlign = ContentAlignment.MiddleCenter
        }
        Dim description As New Label() With {
            .Text = "Filtreleri değiştirin veya Yenile düğmesine basın.",
            .Dock = DockStyle.Fill,
            .AutoSize = False,
            .Width = 420,
            .Font = New Font("Segoe UI", 9.0F),
            .ForeColor = Color.FromArgb(105, 116, 130),
            .TextAlign = ContentAlignment.MiddleCenter
        }
        content.Controls.Add(title, 0, 0)
        content.Controls.Add(description, 0, 1)
        emptyStatePanel.Controls.Add(content)
        AddHandler emptyStatePanel.Resize,
            Sub(sender, e)
                content.Left = Math.Max(0, (emptyStatePanel.ClientSize.Width - content.Width) \ 2)
                content.Top = Math.Max(0, (emptyStatePanel.ClientSize.Height - content.Height) \ 2)
            End Sub
    End Sub

    Private Shared Sub ConfigureButton(button As Button, text As String, backColor As Color, foreColor As Color)
        button.Text = text
        button.Dock = DockStyle.Fill
        button.FlatStyle = FlatStyle.Flat
        button.FlatAppearance.BorderColor = Color.FromArgb(89, 112, 140)
        button.BackColor = backColor
        button.ForeColor = foreColor
        button.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
    End Sub

    Private Sub ConfigureGrid()
        grid.Dock = DockStyle.Fill
        grid.Margin = New Padding(0)
        grid.BackgroundColor = Color.White
        grid.BorderStyle = BorderStyle.FixedSingle
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.AllowUserToResizeRows = False
        grid.ReadOnly = True
        grid.MultiSelect = False
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.RowHeadersVisible = False
        grid.AutoGenerateColumns = False
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None
        grid.RowTemplate.Height = 34
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersHeight = 42
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(220, 232, 247)
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(31, 50, 75)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(205, 226, 250)
        grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(20, 40, 65)
        grid.DefaultCellStyle.Padding = New Padding(4, 2, 4, 2)
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 253)

        AddTextColumn("ReportId", "REPORT ID", 0, False)
        AddTextColumn("ShiftRecordId", "SHIFT ID", 0, False)
        AddTextColumn("ReportNo", "RAPOR NO", 155)
        AddTextColumn("StatusText", "DURUM", 185)
        AddTextColumn("CreatedAt", "OLUŞTURMA", 130)
        AddTextColumn("Product", "ÜRÜN / PARÇA", 240)
        AddTextColumn("TrNo", "TR", 110)
        AddTextColumn("Quantity", "MİKTAR", 85)
        AddTextColumn("SourceDepartment", "KAYNAK BÖLÜM", 155)
        AddTextColumn("CreatedBy", "OLUŞTURAN", 120)
        AddTextColumn("Evaluation", "DEĞERLENDİRME", 135)
        AddTextColumn("UpdatedAt", "SON GÜNCELLEME", 140)
        grid.Columns("Product").AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        grid.Columns("Quantity").DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter
        grid.Columns("Evaluation").DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter
    End Sub

    Private Sub AddTextColumn(name As String, header As String, width As Integer, Optional visible As Boolean = True)
        Dim column As New DataGridViewTextBoxColumn() With {
            .Name = name,
            .HeaderText = header,
            .Width = Math.Max(width, 20),
            .Visible = visible,
            .SortMode = DataGridViewColumnSortMode.Automatic
        }
        grid.Columns.Add(column)
    End Sub

    Private Sub Form_Shown(sender As Object, e As EventArgs)
        LoadData()
        txtSearch.Focus()
    End Sub

    Private Sub LoadData(Optional reportIdToRestore As String = "")
        Try
            Cursor = Cursors.WaitCursor
            allReports = DataService.GetPlasticShiftErrorReports()

            shiftRowsById = DataService.GetPlasticShiftTrackingRecords().
                Where(Function(row) DataService.GetValue(row, "RecordId").Trim() <> "").
                GroupBy(Function(row) DataService.GetValue(row, "RecordId").Trim(), StringComparer.OrdinalIgnoreCase).
                ToDictionary(
                    Function(group) group.Key,
                    Function(group) New Dictionary(Of String, String)(group.First(), StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase)

            evaluationsByReportId = DataService.GetAllPlasticShiftErrorReportEvaluations().
                Where(Function(item) Not String.IsNullOrWhiteSpace(item.ReportId)).
                GroupBy(Function(item) item.ReportId.Trim(), StringComparer.OrdinalIgnoreCase).
                ToDictionary(
                    Function(group) group.Key,
                    Function(group) group.ToList(),
                    StringComparer.OrdinalIgnoreCase)

            ApplyFilters(reportIdToRestore)
        Catch ex As Exception
            MessageBox.Show("Hata raporları yüklenemedi:" & Environment.NewLine & ex.Message,
                            "Hata Raporları",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error)
        Finally
            Cursor = Cursors.Default
        End Try
    End Sub

    Private Sub ApplyFilters(Optional reportIdToRestore As String = "")
        If grid.Columns.Count = 0 Then Return

        Dim searchText = txtSearch.Text.Trim()
        Dim selectedStatus = If(cboStatus.SelectedItem, "TÜMÜ").ToString()
        Dim filtered = allReports.
            Where(Function(report) MatchesStatus(DataService.GetValue(report, "Status"), selectedStatus)).
            Where(Function(report) Not chkReportDate.Checked OrElse SameReportDate(report, dtpReportDate.Value.Date)).
            Where(Function(report) MatchesSearch(report, searchText)).
            OrderByDescending(Function(report) ParseDate(DataService.GetValue(report, "CreatedAt"))).
            ThenByDescending(Function(report) DataService.GetValue(report, "ReportNo")).
            ToList()

        grid.Rows.Clear()
        For Each report In filtered
            AddReportRow(report)
        Next

        UpdateSummary(filtered.Count)
        emptyStatePanel.Visible = filtered.Count = 0
        If emptyStatePanel.Visible Then
            emptyStatePanel.BringToFront()
        Else
            grid.BringToFront()
        End If

        If reportIdToRestore <> "" Then
            For Each row As DataGridViewRow In grid.Rows
                If String.Equals(Convert.ToString(row.Cells("ReportId").Value),
                                 reportIdToRestore,
                                 StringComparison.OrdinalIgnoreCase) Then
                    row.Selected = True
                    grid.CurrentCell = row.Cells("ReportNo")
                    Exit For
                End If
            Next
        End If
        Grid_SelectionChanged(grid, EventArgs.Empty)
    End Sub

    Private Sub AddReportRow(report As Dictionary(Of String, String))
        Dim reportId = DataService.GetValue(report, "ReportId").Trim()
        Dim shiftId = DataService.GetValue(report, "ShiftRecordId").Trim()
        Dim shiftRow As Dictionary(Of String, String) = Nothing
        shiftRowsById.TryGetValue(shiftId, shiftRow)

        Dim product = DataService.GetValue(report, "PartNameNo").Trim()
        If product = "" AndAlso shiftRow IsNot Nothing Then product = DataService.GetValue(shiftRow, "ProductNameCode").Trim()

        Dim quantity = DataService.GetValue(report, "Quantity").Trim()
        If quantity = "" AndAlso shiftRow IsNot Nothing Then quantity = DataService.GetValue(shiftRow, "DefectiveQuantity").Trim()

        Dim evaluations As List(Of PlasticShiftErrorReportEvaluation) = Nothing
        If Not evaluationsByReportId.TryGetValue(reportId, evaluations) Then
            evaluations = New List(Of PlasticShiftErrorReportEvaluation)()
        End If
        Dim approved = evaluations.Where(
            Function(item) String.Equals(item.Decision, "APPROVED", StringComparison.OrdinalIgnoreCase)).Count()
        Dim hasRevision = evaluations.Any(
            Function(item) String.Equals(item.Decision, "REVISION_REQUIRED", StringComparison.OrdinalIgnoreCase))
        Dim evaluationText = approved.ToString() & " / 3"
        If hasRevision Then evaluationText &= " • Revizyon"

        Dim rowIndex = grid.Rows.Add(
            reportId,
            shiftId,
            DataService.GetValue(report, "ReportNo"),
            LocalStatus(DataService.GetValue(report, "Status")),
            FormatDate(DataService.GetValue(report, "CreatedAt")),
            product,
            DataService.GetValue(report, "TrNo"),
            quantity,
            DataService.GetValue(report, "SourceDepartment"),
            DataService.GetValue(report, "CreatedBy"),
            evaluationText,
            FormatDate(DataService.GetValue(report, "UpdatedAt")))

        ApplyStatusStyle(grid.Rows(rowIndex), DataService.GetValue(report, "Status"))
    End Sub

    Private Shared Sub ApplyStatusStyle(row As DataGridViewRow, rawStatus As String)
        Select Case If(rawStatus, "").Trim().ToUpperInvariant()
            Case "REVISION_REQUIRED"
                row.Cells("StatusText").Style.BackColor = Color.FromArgb(255, 222, 222)
                row.Cells("StatusText").Style.ForeColor = Color.FromArgb(176, 30, 30)
            Case "PENDING_EVALUATION", "OPEN", "IN_PROGRESS"
                row.Cells("StatusText").Style.BackColor = Color.FromArgb(255, 246, 207)
                row.Cells("StatusText").Style.ForeColor = Color.FromArgb(120, 78, 0)
            Case "APPROVED"
                row.Cells("StatusText").Style.BackColor = Color.FromArgb(220, 245, 226)
                row.Cells("StatusText").Style.ForeColor = Color.FromArgb(20, 120, 55)
            Case "CLOSED"
                row.Cells("StatusText").Style.BackColor = Color.FromArgb(231, 235, 240)
                row.Cells("StatusText").Style.ForeColor = Color.FromArgb(70, 78, 88)
        End Select
        row.Cells("StatusText").Style.Font = New Font("Segoe UI", 8.5F, FontStyle.Bold)
        row.Cells("StatusText").Style.Alignment = DataGridViewContentAlignment.MiddleCenter
    End Sub

    Private Sub UpdateSummary(shownCount As Integer)
        Dim pending = allReports.Where(
            Function(report)
                Dim status = DataService.GetValue(report, "Status").Trim().ToUpperInvariant()
                Return status = "PENDING_EVALUATION" OrElse status = "OPEN" OrElse status = "IN_PROGRESS"
            End Function).Count()
        Dim revisions = allReports.Where(
            Function(report) String.Equals(DataService.GetValue(report, "Status"), "REVISION_REQUIRED", StringComparison.OrdinalIgnoreCase)).Count()
        Dim approved = allReports.Where(
            Function(report) String.Equals(DataService.GetValue(report, "Status"), "APPROVED", StringComparison.OrdinalIgnoreCase)).Count()
        Dim closed = allReports.Where(
            Function(report) String.Equals(DataService.GetValue(report, "Status"), "CLOSED", StringComparison.OrdinalIgnoreCase)).Count()

        lblShownCount.Text = $"{shownCount} / {allReports.Count}"
        lblPendingCount.Text = pending.ToString()
        lblRevisionCount.Text = revisions.ToString()
        lblApprovedCount.Text = approved.ToString()
        lblClosedCount.Text = closed.ToString()
    End Sub

    Private Function MatchesSearch(report As Dictionary(Of String, String), searchText As String) As Boolean
        If searchText = "" Then Return True
        Dim shiftRow As Dictionary(Of String, String) = Nothing
        shiftRowsById.TryGetValue(DataService.GetValue(report, "ShiftRecordId").Trim(), shiftRow)
        Dim values As New List(Of String) From {
            DataService.GetValue(report, "ReportNo"),
            DataService.GetValue(report, "PartNameNo"),
            DataService.GetValue(report, "TrNo"),
            DataService.GetValue(report, "SourceDepartment"),
            DataService.GetValue(report, "CreatedBy"),
            DataService.GetValue(report, "DefectType"),
            DataService.GetValue(report, "NonconformityDescription")
        }
        If shiftRow IsNot Nothing Then
            values.Add(DataService.GetValue(shiftRow, "ProductNameCode"))
            values.Add(DataService.GetValue(shiftRow, "Responsible"))
            values.Add(DataService.GetValue(shiftRow, "Problem"))
        End If
        Return values.Any(Function(value) If(value, "").IndexOf(searchText, StringComparison.CurrentCultureIgnoreCase) >= 0)
    End Function

    Private Shared Function MatchesStatus(rawStatus As String, selectedStatus As String) As Boolean
        Dim status = If(rawStatus, "").Trim().ToUpperInvariant()
        Select Case selectedStatus
            Case "AÇIK / İŞLEMDE"
                Return status = "OPEN" OrElse status = "IN_PROGRESS"
            Case "DEĞERLENDİRME BEKLİYOR"
                Return status = "PENDING_EVALUATION"
            Case "REVİZYON GEREKLİ"
                Return status = "REVISION_REQUIRED"
            Case "ONAYLANDI"
                Return status = "APPROVED"
            Case "KAPALI"
                Return status = "CLOSED"
            Case Else
                Return True
        End Select
    End Function

    Private Shared Function SameReportDate(report As Dictionary(Of String, String), targetDate As Date) As Boolean
        Dim value = ParseDate(DataService.GetValue(report, "CreatedAt"))
        Return value <> DateTime.MinValue AndAlso value.Date = targetDate
    End Function

    Private Shared Function ParseDate(text As String) As DateTime
        Dim value As DateTime
        If DateTime.TryParse(If(text, "").Trim(), value) Then Return value
        Return DateTime.MinValue
    End Function

    Private Shared Function FormatDate(text As String) As String
        Dim value = ParseDate(text)
        If value = DateTime.MinValue Then Return If(text, "")
        Return value.ToString("dd.MM.yyyy HH:mm")
    End Function

    Private Shared Function LocalStatus(value As String) As String
        Select Case If(value, "").Trim().ToUpperInvariant()
            Case "PENDING_EVALUATION" : Return "DEĞERLENDİRME BEKLİYOR"
            Case "REVISION_REQUIRED" : Return "REVİZYON GEREKLİ"
            Case "APPROVED" : Return "ONAYLANDI"
            Case "CLOSED" : Return "KAPALI"
            Case "IN_PROGRESS" : Return "İŞLEMDE"
            Case "OPEN" : Return "AÇIK"
            Case Else : Return If(value, "").Trim()
        End Select
    End Function

    Private Function GetSelectedReportId() As String
        If grid.CurrentRow Is Nothing Then Return ""
        Return Convert.ToString(grid.CurrentRow.Cells("ReportId").Value).Trim()
    End Function

    Private Sub Grid_SelectionChanged(sender As Object, e As EventArgs)
        Dim hasSelection = GetSelectedReportId() <> ""
        btnOpenDetail.Enabled = hasSelection
        If AppState.CanDeletePlasticShiftErrorReport Then
            btnDelete.Enabled = hasSelection
        End If
    End Sub

    Private Sub Grid_CellDoubleClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 Then Return
        OpenSelectedReport(sender, EventArgs.Empty)
    End Sub

    Private Sub OpenSelectedReport(sender As Object, e As EventArgs)
        Dim reportId = GetSelectedReportId()
        If reportId = "" OrElse grid.CurrentRow Is Nothing Then
            MessageBox.Show("Detayını görmek istediğiniz hata raporunu seçin.",
                            "Hata Raporları",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information)
            Return
        End If

        Dim shiftId = Convert.ToString(grid.CurrentRow.Cells("ShiftRecordId").Value).Trim()
        Dim shiftRow As Dictionary(Of String, String) = Nothing
        If Not shiftRowsById.TryGetValue(shiftId, shiftRow) Then
            shiftRow = New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                {"RecordId", shiftId}
            }
        End If

        Try
            Using detail As New FrmPlasticShiftErrorReport(shiftRow)
                detail.ShowDialog(Me)
            End Using
            LoadData(reportId)
        Catch ex As Exception
            MessageBox.Show("Hata raporu detayı açılamadı:" & Environment.NewLine & ex.Message,
                            "Hata Raporları",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub OpenAssignments(sender As Object, e As EventArgs)
        Try
            Dim selectedReportId = GetSelectedReportId()
            Using assignments As New FrmPlasticShiftErrorReportEvaluatorAssignments()
                assignments.ShowDialog(Me)
            End Using
            LoadData(selectedReportId)
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show("Değerlendirme atamaları açılamadı:" & Environment.NewLine & ex.Message,
                            "Hata Raporları",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub DeleteSelectedReport(sender As Object, e As EventArgs)
        If Not AppState.CanDeletePlasticShiftErrorReport Then
            AuthorizationService.ShowDenied(
                New UnauthorizedAccessException("Hata raporlarını yalnızca Admin silebilir."),
                Me)
            Return
        End If

        Dim reportId = GetSelectedReportId()
        If reportId = "" OrElse grid.CurrentRow Is Nothing Then
            MessageBox.Show("Silmek istediğiniz hata raporunu listeden seçin.",
                            "Hata Raporları",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information)
            Return
        End If

        Dim reportNo = Convert.ToString(grid.CurrentRow.Cells("ReportNo").Value).Trim()
        Dim product = Convert.ToString(grid.CurrentRow.Cells("Product").Value).Trim()
        Dim confirmation =
            "Bu işlem geri alınamaz." & Environment.NewLine & Environment.NewLine &
            "Rapor No: " & If(reportNo = "", reportId, reportNo) & Environment.NewLine &
            "Ürün / TR: " & If(product = "", "-", product) & Environment.NewLine & Environment.NewLine &
            "Rapor ve bağlı değerlendirme kayıtları silinsin mi?"

        If MessageBox.Show(confirmation,
                           "Hata Raporunu Sil",
                           MessageBoxButtons.YesNo,
                           MessageBoxIcon.Warning,
                           MessageBoxDefaultButton.Button2) <> DialogResult.Yes Then
            Return
        End If

        Try
            Cursor = Cursors.WaitCursor
            DataService.DeletePlasticShiftErrorReport(reportId)
            LoadData()
            MessageBox.Show("Hata raporu silindi.",
                            "Hata Raporları",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information)
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show("Hata raporu silinemedi:" & Environment.NewLine & ex.Message,
                            "Hata Raporları",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error)
        Finally
            Cursor = Cursors.Default
        End Try
    End Sub
End Class
