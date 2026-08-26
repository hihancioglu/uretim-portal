Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmMoldTicketDetail
    Inherits Form

    Private ReadOnly isNew As Boolean
    Private ReadOnly originalRow As Dictionary(Of String, String)
    Private allProducts As New List(Of ProductInfo)()

    Private ReadOnly txtTicketId As New TextBox()
    Private ReadOnly txtStatus As New TextBox()
    Private ReadOnly txtCreatedAt As New TextBox()
    Private ReadOnly txtCreatedBy As New TextBox()
    Private ReadOnly txtClosedAt As New TextBox()
    Private ReadOnly txtClosedBy As New TextBox()
    Private ReadOnly txtProductFilter As New TextBox()
    Private ReadOnly cboProduct As New ComboBox()
    Private ReadOnly cboMoldCode As New ComboBox()
    Private ReadOnly cboSeverity As New ComboBox()
    Private ReadOnly cboProblemType As New ComboBox()
    Private ReadOnly txtProblem As New TextBox()
    Private ReadOnly txtAction As New TextBox()
    Private ReadOnly txtCloseNote As New TextBox()
    Private ReadOnly lblMode As New Label()
    Private ReadOnly lblRelatedProducts As New Label()

    Private btnSave As Button
    Private btnCloseTicket As Button
    Private btnDelete As Button

    Public Sub New(Optional row As Dictionary(Of String, String) = Nothing)
        AuthorizationService.Require(AppState.CanOpenMoldTickets, "Kalıp Ticket Detayı")

        originalRow = If(row, New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase))
        isNew = String.IsNullOrWhiteSpace(DataService.GetValue(originalRow, "MoldTicketId"))

        AppIconService.Apply(Me)
        Text = If(isNew, "Yeni Kalıp Ticketı", "Kalıp Ticket Detayı")
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        Size = New Size(1280, 760)
        MinimumSize = New Size(980, 640)
        BackColor = Color.White

        BuildScreen()
        LoadProducts()
        FillFromRow()
        ApplyPermissionState()
    End Sub

    Private Sub BuildScreen()
        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 5,
            .BackColor = Color.White
        }
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 54.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 78.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 166.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 58.0F))
        Controls.Add(layout)

        Dim header As New Panel() With {
            .Dock = DockStyle.Fill,
            .BackColor = Color.FromArgb(37, 78, 125),
            .Padding = New Padding(24, 8, 24, 8)
        }
        Dim title As New Label() With {
            .Text = If(isNew, "Yeni Kalıp Ticketı", "Kalıp Ticket Detayı"),
            .Dock = DockStyle.Left,
            .Width = 460,
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 14.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft
        }
        lblMode.Dock = DockStyle.Right
        lblMode.Width = 360
        lblMode.ForeColor = Color.White
        lblMode.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        lblMode.TextAlign = ContentAlignment.MiddleRight
        header.Controls.Add(title)
        header.Controls.Add(lblMode)
        layout.Controls.Add(header, 0, 0)

        Dim summary As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 6,
            .RowCount = 1,
            .Padding = New Padding(18, 10, 18, 8),
            .BackColor = Color.FromArgb(245, 248, 252)
        }
        For i = 1 To 6
            summary.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 16.666F))
        Next
        summary.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.Controls.Add(summary, 0, 1)

        AddSummaryCard(summary, "Ticket No", txtTicketId, 0)
        AddSummaryCard(summary, "Durum", txtStatus, 1)
        AddSummaryCard(summary, "Açan", txtCreatedBy, 2)
        AddSummaryCard(summary, "Açılış", txtCreatedAt, 3)
        AddSummaryCard(summary, "Kapatan", txtClosedBy, 4)
        AddSummaryCard(summary, "Kapanış", txtClosedAt, 5)

        Dim formArea As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 4,
            .RowCount = 4,
            .Padding = New Padding(20, 10, 20, 10),
            .BackColor = Color.White
        }
        formArea.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 120.0F))
        formArea.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        formArea.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 120.0F))
        formArea.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        formArea.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
        formArea.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
        formArea.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
        formArea.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        layout.Controls.Add(formArea, 0, 2)

        AddBodyLabel(formArea, "TR Filtre", 0, 0)
        StyleInput(txtProductFilter, "TR / ürün / kalıp")
        AddHandler txtProductFilter.TextChanged, Sub() ApplyProductFilter()
        AddLimitedInput(formArea, txtProductFilter, 1, 0)

        AddBodyLabel(formArea, "TR / Revizyon", 0, 1)
        StyleCombo(cboProduct, ComboBoxStyle.DropDownList)
        AddHandler cboProduct.SelectedIndexChanged, AddressOf ProductChanged
        AddLimitedInput(formArea, cboProduct, 1, 1)

        AddBodyLabel(formArea, "Kalıp Kodu", 0, 2)
        StyleCombo(cboMoldCode, ComboBoxStyle.DropDown)
        cboMoldCode.AutoCompleteMode = AutoCompleteMode.SuggestAppend
        cboMoldCode.AutoCompleteSource = AutoCompleteSource.ListItems
        AddHandler cboMoldCode.TextChanged, Sub() UpdateRelatedProductInfo()
        AddLimitedInput(formArea, cboMoldCode, 1, 2)

        AddBodyLabel(formArea, "Önem", 2, 1)
        StyleCombo(cboSeverity, ComboBoxStyle.DropDownList)
        cboSeverity.Items.AddRange(New Object() {"KRİTİK", "YÜKSEK", "ORTA", "DÜŞÜK"})
        cboSeverity.SelectedIndex = 2
        AddLimitedInput(formArea, cboSeverity, 3, 1)

        AddBodyLabel(formArea, "Sorun Tipi", 2, 0)
        StyleCombo(cboProblemType, ComboBoxStyle.DropDownList)
        cboProblemType.Items.AddRange(New Object() {"PARLATMA", "ÇAPAK", "ÖLÇÜ KAÇIĞI", "KIRIK / HASAR", "YÜZEY HATASI", "GÖZ PROBLEMİ", "KALIP SOĞUTMA", "KALIP MEKANİK", "DİĞER"})
        cboProblemType.SelectedIndex = 0
        AddLimitedInput(formArea, cboProblemType, 3, 0)

        AddBodyLabel(formArea, "Bağlı TR'ler", 0, 3)

        lblRelatedProducts.Dock = DockStyle.Fill
        lblRelatedProducts.ForeColor = Color.FromArgb(70, 85, 105)
        lblRelatedProducts.BackColor = Color.FromArgb(245, 248, 252)
        lblRelatedProducts.TextAlign = ContentAlignment.MiddleLeft
        lblRelatedProducts.Padding = New Padding(10, 0, 10, 0)
        lblRelatedProducts.AutoEllipsis = True
        lblRelatedProducts.Margin = New Padding(0, 3, 10, 5)
        formArea.Controls.Add(lblRelatedProducts, 1, 3)
        formArea.SetColumnSpan(lblRelatedProducts, 3)

        Dim textArea As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 2,
            .Padding = New Padding(20, 6, 20, 10),
            .BackColor = Color.White
        }
        textArea.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        textArea.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        textArea.RowStyles.Add(New RowStyle(SizeType.Percent, 65.0F))
        textArea.RowStyles.Add(New RowStyle(SizeType.Percent, 35.0F))
        layout.Controls.Add(textArea, 0, 3)

        txtProblem.PlaceholderText = "Kalıp kaynaklı sorun açıklaması"
        textArea.Controls.Add(MakeTextSection("Sorun Açıklaması", txtProblem), 0, 0)

        txtAction.PlaceholderText = "Yapılacak işlem / bakım / kalıpçı notu"
        textArea.Controls.Add(MakeTextSection("Aksiyon", txtAction), 1, 0)

        txtCloseNote.PlaceholderText = "Ticket kapatıldıysa kapanış notu"
        Dim closeSection = MakeTextSection("Kapanış Notu", txtCloseNote)
        textArea.Controls.Add(closeSection, 0, 1)
        textArea.SetColumnSpan(closeSection, 2)

        Dim footer As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.RightToLeft,
            .Padding = New Padding(16, 10, 16, 8),
            .BackColor = Color.WhiteSmoke,
            .WrapContents = False
        }
        layout.Controls.Add(footer, 0, 4)

        Dim btnCancel As Button = MakeFooterButton(If(isNew, "Vazgeç", "Kapat"), Color.White, Color.Black, 110)
        AddHandler btnCancel.Click, Sub()
                                        DialogResult = DialogResult.Cancel
                                        Close()
                                    End Sub
        footer.Controls.Add(btnCancel)

        btnSave = MakeFooterButton(If(isNew, "Ticket Oluştur", "Kaydet / Güncelle"), Color.FromArgb(37, 78, 125), Color.White, 150)
        AddHandler btnSave.Click, AddressOf Save_Click
        footer.Controls.Add(btnSave)

        btnCloseTicket = MakeFooterButton("Ticketı Kapat", Color.Honeydew, Color.DarkGreen, 135)
        AddHandler btnCloseTicket.Click, AddressOf CloseTicket_Click
        footer.Controls.Add(btnCloseTicket)

        btnDelete = MakeFooterButton("Ticketı Sil", Color.MistyRose, Color.DarkRed, 115)
        AddHandler btnDelete.Click, AddressOf DeleteTicket_Click
        footer.Controls.Add(btnDelete)
    End Sub

    Private Sub AddSummaryCard(parent As TableLayoutPanel, labelText As String, textBox As TextBox, col As Integer)
        Dim card As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .Margin = New Padding(5, 0, 5, 0),
            .Padding = New Padding(8, 3, 8, 4),
            .BackColor = Color.White
        }
        card.RowStyles.Add(New RowStyle(SizeType.Absolute, 20.0F))
        card.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim label As New Label() With {
            .Text = labelText,
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Font = New Font("Segoe UI", 8.0F, FontStyle.Bold),
            .ForeColor = Color.FromArgb(70, 85, 105)
        }
        card.Controls.Add(label, 0, 0)

        textBox.Dock = DockStyle.Fill
        textBox.ReadOnly = True
        textBox.BackColor = Color.White
        textBox.BorderStyle = BorderStyle.FixedSingle
        textBox.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        textBox.Margin = New Padding(0)
        card.Controls.Add(textBox, 0, 1)

        parent.Controls.Add(card, col, 0)
    End Sub

    Private Sub AddBodyLabel(parent As TableLayoutPanel, text As String, col As Integer, row As Integer)
        parent.Controls.Add(New Label() With {
            .Text = text,
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Font = New Font("Segoe UI", 8.8F, FontStyle.Bold),
            .Margin = New Padding(0, 0, 8, 0)
        }, col, row)
    End Sub

    Private Sub AddLimitedInput(parent As TableLayoutPanel, inputControl As Control, col As Integer, row As Integer, Optional maxWidth As Integer = 620)
        Dim host As New Panel() With {
            .Dock = DockStyle.Fill,
            .Margin = New Padding(0),
            .Padding = New Padding(0, 0, 10, 0),
            .BackColor = Color.White
        }

        inputControl.Dock = DockStyle.None
        inputControl.Anchor = AnchorStyles.Left Or AnchorStyles.Top
        inputControl.Left = 0
        inputControl.Top = 3
        inputControl.Width = maxWidth
        inputControl.Margin = New Padding(0)

        AddHandler host.Resize,
            Sub()
                inputControl.Width = Math.Max(80, Math.Min(maxWidth, host.ClientSize.Width - 10))
            End Sub

        host.Controls.Add(inputControl)
        parent.Controls.Add(host, col, row)
    End Sub

    Private Sub StyleInput(textBox As TextBox, placeholder As String)
        textBox.Dock = DockStyle.Fill
        textBox.Margin = New Padding(0, 3, 10, 4)
        textBox.PlaceholderText = placeholder
        textBox.BorderStyle = BorderStyle.FixedSingle
    End Sub

    Private Sub StyleCombo(comboBox As ComboBox, style As ComboBoxStyle)
        comboBox.Dock = DockStyle.Fill
        comboBox.Margin = New Padding(0, 3, 10, 4)
        comboBox.DropDownStyle = style
    End Sub

    Private Function MakeTextSection(titleText As String, textBox As TextBox) As Control
        Dim section As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .Margin = New Padding(0, 0, 10, 8),
            .BackColor = Color.White
        }
        section.RowStyles.Add(New RowStyle(SizeType.Absolute, 30.0F))
        section.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim title As New Label() With {
            .Text = titleText,
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(10, 0, 10, 0),
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .ForeColor = Color.FromArgb(10, 40, 75),
            .BackColor = Color.FromArgb(235, 242, 250)
        }
        section.Controls.Add(title, 0, 0)

        textBox.Dock = DockStyle.Fill
        textBox.Multiline = True
        textBox.ScrollBars = ScrollBars.Vertical
        textBox.Margin = New Padding(0)
        textBox.BorderStyle = BorderStyle.FixedSingle
        textBox.Font = New Font("Segoe UI", 9.0F, FontStyle.Regular)
        section.Controls.Add(textBox, 0, 1)

        Return section
    End Function

    Private Function MakeFooterButton(text As String, backColor As Color, foreColor As Color, width As Integer) As Button
        Dim button As New Button() With {
            .Text = text,
            .Width = width,
            .Height = 34,
            .Margin = New Padding(8, 2, 0, 2),
            .BackColor = backColor,
            .ForeColor = foreColor,
            .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        }
        button.FlatAppearance.BorderColor = If(foreColor = Color.DarkRed, Color.Firebrick, Color.FromArgb(185, 198, 214))
        Return button
    End Function

    Private Sub LoadProducts()
        allProducts = DataService.GetProducts(True)
        ApplyProductFilter()
    End Sub

    Private Sub ApplyProductFilter()
        Dim previousKey = ""
        Dim current = SelectedProduct()
        If current IsNot Nothing Then previousKey = ProductKey(current)

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
        cboProduct.SelectedIndex = -1

        If previousKey <> "" Then
            Dim restoreIndex = filtered.FindIndex(Function(p) ProductKey(p) = previousKey)
            If restoreIndex >= 0 Then cboProduct.SelectedIndex = restoreIndex
        End If
    End Sub

    Private Function ProductKey(product As ProductInfo) As String
        If product Is Nothing Then Return ""
        Return product.TrCode & "|" & product.DrawingRev & "|" & product.DrawingFile
    End Function

    Private Function SelectedProduct() As ProductInfo
        Return TryCast(cboProduct.SelectedItem, ProductInfo)
    End Function

    Private Sub ProductChanged(sender As Object, e As EventArgs)
        FillMoldCodeComboFromProduct(SelectedProduct())
    End Sub

    Private Sub FillMoldCodeComboFromProduct(product As ProductInfo)
        Dim currentText = cboMoldCode.Text.Trim()
        Dim moldCodes = ParseMoldCodes(If(product Is Nothing, "", product.MoldCode))

        cboMoldCode.Items.Clear()
        For Each moldCode In moldCodes
            cboMoldCode.Items.Add(moldCode)
        Next

        If currentText <> "" Then
            cboMoldCode.Text = currentText
        ElseIf moldCodes.Count > 0 Then
            cboMoldCode.SelectedIndex = 0
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

    Private Sub FillFromRow()
        txtTicketId.Text = If(isNew, "(kaydedince oluşacak)", DataService.GetValue(originalRow, "MoldTicketId"))
        txtStatus.Text = If(isNew, "OPEN", DataService.GetValue(originalRow, "Status"))
        txtCreatedAt.Text = DataService.GetValue(originalRow, "CreatedAt")
        txtCreatedBy.Text = DataService.GetValue(originalRow, "CreatedBy")
        txtClosedAt.Text = DataService.GetValue(originalRow, "ClosedAt")
        txtClosedBy.Text = DataService.GetValue(originalRow, "ClosedBy")

        If Not isNew Then SelectProductFromRow()

        cboMoldCode.Text = DataService.GetValue(originalRow, "MoldCode")
        If cboSeverity.Items.Contains(DataService.GetValue(originalRow, "Severity")) Then cboSeverity.Text = DataService.GetValue(originalRow, "Severity")
        If cboProblemType.Items.Contains(DataService.GetValue(originalRow, "ProblemType")) Then cboProblemType.Text = DataService.GetValue(originalRow, "ProblemType")
        txtProblem.Text = DataService.GetValue(originalRow, "ProblemDescription")
        txtAction.Text = DataService.GetValue(originalRow, "ActionPlan")
        txtCloseNote.Text = DataService.GetValue(originalRow, "CloseNote")
        UpdateRelatedProductInfo()
    End Sub

    Private Sub UpdateRelatedProductInfo()
        Dim moldText = cboMoldCode.Text.Trim()
        If moldText = "" Then
            lblRelatedProducts.Text = "Kalıp kodu seçildiğinde aynı kalıba bağlı TR kodları burada gösterilir."
            Return
        End If

        Dim related = allProducts.
            Where(Function(product) DataService.MoldCodeMatches(product.MoldCode, moldText)).
            Select(Function(product) (product.TrCode & " | " & product.DrawingRev & " | " & product.ProductName).Trim(" "c, "|"c)).
            Where(Function(text) text.Trim() <> "").
            Distinct(StringComparer.OrdinalIgnoreCase).
            OrderBy(Function(text) text).
            ToList()

        If related.Count = 0 Then
            lblRelatedProducts.Text = "Bu kalıp koduyla eşleşen aktif TR kaydı bulunamadı. Ticket kalıp kodu üzerinden takip edilir."
            Return
        End If

        Dim preview = String.Join("   •   ", related.Take(5))
        If related.Count > 5 Then preview &= $"   •   +{related.Count - 5} TR"
        lblRelatedProducts.Text = $"Aynı kalıba bağlı TR'ler: {preview}. Uyarılar kalıp kodu üzerinden çalışır."
    End Sub

    Private Sub SelectProductFromRow()
        Dim tr = DataService.GetValue(originalRow, "TrCode")
        Dim rev = DataService.GetValue(originalRow, "DrawingRev")
        If tr.Trim() = "" Then Return

        Dim index = allProducts.FindIndex(Function(p) String.Equals(p.TrCode, tr, StringComparison.OrdinalIgnoreCase) AndAlso
                                                      (rev.Trim() = "" OrElse String.Equals(p.DrawingRev, rev, StringComparison.OrdinalIgnoreCase)))
        If index < 0 Then Return

        Dim selected = allProducts(index)
        Dim filtered = TryCast(cboProduct.DataSource, List(Of ProductInfo))
        If filtered IsNot Nothing Then
            Dim filteredIndex = filtered.FindIndex(Function(p) ProductKey(p) = ProductKey(selected))
            If filteredIndex >= 0 Then cboProduct.SelectedIndex = filteredIndex
        End If
    End Sub

    Private Sub ApplyPermissionState()
        Dim canEdit = If(isNew, AppState.CanModifyMoldTickets, AppState.IsAdmin)
        lblMode.Text = If(isNew,
                          "Yeni kayıt",
                          If(AppState.IsAdmin, "Admin düzenleme yetkisi aktif", "Salt okunur"))

        For Each c In New Control() {txtProductFilter, cboProduct, cboMoldCode, cboSeverity, cboProblemType, txtProblem, txtAction, txtCloseNote}
            c.Enabled = canEdit
        Next
        txtProblem.ReadOnly = Not canEdit
        txtAction.ReadOnly = Not canEdit
        txtCloseNote.ReadOnly = Not canEdit

        btnSave.Visible = canEdit
        btnCloseTicket.Visible = Not isNew AndAlso AppState.CanModifyMoldTickets AndAlso
                                 String.Equals(txtStatus.Text, "OPEN", StringComparison.OrdinalIgnoreCase)
        btnDelete.Visible = Not isNew AndAlso AppState.CanDeleteMoldTickets
    End Sub

    Private Sub Save_Click(sender As Object, e As EventArgs)
        Try
            If Not ValidateRequiredFields() Then Return

            If isNew Then
                CreateTicket()
            Else
                UpdateTicket()
            End If
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Kalıp ticket kaydedilemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Function ValidateRequiredFields() As Boolean
        If cboMoldCode.Text.Trim() = "" Then
            MessageBox.Show("Kalıp Kodu zorunludur.", "Eksik bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            cboMoldCode.Focus()
            Return False
        End If

        If txtProblem.Text.Trim() = "" Then
            MessageBox.Show("Sorun açıklaması zorunludur.", "Eksik bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            txtProblem.Focus()
            Return False
        End If

        Return True
    End Function

    Private Sub CreateTicket()
        AuthorizationService.Require(AppState.CanModifyMoldTickets, "Kalıp Ticketı Oluşturma")

        Dim product = SelectedProduct()
        Dim ticketId = "KLP-" & DateTime.Now.ToString("yyyyMMdd-HHmmss") & "-" &
                       Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant()

        Dim row As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        For Each header In DataService.MoldTicketHeaders
            row(header) = ""
        Next

        row("MoldTicketId") = ticketId
        row("Status") = "OPEN"
        row("CreatedAt") = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        row("CreatedBy") = AppState.CurrentUserName
        row("ComputerName") = Environment.MachineName
        row("MoldCode") = cboMoldCode.Text.Trim()
        row("TrCode") = If(product Is Nothing, "", product.TrCode)
        row("DrawingRev") = If(product Is Nothing, "", product.DrawingRev)
        row("ProductName") = If(product Is Nothing, "", product.ProductName)
        row("Severity") = cboSeverity.Text
        row("ProblemType") = cboProblemType.Text
        row("ProblemDescription") = txtProblem.Text.Trim()
        row("ActionPlan") = txtAction.Text.Trim()

        DataService.AppendMoldTicket(row)
        AuditService.Log("MOLD_TICKET_CREATE", row("TrCode"), row("DrawingRev"), $"MoldTicketId={ticketId}; Mold={row("MoldCode")}; Severity={row("Severity")}")

        MessageBox.Show("Kalıp ticket açıldı." & Environment.NewLine & "Ticket No: " & ticketId,
                        "Kalıp ticket açıldı", MessageBoxButtons.OK, MessageBoxIcon.Information)
        DialogResult = DialogResult.OK
        Close()
    End Sub

    Private Sub UpdateTicket()
        AuthorizationService.Require(AppState.IsAdmin, "Kalıp Ticketı Düzenleme")

        Dim ticketId = DataService.GetValue(originalRow, "MoldTicketId")
        Dim product = SelectedProduct()
        Dim updates As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
            {"MoldCode", cboMoldCode.Text.Trim()},
            {"TrCode", If(product Is Nothing, DataService.GetValue(originalRow, "TrCode"), product.TrCode)},
            {"DrawingRev", If(product Is Nothing, DataService.GetValue(originalRow, "DrawingRev"), product.DrawingRev)},
            {"ProductName", If(product Is Nothing, DataService.GetValue(originalRow, "ProductName"), product.ProductName)},
            {"Severity", cboSeverity.Text},
            {"ProblemType", cboProblemType.Text},
            {"ProblemDescription", txtProblem.Text.Trim()},
            {"ActionPlan", txtAction.Text.Trim()},
            {"CloseNote", txtCloseNote.Text.Trim()}
        }

        DataService.UpdateMoldTicket(ticketId, updates)
        MessageBox.Show("Kalıp ticket detayı güncellendi.", "Güncelleme tamamlandı", MessageBoxButtons.OK, MessageBoxIcon.Information)
        DialogResult = DialogResult.OK
        Close()
    End Sub

    Private Sub CloseTicket_Click(sender As Object, e As EventArgs)
        Try
            AuthorizationService.Require(AppState.CanModifyMoldTickets, "Kalıp Ticketını Kapatma")

            Dim ticketId = DataService.GetValue(originalRow, "MoldTicketId")
            If ticketId.Trim() = "" Then Return

            Dim note = InputBox("Sorun giderildiyse kapanış notu giriniz:", "Kalıp Ticket Kapat", If(txtCloseNote.Text.Trim() = "", "Sorun giderildi.", txtCloseNote.Text.Trim()))
            If note Is Nothing Then note = ""

            DataService.CloseMoldTicket(ticketId, AppState.CurrentUserName, note)
            AuditService.Log("MOLD_TICKET_CLOSE", DataService.GetValue(originalRow, "TrCode"), DataService.GetValue(originalRow, "DrawingRev"), "MoldTicketId=" & ticketId)
            DialogResult = DialogResult.OK
            Close()
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Kalıp ticket kapatılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub DeleteTicket_Click(sender As Object, e As EventArgs)
        Try
            AuthorizationService.Require(AppState.CanDeleteMoldTickets, "Kalıp Ticketı Silme")

            Dim ticketId = DataService.GetValue(originalRow, "MoldTicketId")
            If ticketId.Trim() = "" Then Return

            If MessageBox.Show("Bu kalıp ticketı silinecek." & Environment.NewLine & Environment.NewLine &
                               "Ticket No: " & ticketId & Environment.NewLine &
                               "Bu işlem geri alınamaz. Devam edilsin mi?",
                               "Kalıp ticketı silinsin mi?",
                               MessageBoxButtons.YesNo,
                               MessageBoxIcon.Warning,
                               MessageBoxDefaultButton.Button2) <> DialogResult.Yes Then
                Return
            End If

            DataService.DeleteMoldTicket(ticketId)
            DialogResult = DialogResult.OK
            Close()
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Kalıp ticket silinemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub
End Class
