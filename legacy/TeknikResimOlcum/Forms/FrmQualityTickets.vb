Imports System.Data
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmQualityTickets
    Inherits Form

    Private ReadOnly grid As New DataGridView()
    Private ReadOnly txtFilter As New TextBox()
    Private ReadOnly cboStatus As New ComboBox()
    Private ReadOnly lblCount As New Label()

    Public Sub New()
        AuthorizationService.Require(AppState.CanOpenQualityTickets, "Kalite Ticketlari")
        AppIconService.Apply(Me)
        Text = "Kalite Kontrol Ticketları"
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        Size = New Size(1450, 780)
        MinimumSize = New Size(760, 520)
        BackColor = Color.White

        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .BackColor = Color.White
        }
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 54.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Controls.Add(layout)

        Dim top As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(10, 8, 10, 8),
            .BackColor = SystemColors.Control,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = True,
            .AutoScroll = False
        }
        layout.Controls.Add(top, 0, 0)

        top.Controls.Add(New Label() With {
            .Text = "Arama",
            .Width = 50,
            .Height = 30,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Margin = New Padding(4, 3, 4, 3)
        })
        txtFilter.Width = 250
        txtFilter.Height = 24
        txtFilter.Margin = New Padding(4, 6, 4, 6)
        txtFilter.PlaceholderText = "makine / kalıp / TR / hammadde / iş emri"
        AddHandler txtFilter.TextChanged, Sub() LoadGrid()
        top.Controls.Add(txtFilter)

        top.Controls.Add(New Label() With {
            .Text = "Durum",
            .Width = 55,
            .Height = 30,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Margin = New Padding(16, 3, 4, 3)
        })
        cboStatus.Width = 110
        cboStatus.Height = 24
        cboStatus.Margin = New Padding(4, 5, 4, 5)
        cboStatus.DropDownStyle = ComboBoxStyle.DropDownList
        cboStatus.Items.AddRange({"AKTİF", "AÇIK", "GÖRÜLDÜ", "KAPALI", "TÜMÜ"})
        cboStatus.SelectedIndex = 0
        AddHandler cboStatus.SelectedIndexChanged, Sub() LoadGrid()
        top.Controls.Add(cboStatus)

        Dim btnRefresh As New Button() With {.Text = "Yenile", .Width = 90, .Height = 32, .Margin = New Padding(16, 2, 4, 2)}
        AddHandler btnRefresh.Click, Sub() LoadGrid()

        Dim btnSeen As New Button() With {.Text = "Görüldü Yap", .Width = 110, .Height = 32, .Margin = New Padding(4, 2, 4, 2)}
        AddHandler btnSeen.Click, AddressOf Seen_Click

        Dim btnOpenMeasurement As New Button() With {.Text = "Kontrol Girişi Aç", .Width = 135, .Height = 32, .Margin = New Padding(4, 2, 4, 2)}
        AddHandler btnOpenMeasurement.Click, AddressOf OpenMeasurementForTicket_Click

        Dim btnTicketMeasurements As New Button() With {.Text = "Ticket Ölçümleri", .Width = 125, .Height = 32, .Margin = New Padding(4, 2, 4, 2)}
        AddHandler btnTicketMeasurements.Click, AddressOf OpenTicketMeasurements_Click

        Dim btnClose As New Button() With {.Text = "Ticketı Kapat", .Width = 115, .Height = 32, .Margin = New Padding(4, 2, 4, 2)}
        AddHandler btnClose.Click, AddressOf Close_Click

        top.Controls.Add(btnRefresh)
        If AppState.CanModifyQualityTickets Then top.Controls.Add(btnSeen)
        If AppState.CanOpenMeasurement Then top.Controls.Add(btnOpenMeasurement)
        top.Controls.Add(btnTicketMeasurements)
        If AppState.CanModifyQualityTickets Then top.Controls.Add(btnClose)

        Dim adjustToolbarHeight As Action =
            Sub()
                If layout.IsDisposed OrElse top.IsDisposed Then Return
                Dim availableWidth = Math.Max(320, layout.ClientSize.Width)
                Dim preferredHeight = top.GetPreferredSize(New Size(availableWidth, 0)).Height
                layout.RowStyles(0).Height = CSng(Math.Max(54, Math.Min(150, preferredHeight)))
                top.AutoScroll = preferredHeight > 150
            End Sub
        AddHandler layout.ClientSizeChanged, Sub(sender, e) adjustToolbarHeight()
        AddHandler Me.Shown, Sub(sender, e) adjustToolbarHeight()

        Dim summary As New Panel() With {.Dock = DockStyle.Fill, .Padding = New Padding(10, 5, 10, 4), .BackColor = Color.WhiteSmoke}
        layout.Controls.Add(summary, 0, 1)

        lblCount.Text = "Ticket: 0"
        lblCount.Dock = DockStyle.Fill
        lblCount.Margin = New Padding(2)
        lblCount.TextAlign = ContentAlignment.MiddleLeft
        lblCount.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        summary.Controls.Add(lblCount)

        ConfigureGrid()
        layout.Controls.Add(grid, 0, 2)

        LoadGrid()
    End Sub

    Private Sub ConfigureGrid()
        grid.Dock = DockStyle.Fill
        grid.ReadOnly = True
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.MultiSelect = False
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.AutoGenerateColumns = False
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
        grid.RowHeadersVisible = False
        grid.BackgroundColor = Color.White
        grid.BorderStyle = BorderStyle.FixedSingle
        grid.GridColor = Color.Gainsboro
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 235, 235)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        grid.DefaultCellStyle.BackColor = Color.White
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248)
        grid.RowTemplate.Height = 28
        grid.ScrollBars = ScrollBars.Both

        grid.Columns.Clear()
        grid.Columns.Add(MakeColumn("TicketId", "Ticket No", 170))
        grid.Columns.Add(MakeColumn("Status", "Durum", 80))
        grid.Columns.Add(MakeColumn("CreatedAt", "Tarih", 135))
        grid.Columns.Add(MakeColumn("CreatedBy", "Üretim Kullanıcısı", 120))
        grid.Columns.Add(MakeColumn("MachineNo", "Makine", 120))
        grid.Columns.Add(MakeColumn("MoldCode", "Kalıp Kodu", 110))
        grid.Columns.Add(MakeColumn("TrCode", "TR Kodu", 100))
        grid.Columns.Add(MakeColumn("DrawingRev", "Rev.", 65))
        grid.Columns.Add(MakeColumn("ProductName", "Ürün Adı", 150))
        grid.Columns.Add(MakeColumn("RawMaterial", "Bağlanan Hammadde", 160))
        grid.Columns.Add(MakeColumn("Material", "Tanımlı Malzeme", 130))
        grid.Columns.Add(MakeColumn("ColorName", "Renk", 90))
        grid.Columns.Add(MakeColumn("WorkOrderNo", "İş Emri No", 110))
        grid.Columns.Add(MakeColumn("Note", "Üretim Notu", 220))
        grid.Columns.Add(MakeColumn("SeenByQuality", "Gören", 110))
        grid.Columns.Add(MakeColumn("SeenAt", "Görülme Tarihi", 135))
        grid.Columns.Add(MakeColumn("ClosedBy", "Kapatan", 110))
        grid.Columns.Add(MakeColumn("ClosedAt", "Kapanış Tarihi", 135))
        grid.Columns.Add(MakeColumn("CloseNote", "Kapanış Notu", 220))

        AddHandler grid.CellFormatting, AddressOf Grid_CellFormatting
        AddHandler grid.CellDoubleClick, AddressOf Grid_DoubleClick
    End Sub

    Private Function MakeColumn(name As String, header As String, width As Integer) As DataGridViewTextBoxColumn
        Return New DataGridViewTextBoxColumn() With {
            .Name = name,
            .DataPropertyName = name,
            .HeaderText = header,
            .Width = width,
            .MinimumWidth = 60,
            .SortMode = DataGridViewColumnSortMode.Automatic
        }
    End Function

    Private Sub LoadGrid()
        Dim allRows = DataService.GetProductionTickets()
        Dim rows = allRows.AsEnumerable()

        Dim statusText = If(cboStatus.SelectedItem Is Nothing, "AKTİF", cboStatus.SelectedItem.ToString())
        If statusText = "AKTİF" Then
            rows = rows.Where(Function(r)
                                  Dim st = DataService.GetValue(r, "Status")
                                  Return String.Equals(st, "OPEN", StringComparison.OrdinalIgnoreCase) OrElse
                                         String.Equals(st, "SEEN", StringComparison.OrdinalIgnoreCase)
                              End Function)
        ElseIf statusText = "AÇIK" Then
            rows = rows.Where(Function(r) String.Equals(DataService.GetValue(r, "Status"), "OPEN", StringComparison.OrdinalIgnoreCase))
        ElseIf statusText = "GÖRÜLDÜ" Then
            rows = rows.Where(Function(r) String.Equals(DataService.GetValue(r, "Status"), "SEEN", StringComparison.OrdinalIgnoreCase))
        ElseIf statusText = "KAPALI" Then
            rows = rows.Where(Function(r) String.Equals(DataService.GetValue(r, "Status"), "CLOSED", StringComparison.OrdinalIgnoreCase))
        End If

        Dim filterText = txtFilter.Text.Trim()
        If filterText <> "" Then
            Dim tokens = filterText.Split(New Char() {" "c, ";"c, ","c, "|"c}, StringSplitOptions.RemoveEmptyEntries)
            rows = rows.Where(Function(r)
                                  Dim haystack = (DataService.GetValue(r, "TicketId") & " " &
                                                  DataService.GetValue(r, "MachineNo") & " " &
                                                  DataService.GetValue(r, "MoldCode") & " " &
                                                  DataService.GetValue(r, "TrCode") & " " &
                                                  DataService.GetValue(r, "DrawingRev") & " " &
                                                  DataService.GetValue(r, "ProductName") & " " &
                                                  DataService.GetValue(r, "RawMaterial") & " " &
                                                  DataService.GetValue(r, "Material") & " " &
                                                  DataService.GetValue(r, "ColorName") & " " &
                                                  DataService.GetValue(r, "WorkOrderNo") & " " &
                                                  DataService.GetValue(r, "Note")).ToUpperInvariant()
                                  For Each token In tokens
                                      If Not haystack.Contains(token.ToUpperInvariant()) Then Return False
                                  Next
                                  Return True
                              End Function)
        End If

        Dim list = rows.OrderByDescending(Function(r) DataService.GetValue(r, "CreatedAt")).ToList()

        Dim dt As New DataTable()
        For Each h In DataService.ProductionTicketHeaders
            dt.Columns.Add(h)
        Next

        For Each r In list
            Dim dr = dt.NewRow()
            For Each h In DataService.ProductionTicketHeaders
                dr(h) = DataService.GetValue(r, h)
            Next
            dt.Rows.Add(dr)
        Next

        grid.DataSource = dt

        Dim openCount = allRows.Where(Function(r) String.Equals(DataService.GetValue(r, "Status"), "OPEN", StringComparison.OrdinalIgnoreCase)).Count()
        Dim seenCount = allRows.Where(Function(r) String.Equals(DataService.GetValue(r, "Status"), "SEEN", StringComparison.OrdinalIgnoreCase)).Count()
        Dim activeCount = openCount + seenCount
        Dim closedCount = allRows.Where(Function(r) String.Equals(DataService.GetValue(r, "Status"), "CLOSED", StringComparison.OrdinalIgnoreCase)).Count()
        lblCount.Text = $"Ticket: {dt.Rows.Count} gösteriliyor   |   Aktif: {activeCount}   Açık: {openCount}   Görüldü: {seenCount}   Kapalı: {closedCount}"
    End Sub

    Private Function SelectedTicketId() As String
        If grid.CurrentRow Is Nothing OrElse Not grid.Columns.Contains("TicketId") Then Return ""
        Return Convert.ToString(grid.CurrentRow.Cells("TicketId").Value)
    End Function

    Private Function SelectedCellText(columnName As String) As String
        If grid.CurrentRow Is Nothing OrElse Not grid.Columns.Contains(columnName) Then Return ""
        Return Convert.ToString(grid.CurrentRow.Cells(columnName).Value)
    End Function

    Private Sub OpenTicketMeasurements_Click(sender As Object, e As EventArgs)
        Try
            Dim ticketId = SelectedTicketId()
            If ticketId = "" Then Return

            Using f As New FrmTicketMeasurementResults(ticketId,
                                                       SelectedCellText("TrCode"),
                                                       SelectedCellText("DrawingRev"),
                                                       SelectedCellText("CreatedAt"),
                                                       SelectedCellText("ClosedAt"))
                f.ShowDialog(Me)
            End Using
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Ticket ölçümleri açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub OpenMeasurementForTicket_Click(sender As Object, e As EventArgs)
        Try
            If Not AppState.CanOpenMeasurement OrElse Not AppState.CanModifyQualityTickets Then
                MessageBox.Show("Bu rol için ölçüm girişi yetkisi yoktur." & Environment.NewLine &
                                "Ticketları görüntüleyebilir; ancak ölçüm girişi yapamaz.",
                                "Yetki yok", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim ticketId = SelectedTicketId()
            If ticketId = "" Then Return

            Dim statusText = SelectedCellText("Status")
            If String.Equals(statusText, "CLOSED", StringComparison.OrdinalIgnoreCase) Then
                OpenTicketMeasurements_Click(sender, e)
                Return
            End If

            Dim trCode = SelectedCellText("TrCode")
            Dim drawingRev = SelectedCellText("DrawingRev")

            If trCode.Trim() = "" Then
                MessageBox.Show("Seçili ticket içinde TR Kodu bulunamadı.", "TR bulunamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            If String.Equals(statusText, "OPEN", StringComparison.OrdinalIgnoreCase) Then
                DataService.MarkProductionTicketSeen(ticketId, AppState.CurrentUserName)
                AuditService.Log("PRODUCTION_TICKET_SEEN", trCode, drawingRev, "Kontrol girişi açılırken görüldü yapıldı. TicketId=" & ticketId)
            End If

            Using f As New FrmMeasurementEntry(trCode, drawingRev, ticketId)
                f.ShowDialog(Me)
            End Using

            LoadGrid()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Kontrol girişi açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub Seen_Click(sender As Object, e As EventArgs)
        Try
            AuthorizationService.Require(AppState.CanModifyQualityTickets, "Kalite Ticketını Görüldü Yapma")
            Dim ticketId = SelectedTicketId()
            If ticketId = "" Then Return
            DataService.MarkProductionTicketSeen(ticketId, AppState.CurrentUserName)
            AuditService.Log("PRODUCTION_TICKET_SEEN", "", "", "TicketId=" & ticketId)
            LoadGrid()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Ticket güncellenemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub Close_Click(sender As Object, e As EventArgs)
        Try
            AuthorizationService.Require(AppState.CanModifyQualityTickets, "Kalite Ticketını Kapatma")
            Dim ticketId = SelectedTicketId()
            If ticketId = "" Then Return

            Dim note = InputBox("Kapanış notu giriniz:", "Ticketı Kapat", "Kalite kontrol tarafından kapatıldı.")
            If note Is Nothing Then note = ""

            DataService.CloseProductionTicket(ticketId, AppState.CurrentUserName, note)
            AuditService.Log("PRODUCTION_TICKET_CLOSE", "", "", "TicketId=" & ticketId)
            LoadGrid()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Ticket kapatılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub Grid_DoubleClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 Then Return

        Dim statusText = SelectedCellText("Status")
        If String.Equals(statusText, "CLOSED", StringComparison.OrdinalIgnoreCase) OrElse
           Not AppState.CanModifyQualityTickets Then
            OpenTicketMeasurements_Click(sender, EventArgs.Empty)
        ElseIf AppState.CanOpenMeasurement Then
            OpenMeasurementForTicket_Click(sender, EventArgs.Empty)
        Else
            MessageBox.Show("Bu rol için ölçüm girişi yetkisi yoktur." & Environment.NewLine &
                            "Ticketları görüntüleyebilir; ancak ölçüm girişi yapamaz.",
                            "Yetki yok", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End If
    End Sub

    Private Sub Grid_CellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If e.RowIndex < 0 OrElse Not grid.Columns.Contains("Status") Then Return

        Dim status = Convert.ToString(grid.Rows(e.RowIndex).Cells("Status").Value)
        If String.Equals(status, "OPEN", StringComparison.OrdinalIgnoreCase) Then
            grid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.MistyRose
            grid.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.DarkRed
        ElseIf String.Equals(status, "SEEN", StringComparison.OrdinalIgnoreCase) Then
            grid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.LightYellow
            grid.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.FromArgb(90, 70, 0)
        ElseIf String.Equals(status, "CLOSED", StringComparison.OrdinalIgnoreCase) Then
            grid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.Honeydew
            grid.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.DarkGreen
        End If
    End Sub
End Class
