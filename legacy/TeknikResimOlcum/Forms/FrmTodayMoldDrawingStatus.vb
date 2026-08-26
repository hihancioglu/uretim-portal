Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Net
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Windows.Forms

Friend Class TodayMoldDrawingStatusInfo
    Public Property TrCode As String = ""
    Public Property ProductName As String = ""
    Public Property DrawingRev As String = ""
    Public Property DrawingFile As String = ""
    Public Property PlanPositions As String = ""
    Public Property Machines As String = ""
    Public Property MoldCodes As String = ""
    Public Property PlasticCodes As String = ""
    Public Property IssueText As String = ""
    Public Property IsMissing As Boolean
End Class

Public Class FrmTodayMoldDrawingStatus
    Inherits Form

    Private NotInheritable Class PlanAggregate
        Public Property TrCode As String = ""
        Public ReadOnly PlanPositions As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Public ReadOnly Machines As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Public ReadOnly MoldCodes As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Public ReadOnly PlasticCodes As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
    End Class

    Private ReadOnly grid As New DataGridView()
    Private ReadOnly txtFilter As New TextBox()
    Private ReadOnly chkOnlyMissing As New CheckBox()
    Private ReadOnly lblCount As New Label()
    Private ReadOnly lblSource As New Label()
    Private ReadOnly btnOpenProduct As New Button()
    Private allRows As New List(Of TodayMoldDrawingStatusInfo)()
    Private currentRows As New List(Of TodayMoldDrawingStatusInfo)()

    Public ReadOnly Property SelectedTrCode As String
    Public ReadOnly Property SelectedDrawingRev As String

    Public Sub New()
        AuthorizationService.Require(AppState.CanViewTechnicalDrawingAdmin, "Bugün Bağlanacak Kalıp Teknik Resim Kontrolü")
        AppIconService.Apply(Me)

        Text = "Bugün Bağlanacak Kalıplar - Teknik Resim Kontrolü"
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        Size = New Size(1380, 720)
        MinimumSize = New Size(820, 500)
        Font = New Font("Segoe UI", 9.0F)
        BackColor = Color.FromArgb(243, 247, 252)

        BuildScreen()
        LoadRows()
    End Sub

    Public Shared Function CountMissingDrawings() As Integer
        Return BuildStatusRows(DataService.GetMoldConnectionPlanRows(), DataService.GetProducts(False)).
            Where(Function(row) row.IsMissing).
            Count()
    End Function

    Private Sub BuildScreen()
        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 4,
            .Padding = New Padding(10),
            .BackColor = BackColor
        }
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 44.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58.0F))
        Controls.Add(root)

        Dim header As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 1,
            .BackColor = Color.FromArgb(31, 71, 126),
            .Padding = New Padding(16, 5, 16, 5),
            .Margin = New Padding(0, 0, 0, 6)
        }
        header.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        header.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 520.0F))
        header.Controls.Add(New Label() With {
            .Text = "BUGÜN BAĞLANACAK KALIPLAR - TEKNİK RESİM DURUMU",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 12.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft,
            .AutoEllipsis = True
        }, 0, 0)
        lblSource.Dock = DockStyle.Fill
        lblSource.ForeColor = Color.FromArgb(222, 233, 247)
        lblSource.TextAlign = ContentAlignment.MiddleRight
        lblSource.AutoEllipsis = True
        header.Controls.Add(lblSource, 1, 0)
        root.Controls.Add(header, 0, 0)

        Dim filterPanel As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 5,
            .RowCount = 1,
            .Padding = New Padding(10, 5, 10, 5),
            .BackColor = Color.White,
            .Margin = New Padding(0, 0, 0, 6)
        }
        filterPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 55.0F))
        filterPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        filterPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 165.0F))
        filterPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 390.0F))
        filterPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 1.0F))
        filterPanel.Controls.Add(New Label() With {
            .Text = "Arama",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        }, 0, 0)
        txtFilter.Dock = DockStyle.Fill
        txtFilter.Margin = New Padding(3, 1, 12, 1)
        txtFilter.PlaceholderText = "TR / ürün / makine / kalıp / P kodu / durum"
        AddHandler txtFilter.TextChanged, Sub() ApplyFilter()
        filterPanel.Controls.Add(txtFilter, 1, 0)

        chkOnlyMissing.Text = "Yalnızca eksik resimler"
        chkOnlyMissing.Dock = DockStyle.Fill
        chkOnlyMissing.Margin = New Padding(4, 0, 8, 0)
        AddHandler chkOnlyMissing.CheckedChanged, Sub() ApplyFilter()
        filterPanel.Controls.Add(chkOnlyMissing, 2, 0)

        lblCount.Dock = DockStyle.Fill
        lblCount.TextAlign = ContentAlignment.MiddleRight
        lblCount.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        lblCount.ForeColor = Color.FromArgb(45, 65, 92)
        lblCount.AutoEllipsis = True
        filterPanel.Controls.Add(lblCount, 3, 0)
        root.Controls.Add(filterPanel, 0, 1)

        ConfigureGrid()
        root.Controls.Add(grid, 0, 2)

        Dim footer As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.RightToLeft,
            .WrapContents = False,
            .Padding = New Padding(8, 10, 0, 6),
            .BackColor = Color.White,
            .Margin = New Padding(0, 6, 0, 0)
        }
        Dim btnClose As New Button() With {
            .Text = "Kapat",
            .Width = 105,
            .Height = 34,
            .DialogResult = DialogResult.Cancel,
            .Margin = New Padding(8, 0, 0, 0)
        }
        btnOpenProduct.Text = If(AppState.CanOpenTechnicalDrawingAdmin, "Ürün Kaydını Aç", "Ürün Kaydını Göster")
        btnOpenProduct.Width = 150
        btnOpenProduct.Height = 34
        btnOpenProduct.BackColor = Color.FromArgb(31, 71, 126)
        btnOpenProduct.ForeColor = Color.White
        btnOpenProduct.FlatStyle = FlatStyle.Flat
        btnOpenProduct.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        btnOpenProduct.Margin = New Padding(8, 0, 0, 0)
        AddHandler btnOpenProduct.Click, AddressOf SelectCurrentProduct

        Dim btnEmail As New Button() With {
            .Text = "E-posta Hazırla",
            .Width = 145,
            .Height = 34,
            .BackColor = Color.FromArgb(15, 123, 63),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .Cursor = Cursors.Hand,
            .UseVisualStyleBackColor = False,
            .Margin = New Padding(8, 0, 0, 0)
        }
        btnEmail.FlatAppearance.BorderColor = Color.FromArgb(15, 123, 63)
        AddHandler btnEmail.Click, AddressOf EmailReport_Click
        footer.Controls.Add(btnClose)
        footer.Controls.Add(btnOpenProduct)
        footer.Controls.Add(btnEmail)
        root.Controls.Add(footer, 0, 3)
        CancelButton = btnClose
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
        grid.GridColor = Color.FromArgb(222, 228, 236)
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(225, 235, 247)
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(31, 50, 75)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter
        grid.ColumnHeadersHeight = 42
        grid.DefaultCellStyle.Padding = New Padding(5, 3, 5, 3)
        grid.RowTemplate.Height = 30

        grid.Columns.Add(MakeColumn("Status", "DURUM", 8.0F, 90))
        grid.Columns.Add(MakeColumn("TrCode", "TR KODU", 10.0F, 105))
        grid.Columns.Add(MakeColumn("ProductName", "ÜRÜN ADI", 15.0F, 145))
        grid.Columns.Add(MakeColumn("DrawingRev", "REVİZYON", 8.0F, 80))
        grid.Columns.Add(MakeColumn("DrawingFile", "TEKNİK RESİM DOSYASI", 15.0F, 155))
        grid.Columns.Add(MakeColumn("PlanPositions", "BAĞLANACAK SIRA", 11.0F, 120))
        grid.Columns.Add(MakeColumn("Machines", "MAKİNE", 12.0F, 125))
        grid.Columns.Add(MakeColumn("MoldCodes", "KALIP", 10.0F, 105))
        grid.Columns.Add(MakeColumn("PlasticCodes", "P KODU", 9.0F, 90))
        grid.Columns.Add(MakeColumn("IssueText", "AÇIKLAMA", 18.0F, 170))

        AddHandler grid.SelectionChanged, Sub() UpdateButtonState()
        AddHandler grid.CellDoubleClick,
            Sub(sender, e)
                If e.RowIndex >= 0 Then SelectCurrentProduct(sender, EventArgs.Empty)
            End Sub
    End Sub

    Private Shared Function MakeColumn(name As String, header As String, fillWeight As Single, minimumWidth As Integer) As DataGridViewTextBoxColumn
        Return New DataGridViewTextBoxColumn() With {
            .Name = name,
            .HeaderText = header,
            .FillWeight = fillWeight,
            .MinimumWidth = minimumWidth,
            .SortMode = DataGridViewColumnSortMode.Automatic
        }
    End Function

    Private Sub LoadRows()
        Try
            Dim planRows = DataService.GetMoldConnectionPlanRows()
            allRows = BuildStatusRows(planRows, DataService.GetProducts(False))
            Dim newest = planRows.OrderByDescending(Function(row) DataService.GetValue(row, "ImportedAt")).FirstOrDefault()
            If newest Is Nothing Then
                lblSource.Text = "Bağlanacak Kalıp Listesi bulunamadı."
            Else
                lblSource.Text = "Kaynak: " & DisplayValue(DataService.GetValue(newest, "SourceFile")) &
                                 " | Son aktarım: " & DisplayValue(DataService.GetValue(newest, "ImportedAt"))
            End If

            chkOnlyMissing.Checked = allRows.Any(Function(row) row.IsMissing)
            ApplyFilter()
            AuditService.Log("TODAY_MOLD_DRAWING_STATUS_VIEW", "", "", "Plan TR=" & allRows.Count.ToString() & "; Eksik=" & allRows.Where(Function(row) row.IsMissing).Count().ToString())
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Teknik resim durumu okunamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ApplyFilter()
        Dim rows = allRows.AsEnumerable()
        If chkOnlyMissing.Checked Then rows = rows.Where(Function(row) row.IsMissing)

        Dim filterText = txtFilter.Text.Trim()
        If filterText <> "" Then
            Dim tokens = filterText.Split(New Char() {" "c, ";"c, ","c, "|"c}, StringSplitOptions.RemoveEmptyEntries)
            rows = rows.Where(
                Function(row)
                    Dim haystack = String.Join(" ", {
                        row.TrCode, row.ProductName, row.DrawingRev, row.DrawingFile,
                        row.PlanPositions, row.Machines, row.MoldCodes, row.PlasticCodes,
                        row.IssueText, If(row.IsMissing, "EKSİK", "VAR")})
                    Return tokens.All(Function(token) haystack.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                End Function)
        End If

        Dim visibleRows = rows.
            OrderByDescending(Function(row) row.IsMissing).
            ThenBy(Function(row) row.TrCode, StringComparer.OrdinalIgnoreCase).
            ToList()
        currentRows = visibleRows

        grid.Rows.Clear()
        For Each item In visibleRows
            Dim rowIndex = grid.Rows.Add(
                If(item.IsMissing, "EKSİK", "VAR"),
                DisplayValue(item.TrCode),
                DisplayValue(item.ProductName),
                DisplayValue(item.DrawingRev),
                DisplayValue(item.DrawingFile),
                DisplayValue(item.PlanPositions),
                DisplayValue(item.Machines),
                DisplayValue(item.MoldCodes),
                DisplayValue(item.PlasticCodes),
                item.IssueText)
            Dim gridRow = grid.Rows(rowIndex)
            gridRow.Tag = item
            If item.IsMissing Then
                gridRow.DefaultCellStyle.BackColor = Color.FromArgb(255, 238, 238)
                gridRow.DefaultCellStyle.ForeColor = Color.FromArgb(145, 25, 25)
                gridRow.Cells("Status").Style.Font = New Font(grid.Font, FontStyle.Bold)
            Else
                gridRow.DefaultCellStyle.BackColor = If(rowIndex Mod 2 = 0, Color.White, Color.FromArgb(247, 250, 253))
                gridRow.Cells("Status").Style.BackColor = Color.FromArgb(220, 245, 226)
                gridRow.Cells("Status").Style.ForeColor = Color.DarkGreen
                gridRow.Cells("Status").Style.Font = New Font(grid.Font, FontStyle.Bold)
            End If
        Next

        Dim missingCount = allRows.Where(Function(row) row.IsMissing).Count()
        lblCount.Text = "Gösterilen: " & visibleRows.Count.ToString() & " / " & allRows.Count.ToString() &
                        " | Resim var: " & (allRows.Count - missingCount).ToString() &
                        " | Eksik: " & missingCount.ToString()
        UpdateButtonState()
    End Sub

    Private Sub EmailReport_Click(sender As Object, e As EventArgs)
        Try
            AuthorizationService.Require(AppState.CanViewTechnicalDrawingAdmin, "Bağlanacak Kalıplar Teknik Resim E-posta Raporu")
            If currentRows.Count = 0 Then
                MessageBox.Show(
                    "E-posta raporuna eklenecek kayıt bulunamadı.",
                    "E-posta hazırlanmadı",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information)
                Return
            End If

            Dim answer = MessageBox.Show(
                "Ekranda filtrelenmiş " & currentRows.Count.ToString() & " teknik resim durum kaydı Outlook e-posta taslağına aktarılacak." &
                Environment.NewLine & Environment.NewLine &
                "E-posta otomatik gönderilmez; açılan taslağı düzenleyip siz gönderebilirsiniz." &
                Environment.NewLine & Environment.NewLine &
                "Devam edilsin mi?",
                "E-posta raporu hazırla",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1)
            If answer <> DialogResult.Yes Then Return

            Dim subject = "Bağlanacak Kalıplar Teknik Resim Durumu - " & DateTime.Now.ToString("dd.MM.yyyy")
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
                "TODAY_MOLD_DRAWING_STATUS_EMAIL_REPORT",
                "",
                "",
                "Kayıt sayısı=" & currentRows.Count.ToString() &
                "; Yalnızca eksik=" & If(chkOnlyMissing.Checked, "EVET", "HAYIR") &
                "; Arama=" & txtFilter.Text.Trim())
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "E-posta raporu hazırlanamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Function BuildEmailReportHtml(rows As List(Of TodayMoldDrawingStatusInfo)) As String
        Dim missingCount = rows.Where(Function(row) row.IsMissing).Count()
        Dim availableCount = rows.Count - missingCount
        Dim filterDescription = If(txtFilter.Text.Trim() = "", "Yok", txtFilter.Text.Trim())
        Dim statusDescription = If(chkOnlyMissing.Checked, "Yalnızca eksik resimler", "Tüm kayıtlar")

        Dim html As New StringBuilder()
        html.AppendLine("<!DOCTYPE html><html><head><meta charset=""utf-8""></head>")
        html.AppendLine("<body style=""font-family:Segoe UI,Arial,sans-serif;font-size:13px;color:#1f2937;background:#ffffff;"">")
        html.AppendLine("<h2 style=""margin:0 0 6px;color:#1f477e;"">Bağlanacak Kalıplar - Teknik Resim Durumu</h2>")
        html.AppendLine("<div style=""margin-bottom:14px;color:#4b5563;"">Hazırlanma: " & EncodeHtml(DateTime.Now.ToString("dd.MM.yyyy HH:mm")) &
                        " &nbsp; | &nbsp; Hazırlayan: " & EncodeHtml(AppState.CurrentUserName & " / " & AppState.NormalizeRole(AppState.CurrentRole)) &
                        " &nbsp; | &nbsp; Durum: " & EncodeHtml(statusDescription) &
                        " &nbsp; | &nbsp; Arama: " & EncodeHtml(filterDescription) & "</div>")

        html.AppendLine("<table style=""border-collapse:collapse;margin-bottom:16px;""><tr>")
        AppendSummaryCell(html, "Gösterilen", rows.Count.ToString(), "#e7eef8", "#1f477e")
        AppendSummaryCell(html, "Teknik Resim Var", availableCount.ToString(), "#dcfce7", "#166534")
        AppendSummaryCell(html, "Eksik", missingCount.ToString(), "#fee2e2", "#991b1b")
        html.AppendLine("</tr></table>")

        html.AppendLine("<table style=""border-collapse:collapse;width:100%;font-size:12px;"">")
        html.AppendLine("<thead><tr style=""background:#dfe9f5;color:#183252;"">")
        For Each header In {"DURUM", "TR KODU", "ÜRÜN ADI", "REVİZYON", "TEKNİK RESİM DOSYASI", "PLAN SIRASI", "MAKİNE", "KALIP", "P KODU", "AÇIKLAMA"}
            html.AppendLine("<th style=""border:1px solid #aab8ca;padding:7px;text-align:left;"">" & EncodeHtml(header) & "</th>")
        Next
        html.AppendLine("</tr></thead><tbody>")

        Dim rowIndex As Integer = 0
        For Each row In rows
            Dim backColor = If(row.IsMissing, "#fff1f1", If(rowIndex Mod 2 = 0, "#ffffff", "#f8fafc"))
            html.AppendLine("<tr style=""background:" & backColor & ";"">")
            AppendReportCell(html, If(row.IsMissing, "EKSİK", "VAR"), row.IsMissing, True)
            AppendReportCell(html, row.TrCode, False, False)
            AppendReportCell(html, row.ProductName, String.IsNullOrWhiteSpace(row.ProductName), False)
            AppendReportCell(html, row.DrawingRev, False, False)
            AppendReportCell(html, row.DrawingFile, String.IsNullOrWhiteSpace(row.DrawingFile), False)
            AppendReportCell(html, row.PlanPositions, False, False)
            AppendReportCell(html, row.Machines, False, False)
            AppendReportCell(html, row.MoldCodes, False, False)
            AppendReportCell(html, row.PlasticCodes, False, False)
            AppendReportCell(html, row.IssueText, row.IsMissing, False)
            html.AppendLine("</tr>")
            rowIndex += 1
        Next

        html.AppendLine("</tbody></table>")
        html.AppendLine("<p style=""margin-top:16px;"">Eksik teknik resim ve ürün bilgilerinin tamamlanması rica olunur.</p>")
        html.AppendLine("</body></html>")
        Return html.ToString()
    End Function

    Private Shared Sub AppendSummaryCell(html As StringBuilder, caption As String, value As String, backColor As String, foreColor As String)
        html.AppendLine("<td style=""min-width:120px;border:1px solid #cbd5e1;padding:8px 12px;background:" & backColor & ";color:" & foreColor & ";"">" &
                        "<div style=""font-size:11px;font-weight:600;"">" & EncodeHtml(caption) & "</div>" &
                        "<div style=""font-size:18px;font-weight:700;"">" & EncodeHtml(value) & "</div></td>")
    End Sub

    Private Shared Sub AppendReportCell(html As StringBuilder, value As String, isMissing As Boolean, isBold As Boolean)
        Dim backColor = If(isMissing, "#fee2e2", "transparent")
        Dim foreColor = If(isMissing, "#991b1b", "#1f2937")
        Dim weight = If(isBold, "font-weight:700;", "")
        Dim displayValue = If(String.IsNullOrWhiteSpace(value), "-", value.Trim())
        html.AppendLine("<td style=""border:1px solid #cbd5e1;padding:7px;vertical-align:top;background:" & backColor & ";color:" & foreColor & ";" & weight & """>" &
                        EncodeHtml(displayValue) & "</td>")
    End Sub

    Private Shared Function EncodeHtml(value As String) As String
        Return WebUtility.HtmlEncode(If(value, ""))
    End Function

    Private Sub UpdateButtonState()
        Dim item = If(grid.CurrentRow Is Nothing, Nothing, TryCast(grid.CurrentRow.Tag, TodayMoldDrawingStatusInfo))
        btnOpenProduct.Enabled = item IsNot Nothing AndAlso item.TrCode.Trim() <> ""
    End Sub

    Private Sub SelectCurrentProduct(sender As Object, e As EventArgs)
        Dim item = If(grid.CurrentRow Is Nothing, Nothing, TryCast(grid.CurrentRow.Tag, TodayMoldDrawingStatusInfo))
        If item Is Nothing OrElse item.TrCode.Trim() = "" Then
            MessageBox.Show("Bu plan satırında ürün kaydını açmak için TR kodu bulunmuyor.", "TR kodu eksik", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        _SelectedTrCode = item.TrCode
        _SelectedDrawingRev = item.DrawingRev
        DialogResult = DialogResult.OK
        Close()
    End Sub

    Private Shared Function BuildStatusRows(planRows As List(Of Dictionary(Of String, String)),
                                            products As List(Of ProductInfo)) As List(Of TodayMoldDrawingStatusInfo)
        Dim aggregates As New Dictionary(Of String, PlanAggregate)(StringComparer.OrdinalIgnoreCase)
        For Each planRow In planRows
            AddPlanPart(aggregates, planRow, "Çalışan", "CurrentTrCode", "CurrentMoldNo", "CurrentPlasticCode")
            AddPlanPart(aggregates, planRow, "1. Bağlanacak", "FirstTrCode", "FirstMoldNo", "FirstPlasticCode")
            AddPlanPart(aggregates, planRow, "2. Bağlanacak", "SecondTrCode", "SecondMoldNo", "SecondPlasticCode")
        Next

        Dim activeProducts = If(products, New List(Of ProductInfo)()).
            Where(Function(product) product IsNot Nothing AndAlso String.Equals(product.IsActive, "YES", StringComparison.OrdinalIgnoreCase)).
            ToList()
        Dim result As New List(Of TodayMoldDrawingStatusInfo)()

        For Each aggregate In aggregates.Values
            Dim normalizedTr = NormalizeTrCode(aggregate.TrCode)
            Dim matches = activeProducts.
                Where(Function(product) NormalizeTrCode(product.TrCode) = normalizedTr AndAlso normalizedTr <> "").
                OrderByDescending(Function(product) product.CreatedAt, StringComparer.OrdinalIgnoreCase).
                ThenByDescending(Function(product) product.DrawingRev, StringComparer.OrdinalIgnoreCase).
                ToList()

            Dim productWithFile = matches.FirstOrDefault(Function(product) DrawingFileExists(product))
            Dim selectedProduct = If(productWithFile, matches.FirstOrDefault())
            Dim item As New TodayMoldDrawingStatusInfo With {
                .TrCode = aggregate.TrCode,
                .PlanPositions = JoinValues(aggregate.PlanPositions),
                .Machines = JoinValues(aggregate.Machines),
                .MoldCodes = JoinValues(aggregate.MoldCodes),
                .PlasticCodes = JoinValues(aggregate.PlasticCodes)
            }

            If selectedProduct IsNot Nothing Then
                item.ProductName = ProductNameResolver.Resolve(
                    products,
                    selectedProduct.TrCode,
                    selectedProduct.PlasticCode,
                    selectedProduct.MoldCode)
                item.DrawingRev = selectedProduct.DrawingRev
                item.DrawingFile = selectedProduct.DrawingFile
            Else
                item.ProductName = ProductNameResolver.Resolve(
                    products,
                    aggregate.TrCode,
                    aggregate.PlasticCodes.FirstOrDefault(),
                    aggregate.MoldCodes.FirstOrDefault())
            End If

            If normalizedTr = "" Then
                item.IsMissing = True
                item.IssueText = "Bağlanacak kalıp için TR kodu bulunmuyor."
            ElseIf matches.Count = 0 Then
                item.IsMissing = True
                item.IssueText = "Aktif ürün / teknik resim kaydı bulunamadı."
            ElseIf String.IsNullOrWhiteSpace(item.ProductName) AndAlso productWithFile Is Nothing Then
                item.IsMissing = True
                item.IssueText = "Ürün adı tanımlı değil; teknik resim dosyası da bulunamadı."
            ElseIf String.IsNullOrWhiteSpace(item.ProductName) Then
                item.IsMissing = True
                item.IssueText = "Ürün adı tanımlı değil; kayıt eksik kabul edildi."
            ElseIf productWithFile Is Nothing Then
                item.IsMissing = True
                item.IssueText = If(String.IsNullOrWhiteSpace(item.DrawingFile),
                                    "Teknik resim dosyası kayıtlı değil.",
                                    "Kayıtlı teknik resim dosyası klasörde bulunamadı.")
            Else
                item.IsMissing = False
                item.IssueText = "Teknik resim mevcut."
            End If
            result.Add(item)
        Next

        Return result
    End Function

    Private Shared Sub AddPlanPart(aggregates As Dictionary(Of String, PlanAggregate),
                                   planRow As Dictionary(Of String, String),
                                   planPosition As String,
                                   trColumn As String,
                                   moldColumn As String,
                                   plasticColumn As String)
        Dim trCode = DataService.GetValue(planRow, trColumn).Trim()
        Dim moldCode = DataService.GetValue(planRow, moldColumn).Trim()
        If moldCode = "" AndAlso String.Equals(planPosition, "Çalışan", StringComparison.OrdinalIgnoreCase) Then
            moldCode = DataService.GetValue(planRow, "RunningMolds").Trim()
        End If
        If trCode = "" AndAlso moldCode = "" Then Return

        Dim key = NormalizeTrCode(trCode)
        If key = "" Then
            key = "__TR_EKSIK__|" & DataService.GetValue(planRow, "MachineNo").Trim() & "|" & planPosition & "|" & moldCode
        End If

        Dim aggregate As PlanAggregate = Nothing
        If Not aggregates.TryGetValue(key, aggregate) Then
            aggregate = New PlanAggregate() With {.TrCode = trCode}
            aggregates(key) = aggregate
        End If

        aggregate.PlanPositions.Add(planPosition)
        AddIfNotBlank(aggregate.MoldCodes, moldCode)
        AddIfNotBlank(aggregate.PlasticCodes, DataService.GetValue(planRow, plasticColumn))
        Dim machine = String.Join(" / ", {
            DataService.GetValue(planRow, "MachineNo").Trim(),
            DataService.GetValue(planRow, "MachineName").Trim()}.
            Where(Function(value) value <> ""))
        AddIfNotBlank(aggregate.Machines, machine)
    End Sub

    Private Shared Sub AddIfNotBlank(values As HashSet(Of String), value As String)
        value = If(value, "").Trim()
        If value <> "" Then values.Add(value)
    End Sub

    Private Shared Function DrawingFileExists(product As ProductInfo) As Boolean
        If product Is Nothing OrElse String.IsNullOrWhiteSpace(product.DrawingFile) Then Return False
        Try
            Return File.Exists(AppPaths.ResolveDrawingFilePath(product.DrawingFile.Trim()))
        Catch
            Return False
        End Try
    End Function

    Friend Shared Function NormalizeTrCode(value As String) As String
        Return Regex.Replace(If(value, "").Trim(), "[\s\-_/]+", "").ToUpperInvariant()
    End Function

    Private Shared Function JoinValues(values As HashSet(Of String)) As String
        Return String.Join(", ", values.OrderBy(Function(value) value, StringComparer.OrdinalIgnoreCase))
    End Function

    Private Shared Function DisplayValue(value As String) As String
        Return If(String.IsNullOrWhiteSpace(value), "-", value.Trim())
    End Function
End Class
