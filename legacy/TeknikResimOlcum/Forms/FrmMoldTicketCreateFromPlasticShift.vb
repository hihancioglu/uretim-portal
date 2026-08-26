Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmMoldTicketCreateFromPlasticShift
    Inherits Form

    Private ReadOnly sourceRecordId As String
    Private ReadOnly sourceProductText As String
    Private ReadOnly cboProduct As New ComboBox()
    Private ReadOnly cboMoldCode As New ComboBox()
    Private ReadOnly cboSeverity As New ComboBox()
    Private ReadOnly cboProblemType As New ComboBox()
    Private ReadOnly txtProblem As New TextBox()
    Private ReadOnly txtAction As New TextBox()
    Private ReadOnly lblSource As New Label()
    Private allProducts As New List(Of ProductInfo)()

    Public Property TicketDraft As Dictionary(Of String, String)

    Public Sub New(recordId As String, productText As String, problemText As String, actionText As String)
        AuthorizationService.Require(AppState.CanCreateMoldTicketFromPlasticShift, "Vardiya Kaydından Kalıp Ticketı Oluşturma")
        sourceRecordId = If(recordId, "").Trim()
        sourceProductText = If(productText, "").Trim()

        AppIconService.Apply(Me)
        Text = "Kalıp Ticketı Oluştur"
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        Size = New Size(1100, 680)
        MinimumSize = New Size(760, 540)
        Font = New Font("Segoe UI", 9.0F)
        BackColor = Color.FromArgb(242, 246, 251)

        BuildScreen()
        txtProblem.Text = If(problemText, "").Trim()
        txtAction.Text = If(actionText, "").Trim()
        LoadProducts()
        ResponsiveFormService.Apply(Me)
    End Sub

    Private Sub BuildScreen()
        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .BackColor = BackColor
        }
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 56.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 72.0F))
        Controls.Add(root)

        Dim header As New Label() With {
            .Dock = DockStyle.Fill,
            .Text = "KALIP TADİLATI — KALIP TICKET BİLGİLERİ",
            .Padding = New Padding(20, 0, 0, 0),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Font = New Font("Segoe UI", 13.0F, FontStyle.Bold),
            .ForeColor = Color.White,
            .BackColor = Color.FromArgb(31, 71, 126)
        }
        root.Controls.Add(header, 0, 0)

        Dim body As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 1,
            .Padding = New Padding(14),
            .BackColor = BackColor
        }
        body.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 42.0F))
        body.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 58.0F))
        body.Controls.Add(BuildTicketInformationCard(), 0, 0)
        body.Controls.Add(BuildDescriptionCard(), 1, 0)
        root.Controls.Add(body, 0, 1)

        Dim footer As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 1,
            .Padding = New Padding(16, 11, 14, 11),
            .BackColor = Color.White
        }
        footer.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        footer.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 390.0F))

        Dim note As New Label() With {
            .Dock = DockStyle.Fill,
            .Text = "Ticket, vardiya kaydı kaydedildiğinde oluşturulur. Aynı vardiya kaydı için ikinci ticket açılmaz.",
            .TextAlign = ContentAlignment.MiddleLeft,
            .ForeColor = Color.FromArgb(75, 88, 105),
            .AutoEllipsis = True
        }
        footer.Controls.Add(note, 0, 0)

        Dim buttons As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.RightToLeft,
            .WrapContents = False
        }
        Dim btnCancel As New Button()
        ConfigureButton(btnCancel, "Vazgeç", 105, Color.White, Color.FromArgb(35, 55, 80))
        AddHandler btnCancel.Click, Sub()
                                        DialogResult = DialogResult.Cancel
                                        Close()
                                    End Sub
        Dim btnConfirm As New Button()
        ConfigureButton(btnConfirm, "Ticket Bilgilerini Onayla", 220, Color.FromArgb(22, 128, 70), Color.White)
        AddHandler btnConfirm.Click, AddressOf Confirm_Click
        buttons.Controls.Add(btnCancel)
        buttons.Controls.Add(btnConfirm)
        footer.Controls.Add(buttons, 1, 0)
        root.Controls.Add(footer, 0, 2)
    End Sub

    Private Function BuildTicketInformationCard() As Control
        Dim content As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 10,
            .Padding = New Padding(18, 14, 18, 16),
            .BackColor = Color.White
        }
        For i As Integer = 0 To 4
            content.RowStyles.Add(New RowStyle(SizeType.Absolute, 27.0F))
            content.RowStyles.Add(New RowStyle(SizeType.Absolute, 43.0F))
        Next

        lblSource.Dock = DockStyle.Top
        lblSource.Height = 30
        lblSource.TextAlign = ContentAlignment.MiddleLeft
        lblSource.Padding = New Padding(8, 0, 0, 0)
        lblSource.BackColor = Color.FromArgb(245, 248, 252)
        lblSource.ForeColor = Color.FromArgb(31, 71, 126)
        lblSource.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        lblSource.Text = If(sourceRecordId = "", "Kayıt kaydedilince atanacak", sourceRecordId)

        cboProduct.Dock = DockStyle.Top
        cboProduct.Height = 32
        cboProduct.DropDownStyle = ComboBoxStyle.DropDownList
        AddHandler cboProduct.SelectedIndexChanged, AddressOf ProductChanged

        cboMoldCode.Dock = DockStyle.Top
        cboMoldCode.Height = 32
        cboMoldCode.DropDownStyle = ComboBoxStyle.DropDown
        cboMoldCode.AutoCompleteMode = AutoCompleteMode.SuggestAppend
        cboMoldCode.AutoCompleteSource = AutoCompleteSource.ListItems

        cboSeverity.Dock = DockStyle.Top
        cboSeverity.Height = 32
        cboSeverity.DropDownStyle = ComboBoxStyle.DropDownList
        cboSeverity.Items.AddRange({"KRİTİK", "YÜKSEK", "ORTA", "DÜŞÜK"})
        cboSeverity.SelectedItem = "ORTA"

        cboProblemType.Dock = DockStyle.Top
        cboProblemType.Height = 32
        cboProblemType.DropDownStyle = ComboBoxStyle.DropDownList
        cboProblemType.Items.AddRange({"PARLATMA", "KALIP TADİLATI", "KALIP MEKANİK", "KALIP SOĞUTMA", "ÇAPAK", "ÖLÇÜ KAÇIĞI", "KIRIK / HASAR", "YÜZEY HATASI", "GÖZ PROBLEMİ", "DİĞER"})
        cboProblemType.SelectedItem = "PARLATMA"

        AddLabeledControl(content, 0, "Kaynak Vardiya Kaydı", lblSource)
        AddLabeledControl(content, 2, "Ürün / TR", cboProduct)
        AddLabeledControl(content, 4, "Kalıp Kodu", cboMoldCode)
        AddLabeledControl(content, 6, "Önem", cboSeverity)
        AddLabeledControl(content, 8, "Sorun Tipi", cboProblemType)
        Return WrapCard("Ticket Bilgileri", content, New Padding(0, 0, 8, 0))
    End Function

    Private Function BuildDescriptionCard() As Control
        Dim content As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 4,
            .Padding = New Padding(18, 14, 18, 16),
            .BackColor = Color.White
        }
        content.RowStyles.Add(New RowStyle(SizeType.Absolute, 27.0F))
        content.RowStyles.Add(New RowStyle(SizeType.Percent, 50.0F))
        content.RowStyles.Add(New RowStyle(SizeType.Absolute, 27.0F))
        content.RowStyles.Add(New RowStyle(SizeType.Percent, 50.0F))

        txtProblem.Dock = DockStyle.Fill
        txtProblem.Multiline = True
        txtProblem.ScrollBars = ScrollBars.Vertical
        txtProblem.MaxLength = 4000
        txtProblem.Margin = New Padding(0, 0, 0, 10)

        txtAction.Dock = DockStyle.Fill
        txtAction.Multiline = True
        txtAction.ScrollBars = ScrollBars.Vertical
        txtAction.MaxLength = 4000

        content.Controls.Add(CreateFieldLabel("Sorun Açıklaması"), 0, 0)
        content.Controls.Add(txtProblem, 0, 1)
        content.Controls.Add(CreateFieldLabel("Aksiyon Planı"), 0, 2)
        content.Controls.Add(txtAction, 0, 3)
        Return WrapCard("Sorun ve Aksiyon", content, New Padding(8, 0, 0, 0))
    End Function

    Private Sub LoadProducts()
        allProducts = DataService.GetProducts(False).
            OrderBy(Function(product) DataService.TrCodeNumericSortValue(product.TrCode)).
            ThenBy(Function(product) product.TrCode, StringComparer.OrdinalIgnoreCase).
            ThenBy(Function(product) product.DrawingRev, StringComparer.OrdinalIgnoreCase).
            ToList()
        cboProduct.DataSource = allProducts
        cboProduct.DisplayMember = "DisplayName"

        Dim selectedIndex = allProducts.FindIndex(
            Function(product) String.Equals(BuildShiftProductText(product), sourceProductText, StringComparison.OrdinalIgnoreCase))
        If selectedIndex < 0 Then
            Dim sourceTrCode = FirstProductPart(sourceProductText)
            selectedIndex = allProducts.FindIndex(
                Function(product) String.Equals(product.TrCode.Trim(), sourceTrCode, StringComparison.OrdinalIgnoreCase))
        End If
        cboProduct.SelectedIndex = selectedIndex
        FillMoldCodeCombo(SelectedProduct())
    End Sub

    Private Shared Function BuildShiftProductText(product As ProductInfo) As String
        If product Is Nothing Then Return ""
        Dim parts = New List(Of String) From {product.TrCode, product.ProductName}
        If product.PlasticCode.Trim() <> "" Then parts.Add(product.PlasticCode)
        Return String.Join(" | ", parts.Where(Function(part) Not String.IsNullOrWhiteSpace(part)))
    End Function

    Private Shared Function FirstProductPart(value As String) As String
        If String.IsNullOrWhiteSpace(value) Then Return ""
        Return value.Split("|"c)(0).Trim()
    End Function

    Private Function SelectedProduct() As ProductInfo
        Return TryCast(cboProduct.SelectedItem, ProductInfo)
    End Function

    Private Sub ProductChanged(sender As Object, e As EventArgs)
        FillMoldCodeCombo(SelectedProduct())
    End Sub

    Private Sub FillMoldCodeCombo(product As ProductInfo)
        Dim previous = cboMoldCode.Text.Trim()
        Dim codes = ParseMoldCodes(If(product Is Nothing, "", product.MoldCode))
        cboMoldCode.Items.Clear()
        For Each code In codes
            cboMoldCode.Items.Add(code)
        Next

        If previous <> "" AndAlso codes.Any(Function(code) String.Equals(code, previous, StringComparison.OrdinalIgnoreCase)) Then
            cboMoldCode.Text = previous
        ElseIf codes.Count > 0 Then
            cboMoldCode.SelectedIndex = 0
        Else
            cboMoldCode.Text = previous
        End If
    End Sub

    Private Shared Function ParseMoldCodes(rawText As String) As List(Of String)
        Dim result As New List(Of String)()
        Dim cleaned = If(rawText, "").Replace(vbCr, ";").Replace(vbLf, ";").Replace("/", ";").Replace(",", ";").Replace("|", ";")
        For Each part In cleaned.Split(";"c)
            Dim value = part.Trim()
            If value <> "" AndAlso Not result.Any(Function(item) String.Equals(item, value, StringComparison.OrdinalIgnoreCase)) Then
                result.Add(value)
            End If
        Next
        Return result
    End Function

    Private Sub Confirm_Click(sender As Object, e As EventArgs)
        Dim moldCode = cboMoldCode.Text.Trim()
        If moldCode = "" Then
            MessageBox.Show("Kalıp Kodu zorunludur.", "Eksik bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            cboMoldCode.Focus()
            Return
        End If
        If txtProblem.Text.Trim() = "" Then
            MessageBox.Show("Sorun açıklaması zorunludur.", "Eksik bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            txtProblem.Focus()
            Return
        End If

        Dim product = SelectedProduct()
        Dim fallbackParts = sourceProductText.Split("|"c).Select(Function(part) part.Trim()).ToArray()
        TicketDraft = New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
            {"MoldCode", moldCode},
            {"TrCode", If(product Is Nothing, If(fallbackParts.Length > 0, fallbackParts(0), ""), product.TrCode)},
            {"DrawingRev", If(product Is Nothing, "", product.DrawingRev)},
            {"ProductName", If(product Is Nothing, If(fallbackParts.Length > 1, fallbackParts(1), sourceProductText), product.ProductName)},
            {"Severity", cboSeverity.Text.Trim()},
            {"ProblemType", cboProblemType.Text.Trim()},
            {"ProblemDescription", txtProblem.Text.Trim()},
            {"ActionPlan", txtAction.Text.Trim()}
        }
        DialogResult = DialogResult.OK
        Close()
    End Sub

    Private Shared Sub AddLabeledControl(layout As TableLayoutPanel, labelRow As Integer, caption As String, control As Control)
        layout.Controls.Add(CreateFieldLabel(caption), 0, labelRow)
        control.Margin = New Padding(0, 0, 0, 8)
        layout.Controls.Add(control, 0, labelRow + 1)
    End Sub

    Private Shared Function CreateFieldLabel(caption As String) As Label
        Return New Label() With {
            .Text = caption,
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .ForeColor = Color.FromArgb(40, 58, 82),
            .Margin = New Padding(0)
        }
    End Function

    Private Shared Function WrapCard(titleText As String, content As Control, margin As Padding) As Control
        Dim card As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .BackColor = Color.White,
            .Margin = margin,
            .CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
        }
        card.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        card.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        card.Controls.Add(New Label() With {
            .Dock = DockStyle.Fill,
            .Text = titleText,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(14, 0, 0, 0),
            .Font = New Font("Segoe UI", 10.0F, FontStyle.Bold),
            .ForeColor = Color.FromArgb(31, 71, 126),
            .BackColor = Color.FromArgb(231, 238, 248),
            .Margin = New Padding(0)
        }, 0, 0)
        card.Controls.Add(content, 0, 1)
        Return card
    End Function

    Private Shared Sub ConfigureButton(button As Button, caption As String, width As Integer, backColor As Color, foreColor As Color)
        button.Text = caption
        button.Width = width
        button.Height = 40
        button.Margin = New Padding(8, 0, 0, 0)
        button.FlatStyle = FlatStyle.Flat
        button.FlatAppearance.BorderColor = Color.FromArgb(178, 190, 205)
        button.BackColor = backColor
        button.ForeColor = foreColor
        button.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        button.AutoEllipsis = False
        button.Tag = "RESPONSIVE_NO_AUTO_SCALE"
    End Sub
End Class
