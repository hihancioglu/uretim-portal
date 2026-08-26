Imports System.Drawing
Imports System.Collections.Generic
Imports System.Linq
Imports System.Net
Imports System.Text
Imports System.Windows.Forms

Public Class FrmIncompleteProductInfo
    Inherits Form

    Private ReadOnly allRows As List(Of ProductInfo)
    Private ReadOnly activeControlPointProductKeys As HashSet(Of String)
    Private ReadOnly grid As New DataGridView()
    Private ReadOnly txtFilter As New TextBox()
    Private ReadOnly lblCount As New Label()
    Private currentRows As New List(Of ProductInfo)()

    Public ReadOnly Property SelectedTrCode As String
    Public ReadOnly Property SelectedDrawingRev As String

    Public Sub New(products As IEnumerable(Of ProductInfo),
                   Optional productKeysWithActiveControlPoints As IEnumerable(Of String) = Nothing)
        AppIconService.Apply(Me)
        If productKeysWithActiveControlPoints Is Nothing Then
            activeControlPointProductKeys = DataService.GetActiveControlPointProductKeys()
        Else
            activeControlPointProductKeys = New HashSet(Of String)(productKeysWithActiveControlPoints, StringComparer.OrdinalIgnoreCase)
        End If
        allRows = If(products, Enumerable.Empty(Of ProductInfo)()).
            Where(Function(p) p IsNot Nothing AndAlso (p.HasIncompleteMetadata OrElse HasNoActiveControlPoints(p))).
            ToList()

        Text = "Eksik Ürün Bilgileri"
        StartPosition = FormStartPosition.CenterParent
        Size = New Size(1200, 620)
        MinimumSize = New Size(850, 420)
        BackColor = Color.White

        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .BackColor = Color.White
        }
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 52.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 48.0F))
        Controls.Add(layout)

        Dim filterPanel As New Panel() With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(12, 10, 12, 6),
            .BackColor = Color.WhiteSmoke
        }
        lblCount.SetBounds(12, 14, 340, 24)
        lblCount.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)

        Dim lblFilter As New Label() With {
            .Text = "Filtre",
            .Left = 370,
            .Top = 16,
            .Width = 45,
            .Height = 22
        }
        txtFilter.SetBounds(420, 11, 350, 27)
        txtFilter.PlaceholderText = "TR / revizyon / ürün / eksik alan / ölçü durumu"
        AddHandler txtFilter.TextChanged, Sub() LoadGrid()
        filterPanel.Controls.AddRange({lblCount, lblFilter, txtFilter})
        layout.Controls.Add(filterPanel, 0, 0)

        ConfigureGrid()
        layout.Controls.Add(grid, 0, 1)

        Dim buttons As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.RightToLeft,
            .WrapContents = False,
            .Padding = New Padding(8)
        }
        Dim btnClose As New Button() With {
            .Text = "Kapat",
            .Width = 100,
            .Height = 30,
            .DialogResult = DialogResult.Cancel
        }
        Dim btnSelect As New Button() With {
            .Text = "Ürünü Aç",
            .Width = 120,
            .Height = 30
        }
        Dim btnEmail As New Button() With {
            .Text = "E-posta Hazırla",
            .Width = 145,
            .Height = 30,
            .BackColor = Color.FromArgb(15, 123, 63),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .Cursor = Cursors.Hand,
            .UseVisualStyleBackColor = False
        }
        btnEmail.FlatAppearance.BorderColor = Color.FromArgb(15, 123, 63)
        AddHandler btnSelect.Click, AddressOf SelectCurrentProduct
        AddHandler btnEmail.Click, AddressOf EmailReport_Click
        buttons.Controls.AddRange({btnClose, btnSelect, btnEmail})
        layout.Controls.Add(buttons, 0, 2)

        CancelButton = btnClose
        LoadGrid()
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
        grid.GridColor = Color.Gainsboro
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 235, 235)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 120, 215)
        grid.DefaultCellStyle.SelectionForeColor = Color.White
        grid.RowTemplate.Height = 29

        grid.Columns.Add(MakeColumn("TrCode", "TR Kodu", 90, 9))
        grid.Columns.Add(MakeColumn("DrawingRev", "Revizyon", 75, 8))
        grid.Columns.Add(MakeColumn("ProductName", "Ürün Adı", 170, 17))
        grid.Columns.Add(MakeColumn("PlasticCode", "Plastik Kodu", 105, 11))
        grid.Columns.Add(MakeColumn("Material", "Malzeme", 120, 12))
        grid.Columns.Add(MakeColumn("ColorName", "Renk", 90, 9))
        grid.Columns.Add(MakeColumn("MoldCavityCount", "Kalıp Göz Adedi", 105, 10))
        grid.Columns.Add(MakeColumn("MoldCode", "Kalıp Kodu", 105, 10))
        grid.Columns.Add(MakeColumn("MissingFields", "Eksik / Yapılacak İşlem", 240, 24))

        AddHandler grid.CellDoubleClick,
            Sub(sender, e)
                If e.RowIndex >= 0 Then SelectCurrentProduct(sender, EventArgs.Empty)
            End Sub
    End Sub

    Private Shared Function MakeColumn(name As String, header As String, width As Integer, fillWeight As Single) As DataGridViewTextBoxColumn
        Return New DataGridViewTextBoxColumn() With {
            .Name = name,
            .HeaderText = header,
            .Width = width,
            .MinimumWidth = 65,
            .FillWeight = fillWeight,
            .SortMode = DataGridViewColumnSortMode.NotSortable
        }
    End Function

    Private Sub LoadGrid()
        Dim filterText = txtFilter.Text.Trim()
        Dim rows = allRows.AsEnumerable()

        If filterText <> "" Then
            Dim tokens = filterText.Split(New Char() {" "c, ";"c, ","c, "|"c}, StringSplitOptions.RemoveEmptyEntries)
            rows = rows.Where(
                Function(p)
                    Dim haystack =
                        (p.TrCode & " " & p.DrawingRev & " " & p.ProductName & " " &
                         p.PlasticCode & " " & p.Material & " " & p.ColorName & " " &
                         p.MoldCavityCount & " " & p.MoldCode & " " &
                         String.Join(" ", GetIssueFields(p))).ToUpperInvariant()
                    Return tokens.All(Function(token) haystack.Contains(token.ToUpperInvariant()))
                End Function)
        End If

        Dim visibleRows = rows.ToList()
        currentRows = visibleRows
        grid.Rows.Clear()

        For Each product In visibleRows
            Dim missing = GetIssueFields(product)
            Dim rowIndex = grid.Rows.Add(
                product.TrCode,
                product.DrawingRev,
                product.ProductName,
                product.PlasticCode,
                product.Material,
                product.ColorName,
                product.MoldCavityCount,
                product.MoldCode,
                String.Join(", ", missing))
            Dim row = grid.Rows(rowIndex)
            row.Tag = product

            MarkMissingCell(row, "ProductName", product.ProductName)
            MarkMissingCell(row, "PlasticCode", product.PlasticCode)
            MarkMissingCell(row, "Material", product.Material)
            MarkMissingCell(row, "ColorName", product.ColorName)
            MarkMissingCell(row, "MoldCavityCount", product.MoldCavityCount)
            MarkMissingCell(row, "MoldCode", product.MoldCode)
            row.Cells("MissingFields").Style.BackColor = Color.MistyRose
            row.Cells("MissingFields").Style.ForeColor = Color.DarkRed
        Next

        lblCount.Text = $"Eksik bilgi / kontrol ölçüsü: {visibleRows.Count} / {allRows.Count} kayıt"
    End Sub

    Private Sub EmailReport_Click(sender As Object, e As EventArgs)
        Try
            AuthorizationService.Require(AppState.CanViewTechnicalDrawingAdmin, "Eksik Ürün Bilgileri E-posta Raporu")
            If currentRows.Count = 0 Then
                MessageBox.Show(
                    "E-posta raporuna eklenecek kayıt bulunamadı.",
                    "E-posta hazırlanmadı",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information)
                Return
            End If

            Dim answer = MessageBox.Show(
                "Ekranda filtrelenmiş " & currentRows.Count.ToString() & " eksik ürün kaydı Outlook e-posta taslağına aktarılacak." &
                Environment.NewLine & Environment.NewLine &
                "E-posta otomatik gönderilmez; açılan taslağı düzenleyip siz gönderebilirsiniz." &
                Environment.NewLine & Environment.NewLine &
                "Devam edilsin mi?",
                "E-posta raporu hazırla",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1)
            If answer <> DialogResult.Yes Then Return

            Dim subject = "Eksik Ürün Bilgileri Raporu - " & DateTime.Now.ToString("dd.MM.yyyy")
            Dim htmlBody = BuildEmailReportHtml(currentRows)
            If Not OutlookEmailDraftService.TryOpenEditableDraft(subject, htmlBody) Then
                MessageBox.Show(
                    "Outlook düzenlenebilir e-posta penceresi açılamadı." & Environment.NewLine &
                    "Lütfen Outlook'un bu bilgisayarda kurulu ve kullanılabilir olduğunu kontrol edin.",
                    "Outlook açılamadı",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning)
                Return
            End If

            AuditService.Log(
                "INCOMPLETE_PRODUCT_INFO_EMAIL_REPORT",
                "",
                "",
                "Kayıt sayısı=" & currentRows.Count.ToString() & "; Filtre=" & txtFilter.Text.Trim())
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "E-posta raporu hazırlanamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Function BuildEmailReportHtml(rows As List(Of ProductInfo)) As String
        Dim missingNameCount = rows.Where(Function(product) String.IsNullOrWhiteSpace(product.ProductName)).Count()
        Dim missingControlPointCount = rows.Where(Function(product) HasNoActiveControlPoints(product)).Count()
        Dim filterDescription = If(txtFilter.Text.Trim() = "", "Yok", txtFilter.Text.Trim())

        Dim html As New StringBuilder()
        html.AppendLine("<!DOCTYPE html><html><head><meta charset=""utf-8""></head>")
        html.AppendLine("<body style=""font-family:Segoe UI,Arial,sans-serif;font-size:13px;color:#1f2937;background:#ffffff;"">")
        html.AppendLine("<h2 style=""margin:0 0 6px;color:#1f477e;"">Eksik Ürün Bilgileri Raporu</h2>")
        html.AppendLine("<div style=""margin-bottom:14px;color:#4b5563;"">Hazırlanma: " & EncodeHtml(DateTime.Now.ToString("dd.MM.yyyy HH:mm")) &
                        " &nbsp; | &nbsp; Hazırlayan: " & EncodeHtml(AppState.CurrentUserName & " / " & AppState.NormalizeRole(AppState.CurrentRole)) &
                        " &nbsp; | &nbsp; Filtre: " & EncodeHtml(filterDescription) & "</div>")

        html.AppendLine("<table style=""border-collapse:collapse;margin-bottom:16px;""><tr>")
        AppendSummaryCell(html, "Gösterilen Kayıt", rows.Count.ToString(), "#e7eef8", "#1f477e")
        AppendSummaryCell(html, "Ürün Adı Eksik", missingNameCount.ToString(), "#fee2e2", "#991b1b")
        AppendSummaryCell(html, "Kontrol Ölçüsü Eksik", missingControlPointCount.ToString(), "#fff4b3", "#785700")
        html.AppendLine("</tr></table>")

        html.AppendLine("<table style=""border-collapse:collapse;width:100%;font-size:12px;"">")
        html.AppendLine("<thead><tr style=""background:#dfe9f5;color:#183252;"">")
        For Each header In {"TR KODU", "REVİZYON", "ÜRÜN ADI", "PLASTİK KODU", "MALZEME", "RENK", "KALIP GÖZ ADEDİ", "KALIP KODU", "EKSİK / YAPILACAK İŞLEM"}
            html.AppendLine("<th style=""border:1px solid #aab8ca;padding:7px;text-align:left;"">" & EncodeHtml(header) & "</th>")
        Next
        html.AppendLine("</tr></thead><tbody>")

        Dim rowIndex As Integer = 0
        For Each product In rows
            Dim backColor = If(rowIndex Mod 2 = 0, "#ffffff", "#f8fafc")
            html.AppendLine("<tr style=""background:" & backColor & ";"">")
            AppendReportCell(html, product.TrCode, False)
            AppendReportCell(html, product.DrawingRev, False)
            AppendReportCell(html, product.ProductName, String.IsNullOrWhiteSpace(product.ProductName))
            AppendReportCell(html, product.PlasticCode, String.IsNullOrWhiteSpace(product.PlasticCode))
            AppendReportCell(html, product.Material, String.IsNullOrWhiteSpace(product.Material))
            AppendReportCell(html, product.ColorName, String.IsNullOrWhiteSpace(product.ColorName))
            AppendReportCell(html, product.MoldCavityCount, String.IsNullOrWhiteSpace(product.MoldCavityCount))
            AppendReportCell(html, product.MoldCode, String.IsNullOrWhiteSpace(product.MoldCode))
            AppendReportCell(html, String.Join(", ", GetIssueFields(product)), True)
            html.AppendLine("</tr>")
            rowIndex += 1
        Next

        html.AppendLine("</tbody></table>")
        html.AppendLine("<p style=""margin-top:16px;"">Eksik bilgilerin ve kontrol ölçülerinin tamamlanması rica olunur.</p>")
        html.AppendLine("</body></html>")
        Return html.ToString()
    End Function

    Private Shared Sub AppendSummaryCell(html As StringBuilder, caption As String, value As String, backColor As String, foreColor As String)
        html.AppendLine("<td style=""min-width:120px;border:1px solid #cbd5e1;padding:8px 12px;background:" & backColor & ";color:" & foreColor & ";"">" &
                        "<div style=""font-size:11px;font-weight:600;"">" & EncodeHtml(caption) & "</div>" &
                        "<div style=""font-size:18px;font-weight:700;"">" & EncodeHtml(value) & "</div></td>")
    End Sub

    Private Shared Sub AppendReportCell(html As StringBuilder, value As String, isMissing As Boolean)
        Dim backColor = If(isMissing, "#fee2e2", "transparent")
        Dim foreColor = If(isMissing, "#991b1b", "#1f2937")
        Dim displayValue = If(String.IsNullOrWhiteSpace(value), "-", value.Trim())
        html.AppendLine("<td style=""border:1px solid #cbd5e1;padding:7px;vertical-align:top;background:" & backColor & ";color:" & foreColor & ";"">" &
                        EncodeHtml(displayValue) & "</td>")
    End Sub

    Private Shared Function EncodeHtml(value As String) As String
        Return WebUtility.HtmlEncode(If(value, ""))
    End Function

    Private Function HasNoActiveControlPoints(product As ProductInfo) As Boolean
        If product Is Nothing Then Return False
        Dim key = DataService.GetControlPointProductKey(product.TrCode, product.DrawingRev, product.DrawingScope)
        Return Not activeControlPointProductKeys.Contains(key)
    End Function

    Private Function GetIssueFields(product As ProductInfo) As List(Of String)
        Dim issues = product.GetMissingMetadataFields()
        If HasNoActiveControlPoints(product) Then issues.Add("Kontrol Ölçüsü Tanımlı Değil")
        Return issues
    End Function

    Private Shared Sub MarkMissingCell(row As DataGridViewRow, columnName As String, value As String)
        If Not String.IsNullOrWhiteSpace(value) Then Return
        row.Cells(columnName).Style.BackColor = Color.MistyRose
        row.Cells(columnName).Style.ForeColor = Color.DarkRed
    End Sub

    Private Sub SelectCurrentProduct(sender As Object, e As EventArgs)
        If grid.CurrentRow Is Nothing Then Return
        Dim product = TryCast(grid.CurrentRow.Tag, ProductInfo)
        If product Is Nothing Then Return

        _SelectedTrCode = product.TrCode
        _SelectedDrawingRev = product.DrawingRev
        DialogResult = DialogResult.OK
        Close()
    End Sub
End Class
