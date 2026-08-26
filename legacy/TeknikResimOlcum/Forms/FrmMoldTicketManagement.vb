Imports System.Data
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmMoldTicketManagement
    Inherits Form

    Private ReadOnly grid As New DataGridView()
    Private ReadOnly cboProduct As New ComboBox()
    Private ReadOnly txtProductFilter As New TextBox()
    Private ReadOnly cboMoldCode As New ComboBox()
    Private ReadOnly cboSeverity As New ComboBox()
    Private ReadOnly cboProblemType As New ComboBox()
    Private ReadOnly txtProblem As New TextBox()
    Private ReadOnly txtAction As New TextBox()
    Private ReadOnly txtListFilter As New TextBox()
    Private ReadOnly cboStatus As New ComboBox()
    Private ReadOnly lblCount As New Label()

    Private allProducts As New List(Of ProductInfo)()

    Public Sub New(Optional initialMoldCode As String = "")
        AuthorizationService.Require(AppState.CanOpenMoldTickets, "Kalip Ticketlari")
        AppIconService.Apply(Me)
        Text = "Kalıp Ticketları"
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        Size = New Size(1450, 820)
        MinimumSize = New Size(760, 520)
        BackColor = Color.White

        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .BackColor = Color.White
        }
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 64.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 46.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Controls.Add(layout)

        Dim top As New Panel() With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(12),
            .BackColor = SystemColors.Control,
            .AutoScroll = True,
            .AutoScrollMinSize = New Size(1270, 245)
        }
        layout.Controls.Add(top, 0, 0)

        Dim title As New Label() With {
            .Text = "Kalıp Kaynaklı Sorun / Kalıp Ticket Aç",
            .Left = 15,
            .Top = 12,
            .Width = 650,
            .Height = 28,
            .Font = New Font("Segoe UI", 13.0F, FontStyle.Bold),
            .BackColor = Color.Transparent
        }

        AddLabel(top, "TR / Revizyon", 15, 55, 110)
        cboProduct.SetBounds(130, 52, 330, 27)
        cboProduct.DropDownStyle = ComboBoxStyle.DropDownList
        AddHandler cboProduct.SelectedIndexChanged, AddressOf ProductChanged

        AddLabel(top, "TR Filtre", 480, 55, 70)
        txtProductFilter.SetBounds(555, 52, 210, 27)
        txtProductFilter.PlaceholderText = "TR / ürün / kalıp"
        AddHandler txtProductFilter.TextChanged, Sub() ApplyProductFilter()

        AddLabel(top, "Kalıp Kodu", 15, 95, 110)
        cboMoldCode.SetBounds(130, 92, 210, 27)
        cboMoldCode.DropDownStyle = ComboBoxStyle.DropDown
        cboMoldCode.AutoCompleteMode = AutoCompleteMode.SuggestAppend
        cboMoldCode.AutoCompleteSource = AutoCompleteSource.ListItems

        AddLabel(top, "Önem", 365, 95, 55)
        cboSeverity.SetBounds(435, 92, 125, 27)
        cboSeverity.DropDownStyle = ComboBoxStyle.DropDownList
        cboSeverity.Items.AddRange({"KRİTİK", "YÜKSEK", "ORTA", "DÜŞÜK"})
        cboSeverity.SelectedIndex = 2

        AddLabel(top, "Sorun Tipi", 590, 95, 80)
        cboProblemType.SetBounds(675, 92, 210, 27)
        cboProblemType.DropDownStyle = ComboBoxStyle.DropDownList
        cboProblemType.Items.AddRange({"PARLATMA", "ÇAPAK", "ÖLÇÜ KAÇIĞI", "KIRIK / HASAR", "YÜZEY HATASI", "GÖZ PROBLEMİ", "KALIP SOĞUTMA", "KALIP MEKANİK", "DİĞER"})
        cboProblemType.SelectedIndex = 0

        AddLabel(top, "Sorun", 15, 135, 110)
        txtProblem.SetBounds(130, 132, 755, 42)
        txtProblem.Multiline = True
        txtProblem.ScrollBars = ScrollBars.Vertical
        txtProblem.PlaceholderText = "Kalıp kaynaklı sorun açıklaması"

        AddLabel(top, "Aksiyon", 15, 185, 110)
        txtAction.SetBounds(130, 182, 755, 42)
        txtAction.Multiline = True
        txtAction.ScrollBars = ScrollBars.Vertical
        txtAction.PlaceholderText = "Yapılacak işlem / bakım / kalıpçı notu"

        Dim btnCreate As New Button() With {.Text = "Kalıba Ticket Aç", .Left = 910, .Top = 92, .Width = 160, .Height = 36}
        btnCreate.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        AddHandler btnCreate.Click, AddressOf Create_Click

        Dim btnClose As New Button() With {.Text = "Seçili Ticketı Kapat", .Left = 910, .Top = 138, .Width = 160, .Height = 36}
        AddHandler btnClose.Click, AddressOf Close_Click

        Dim btnClear As New Button() With {.Text = "Temizle", .Left = 910, .Top = 184, .Width = 160, .Height = 36}
        AddHandler btnClear.Click, Sub() ClearInputs()

        Dim btnDelete As New Button() With {
            .Text = "Seçili Ticketı Sil",
            .Left = 1085,
            .Top = 138,
            .Width = 170,
            .Height = 36,
            .BackColor = Color.MistyRose,
            .ForeColor = Color.DarkRed,
            .FlatStyle = FlatStyle.Flat
        }
        btnDelete.FlatAppearance.BorderColor = Color.Firebrick
        AddHandler btnDelete.Click, AddressOf Delete_Click

        Dim lblMultiMoldInfo As New Label() With {
            .Text = "Not: TR birden fazla kalıpta basılıyorsa Kalıp Kodu listesinden doğru kalıbı seçiniz.",
            .Left = 130,
            .Top = 225,
            .Width = 755,
            .Height = 20,
            .ForeColor = Color.DimGray,
            .BackColor = Color.Transparent
        }

        top.Controls.AddRange({title, cboProduct, txtProductFilter, cboMoldCode, cboSeverity, cboProblemType, txtProblem, txtAction})

        If AppState.CanModifyMoldTickets Then
            top.Controls.AddRange({btnCreate, btnClose, btnClear})
            If AppState.CanDeleteMoldTickets Then
                top.Controls.Add(btnDelete)
            End If
        Else
            txtProblem.ReadOnly = True
            txtAction.ReadOnly = True
            cboProduct.Enabled = False
            txtProductFilter.Enabled = False
            cboMoldCode.Enabled = False
            cboSeverity.Enabled = False
            cboProblemType.Enabled = False

            top.Controls.Add(New Label() With {
                .Text = "Sadece görüntüleme",
                .Left = 910,
                .Top = 100,
                .Width = 160,
                .Height = 28,
                .ForeColor = Color.DimGray,
                .BackColor = Color.Transparent,
                .TextAlign = ContentAlignment.MiddleLeft
            })
        End If

        top.Controls.Add(lblMultiMoldInfo)

        top.Controls.Clear()
        top.AutoScroll = False
        top.AutoScrollMinSize = Size.Empty
        top.BackColor = Color.WhiteSmoke
        top.Padding = New Padding(12, 10, 12, 8)

        Dim nextLeft As Integer = 12
        If AppState.CanModifyMoldTickets Then
            Dim btnNewTicket = NewToolbarButton("Yeni Ticket", nextLeft, 130, Color.FromArgb(15, 120, 65), Color.White)
            AddHandler btnNewTicket.Click, AddressOf NewTicket_Click
            top.Controls.Add(btnNewTicket)
            nextLeft += btnNewTicket.Width + 10
        End If

        Dim btnDetail = NewToolbarButton("Detay Aç", nextLeft, 120, Color.FromArgb(37, 78, 125), Color.White)
        AddHandler btnDetail.Click, AddressOf Detail_Click
        top.Controls.Add(btnDetail)
        nextLeft += btnDetail.Width + 10

        If AppState.CanModifyMoldTickets Then
            Dim btnCloseTicket = NewToolbarButton("Seçili Ticketı Kapat", nextLeft, 165, Color.White, Color.FromArgb(15, 40, 70))
            AddHandler btnCloseTicket.Click, AddressOf Close_Click
            top.Controls.Add(btnCloseTicket)
            nextLeft += btnCloseTicket.Width + 10
        End If

        If AppState.CanDeleteMoldTickets Then
            Dim btnDeleteTicket = NewToolbarButton("Seçili Ticketı Sil", nextLeft, 150, Color.MistyRose, Color.DarkRed)
            btnDeleteTicket.FlatAppearance.BorderColor = Color.Firebrick
            AddHandler btnDeleteTicket.Click, AddressOf Delete_Click
            top.Controls.Add(btnDeleteTicket)
            nextLeft += btnDeleteTicket.Width + 10
        End If

        Dim btnToolbarRefresh = NewToolbarButton("Yenile", nextLeft, 95, Color.White, Color.FromArgb(15, 40, 70))
        AddHandler btnToolbarRefresh.Click, Sub() LoadGrid()
        top.Controls.Add(btnToolbarRefresh)

        Dim hintText = If(AppState.IsAdmin,
                          "Listeden çift tıklayarak detayı açın. Admin mevcut ticket detaylarını düzenleyebilir.",
                          "Listeden çift tıklayarak detayı görüntüleyin.")
        Dim lblHint As New Label() With {
            .Text = hintText,
            .Left = nextLeft + btnToolbarRefresh.Width + 18,
            .Top = 19,
            .Width = 720,
            .Height = 24,
            .ForeColor = Color.FromArgb(60, 70, 85),
            .BackColor = Color.Transparent,
            .AutoEllipsis = True
        }
        top.Controls.Add(lblHint)

        Dim filterPanel As New Panel() With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(12, 7, 12, 4),
            .BackColor = Color.WhiteSmoke,
            .AutoScroll = True,
            .AutoScrollMinSize = New Size(1020, 38)
        }
        layout.Controls.Add(filterPanel, 0, 1)

        lblCount.SetBounds(15, 9, 330, 22)
        lblCount.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)

        filterPanel.Controls.Add(New Label() With {.Text = "Liste Filtresi", .Left = 360, .Top = 10, .Width = 85, .Height = 20, .BackColor = Color.Transparent})
        txtListFilter.SetBounds(450, 7, 260, 25)
        txtListFilter.PlaceholderText = "kalıp / TR / sorun / durum"
        AddHandler txtListFilter.TextChanged, Sub() LoadGrid()

        filterPanel.Controls.Add(New Label() With {.Text = "Durum", .Left = 730, .Top = 10, .Width = 55, .Height = 20, .BackColor = Color.Transparent})
        cboStatus.SetBounds(790, 7, 110, 25)
        cboStatus.DropDownStyle = ComboBoxStyle.DropDownList
        cboStatus.Items.AddRange({"AÇIK", "KAPALI", "TÜMÜ"})
        cboStatus.SelectedIndex = 0
        AddHandler cboStatus.SelectedIndexChanged, Sub() LoadGrid()

        Dim btnRefresh As New Button() With {.Text = "Yenile", .Left = 920, .Top = 6, .Width = 90, .Height = 27}
        AddHandler btnRefresh.Click, Sub() LoadGrid()

        filterPanel.Controls.AddRange({lblCount, txtListFilter, cboStatus, btnRefresh})

        ConfigureGrid()
        layout.Controls.Add(grid, 0, 2)

        If initialMoldCode.Trim() <> "" Then
            txtListFilter.Text = initialMoldCode.Trim()
        End If
        LoadGrid()
    End Sub

    Private Function NewToolbarButton(text As String, left As Integer, width As Integer, backColor As Color, foreColor As Color) As Button
        Dim button As New Button() With {
            .Text = text,
            .Left = left,
            .Top = 12,
            .Width = width,
            .Height = 38,
            .BackColor = backColor,
            .ForeColor = foreColor,
            .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        }
        button.FlatAppearance.BorderColor = Color.FromArgb(185, 198, 214)
        button.FlatAppearance.BorderSize = 1
        Return button
    End Function

    Private Sub AddLabel(parent As Control, text As String, x As Integer, y As Integer, width As Integer)
        parent.Controls.Add(New Label() With {.Text = text, .Left = x, .Top = y + 3, .Width = width, .Height = 22, .BackColor = Color.Transparent})
    End Sub

    Private Sub LoadProducts()
        allProducts = DataService.GetProducts(True)
        ApplyProductFilter()
    End Sub

    Private Sub ApplyProductFilter()
        Dim selectedKey As String = ""
        Dim current = SelectedProduct()
        If current IsNot Nothing Then selectedKey = current.TrCode & "|" & current.DrawingRev & "|" & current.DrawingFile

        Dim filterText = txtProductFilter.Text.Trim()
        Dim filtered As List(Of ProductInfo)
        If filterText = "" Then
            filtered = allProducts.ToList()
        Else
            Dim tokens = filterText.Split(New Char() {" "c, ";"c, ","c, "|"c}, StringSplitOptions.RemoveEmptyEntries)
            filtered = allProducts.Where(Function(p)
                                             Dim haystack = (p.TrCode & " " & p.DrawingRev & " " & p.ProductName & " " & p.MoldCode & " " & p.Material).ToUpperInvariant()
                                             For Each token In tokens
                                                 If Not haystack.Contains(token.ToUpperInvariant()) Then Return False
                                             Next
                                             Return True
                                         End Function).ToList()
        End If

        cboProduct.DataSource = Nothing
        cboProduct.DisplayMember = "DisplayName"
        cboProduct.DataSource = filtered
        cboProduct.DisplayMember = "DisplayName"

        If filtered.Count > 0 Then
            Dim restoreIndex = filtered.FindIndex(Function(p) (p.TrCode & "|" & p.DrawingRev & "|" & p.DrawingFile) = selectedKey)
            cboProduct.SelectedIndex = If(restoreIndex >= 0, restoreIndex, 0)
        End If
    End Sub

    Private Function SelectedProduct() As ProductInfo
        Return TryCast(cboProduct.SelectedItem, ProductInfo)
    End Function

    Private Sub FillMoldCodeComboFromProduct(p As ProductInfo)
        Dim currentText = cboMoldCode.Text.Trim()
        Dim moldCodes = ParseMoldCodes(If(p Is Nothing, "", p.MoldCode))

        cboMoldCode.Items.Clear()
        For Each moldCode In moldCodes
            cboMoldCode.Items.Add(moldCode)
        Next

        If currentText <> "" AndAlso moldCodes.Any(Function(x) String.Equals(x, currentText, StringComparison.OrdinalIgnoreCase)) Then
            cboMoldCode.Text = currentText
        ElseIf moldCodes.Count = 1 Then
            cboMoldCode.SelectedIndex = 0
        ElseIf moldCodes.Count > 1 Then
            cboMoldCode.SelectedIndex = 0
        ElseIf currentText <> "" Then
            cboMoldCode.Text = currentText
        Else
            cboMoldCode.Text = ""
        End If
    End Sub

    Private Function ParseMoldCodes(rawText As String) As List(Of String)
        Dim result As New List(Of String)()
        If rawText Is Nothing Then Return result

        Dim cleaned = rawText.Replace(vbCr, ";").Replace(vbLf, ";").Replace("/", ";").Replace(",", ";").Replace("|", ";")
        For Each part In cleaned.Split(";"c)
            Dim value = part.Trim()
            If value = "" Then Continue For
            If Not result.Any(Function(x) String.Equals(x, value, StringComparison.OrdinalIgnoreCase)) Then
                result.Add(value)
            End If
        Next

        Return result
    End Function

    Private Sub ProductChanged(sender As Object, e As EventArgs)
        Dim p = SelectedProduct()
        If p Is Nothing Then Return
        FillMoldCodeComboFromProduct(p)
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
        grid.Columns.Add(MakeColumn("MoldTicketId", "Ticket No", 170))
        grid.Columns.Add(MakeColumn("Status", "Durum", 80))
        grid.Columns.Add(MakeColumn("CreatedAt", "Açılış Tarihi", 135))
        grid.Columns.Add(MakeColumn("CreatedBy", "Açan", 110))
        grid.Columns.Add(MakeColumn("MoldCode", "Kalıp Kodu", 110))
        grid.Columns.Add(MakeColumn("TrCode", "TR Kodu", 95))
        grid.Columns.Add(MakeColumn("DrawingRev", "Rev.", 65))
        grid.Columns.Add(MakeColumn("ProductName", "Ürün Adı", 150))
        grid.Columns.Add(MakeColumn("Severity", "Önem", 85))
        grid.Columns.Add(MakeColumn("ProblemType", "Sorun Tipi", 130))
        grid.Columns.Add(MakeColumn("ProblemDescription", "Sorun Açıklaması", 260))
        grid.Columns.Add(MakeColumn("ActionPlan", "Aksiyon", 220))
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
        Dim allRows = DataService.GetMoldTickets()
        Dim rows = allRows.AsEnumerable()

        Dim statusText = If(cboStatus.SelectedItem Is Nothing, "AÇIK", cboStatus.SelectedItem.ToString())
        If statusText = "AÇIK" Then
            rows = rows.Where(Function(r) String.Equals(DataService.GetValue(r, "Status"), "OPEN", StringComparison.OrdinalIgnoreCase))
        ElseIf statusText = "KAPALI" Then
            rows = rows.Where(Function(r) String.Equals(DataService.GetValue(r, "Status"), "CLOSED", StringComparison.OrdinalIgnoreCase))
        End If

        Dim filterText = txtListFilter.Text.Trim()
        If filterText <> "" Then
            Dim tokens = filterText.Split(New Char() {" "c, ";"c, ","c, "|"c}, StringSplitOptions.RemoveEmptyEntries)
            rows = rows.Where(Function(r)
                                  Dim haystack = (DataService.GetValue(r, "MoldTicketId") & " " &
                                                  DataService.GetValue(r, "Status") & " " &
                                                  DataService.GetValue(r, "MoldCode") & " " &
                                                  DataService.GetValue(r, "TrCode") & " " &
                                                  DataService.GetValue(r, "DrawingRev") & " " &
                                                  DataService.GetValue(r, "ProductName") & " " &
                                                  DataService.GetValue(r, "Severity") & " " &
                                                  DataService.GetValue(r, "ProblemType") & " " &
                                                  DataService.GetValue(r, "ProblemDescription") & " " &
                                                  DataService.GetValue(r, "ActionPlan")).ToUpperInvariant()
                                  For Each token In tokens
                                      If Not haystack.Contains(token.ToUpperInvariant()) Then Return False
                                  Next
                                  Return True
                              End Function)
        End If

        Dim list = rows.OrderByDescending(Function(r) DataService.GetValue(r, "CreatedAt")).ToList()

        Dim dt As New DataTable()
        For Each h In DataService.MoldTicketHeaders
            dt.Columns.Add(h)
        Next

        For Each r In list
            Dim dr = dt.NewRow()
            For Each h In DataService.MoldTicketHeaders
                dr(h) = DataService.GetValue(r, h)
            Next
            dt.Rows.Add(dr)
        Next

        grid.DataSource = dt

        Dim openCount = allRows.Where(Function(r) String.Equals(DataService.GetValue(r, "Status"), "OPEN", StringComparison.OrdinalIgnoreCase)).Count()
        Dim closedCount = allRows.Where(Function(r) String.Equals(DataService.GetValue(r, "Status"), "CLOSED", StringComparison.OrdinalIgnoreCase)).Count()
        lblCount.Text = $"Kalıp Ticket: {dt.Rows.Count} gösteriliyor   |   Açık: {openCount}   Kapalı: {closedCount}"
    End Sub

    Private Sub Create_Click(sender As Object, e As EventArgs)
        Try
            If Not AppState.CanModifyMoldTickets Then
                MessageBox.Show("Bu rol için kalıp ticketına müdahale yetkisi yoktur. Sadece görüntüleme yapılabilir.",
                                "Yetki yok", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim p = SelectedProduct()

            If cboMoldCode.Text.Trim() = "" Then
                MessageBox.Show("Kalıp Kodu zorunludur.", "Eksik bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                cboMoldCode.Focus()
                Return
            End If

            If txtProblem.Text.Trim() = "" Then
                MessageBox.Show("Sorun açıklaması zorunludur.", "Eksik bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                txtProblem.Focus()
                Return
            End If

            Dim ticketId = "KLP-" & DateTime.Now.ToString("yyyyMMdd-HHmmss") & "-" & Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant()

            Dim row As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                {"MoldTicketId", ticketId},
                {"Status", "OPEN"},
                {"CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")},
                {"CreatedBy", AppState.CurrentUserName},
                {"ComputerName", Environment.MachineName},
                {"MoldCode", cboMoldCode.Text.Trim()},
                {"TrCode", If(p Is Nothing, "", p.TrCode)},
                {"DrawingRev", If(p Is Nothing, "", p.DrawingRev)},
                {"ProductName", If(p Is Nothing, "", p.ProductName)},
                {"Severity", cboSeverity.Text},
                {"ProblemType", cboProblemType.Text},
                {"ProblemDescription", txtProblem.Text.Trim()},
                {"ActionPlan", txtAction.Text.Trim()},
                {"ClosedBy", ""},
                {"ClosedAt", ""},
                {"CloseNote", ""}
            }

            DataService.AppendMoldTicket(row)
            AuditService.Log("MOLD_TICKET_CREATE", If(p Is Nothing, "", p.TrCode), If(p Is Nothing, "", p.DrawingRev), $"MoldTicketId={ticketId}; Mold={cboMoldCode.Text.Trim()}; Severity={cboSeverity.Text}")

            MessageBox.Show("Kalıp ticket açıldı. Üretim bu kalıbı bağlarken uyarı görecek." & Environment.NewLine & "Ticket No: " & ticketId,
                            "Kalıp ticket açıldı", MessageBoxButtons.OK, MessageBoxIcon.Information)
            ClearInputs()
            LoadGrid()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Kalıp ticket açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub NewTicket_Click(sender As Object, e As EventArgs)
        Try
            If Not AppState.CanModifyMoldTickets Then
                MessageBox.Show("Bu rol için kalıp ticketı oluşturma yetkisi yoktur.", "Yetki yok", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Using detail As New FrmMoldTicketDetail()
                If detail.ShowDialog(Me) = DialogResult.OK Then
                    LoadGrid()
                End If
            End Using
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Yeni ticket açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub Detail_Click(sender As Object, e As EventArgs)
        OpenSelectedDetail()
    End Sub

    Private Sub OpenSelectedDetail()
        Try
            Dim row = SelectedMoldTicketRow()
            If row Is Nothing Then
                MessageBox.Show("Lütfen detayını açmak istediğiniz kalıp ticketını seçin.", "Kayıt seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Using detail As New FrmMoldTicketDetail(row)
                If detail.ShowDialog(Me) = DialogResult.OK Then
                    LoadGrid()
                End If
            End Using
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Ticket detayı açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Function SelectedMoldTicketId() As String
        If grid.CurrentRow Is Nothing OrElse Not grid.Columns.Contains("MoldTicketId") Then Return ""
        Return Convert.ToString(grid.CurrentRow.Cells("MoldTicketId").Value)
    End Function

    Private Function SelectedMoldTicketRow() As Dictionary(Of String, String)
        Dim ticketId = SelectedMoldTicketId()
        If ticketId.Trim() = "" Then Return Nothing
        Return DataService.GetMoldTicketById(ticketId)
    End Function

    Private Sub Close_Click(sender As Object, e As EventArgs)
        Try
            If Not AppState.CanModifyMoldTickets Then
                MessageBox.Show("Bu rol için kalıp ticketına müdahale yetkisi yoktur. Sadece görüntüleme yapılabilir.",
                                "Yetki yok", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim ticketId = SelectedMoldTicketId()
            If ticketId = "" Then Return

            Dim note = InputBox("Sorun giderildiyse kapanış notu giriniz:", "Kalıp Ticket Kapat", "Sorun giderildi.")
            If note Is Nothing Then note = ""

            DataService.CloseMoldTicket(ticketId, AppState.CurrentUserName, note)
            AuditService.Log("MOLD_TICKET_CLOSE", "", "", "MoldTicketId=" & ticketId)
            LoadGrid()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Kalıp ticket kapatılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub Delete_Click(sender As Object, e As EventArgs)
        Try
            AuthorizationService.Require(AppState.CanDeleteMoldTickets, "Kalıp Ticketı Silme")

            If grid.CurrentRow Is Nothing Then
                MessageBox.Show("Lütfen silinecek kalıp ticketını seçin.", "Kayıt seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim ticketId = SelectedMoldTicketId()
            If ticketId = "" Then
                MessageBox.Show("Seçili satırın ticket numarası bulunamadı.", "Kayıt seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim moldCode = If(grid.Columns.Contains("MoldCode"), Convert.ToString(grid.CurrentRow.Cells("MoldCode").Value), "")
            Dim trCode = If(grid.Columns.Contains("TrCode"), Convert.ToString(grid.CurrentRow.Cells("TrCode").Value), "")
            Dim statusText = If(grid.Columns.Contains("Status"), Convert.ToString(grid.CurrentRow.Cells("Status").Value), "")
            Dim confirmText = "Seçili kalıp ticketı silinecek." & Environment.NewLine & Environment.NewLine &
                              "Ticket No: " & ticketId & Environment.NewLine &
                              "Kalıp: " & If(moldCode.Trim() = "", "-", moldCode) & Environment.NewLine &
                              "TR: " & If(trCode.Trim() = "", "-", trCode) & Environment.NewLine &
                              "Durum: " & If(statusText.Trim() = "", "-", statusText) & Environment.NewLine & Environment.NewLine &
                              "Bu işlem geri alınamaz. Devam edilsin mi?"

            If MessageBox.Show(confirmText, "Kalıp ticketı silinsin mi?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) <> DialogResult.Yes Then
                Return
            End If

            DataService.DeleteMoldTicket(ticketId)
            LoadGrid()
            MessageBox.Show("Kalıp ticketı silindi.", "Silme tamamlandı", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Kalıp ticket silinemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub Grid_DoubleClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 Then Return
        grid.CurrentCell = grid.Rows(e.RowIndex).Cells(0)
        OpenSelectedDetail()
    End Sub

    Private Sub Grid_CellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If e.RowIndex < 0 OrElse Not grid.Columns.Contains("Status") Then Return

        Dim status = Convert.ToString(grid.Rows(e.RowIndex).Cells("Status").Value)
        Dim severity = If(grid.Columns.Contains("Severity"), Convert.ToString(grid.Rows(e.RowIndex).Cells("Severity").Value), "")

        If String.Equals(status, "OPEN", StringComparison.OrdinalIgnoreCase) Then
            If String.Equals(severity, "KRİTİK", StringComparison.OrdinalIgnoreCase) OrElse String.Equals(severity, "YÜKSEK", StringComparison.OrdinalIgnoreCase) Then
                grid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.MistyRose
                grid.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.DarkRed
            Else
                grid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.LightYellow
                grid.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.FromArgb(80, 65, 0)
            End If
        ElseIf String.Equals(status, "CLOSED", StringComparison.OrdinalIgnoreCase) Then
            grid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.Honeydew
            grid.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.DarkGreen
        End If
    End Sub

    Private Sub ClearInputs()
        txtProblem.Clear()
        txtAction.Clear()
        If cboSeverity.Items.Count > 0 Then cboSeverity.SelectedIndex = 2
        If cboProblemType.Items.Count > 0 Then cboProblemType.SelectedIndex = 0
    End Sub
End Class
