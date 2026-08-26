Imports System.Collections.Generic
Imports System.Data
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Net
Imports System.Text
Imports System.Windows.Forms
Imports Microsoft.Web.WebView2.WinForms
Imports Microsoft.Web.WebView2.Core

Public Class FrmMeasurementReview
    Inherits Form

    Private ReadOnly recordId As String
    Private ReadOnly selectedMeasureId As String
    Private currentSelectedMeasureId As String

    Private ReadOnly pdfViewer As New WebView2()
    Private ReadOnly grid As New DataGridView()
    Private ReadOnly visualGrid As New DataGridView()
    Private ReadOnly lblInfo As New Label()
    Private ReadOnly lblZoomValue As New Label()
    Private ReadOnly lblMeasurementTitle As New Label()
    Private ReadOnly lblVisualTitle As New Label()

    Private currentTempPdf As String = ""
    Private currentPdfZoom As Integer = 100
    Private currentTempPng As String = ""
    Private currentTempHtml As String = ""
    Private currentImageUri As String = ""
    Private reviewWebMessageHooked As Boolean = False

    Public Sub New(recordId As String, selectedMeasureId As String)
        AuthorizationService.Require(AppState.CanViewMeasurementHistory OrElse AppState.CanOpenQualityToProductionTickets OrElse AppState.CanOpenQualityTickets, "Olcum Kaydi Inceleme")
        AppIconService.Apply(Me)
        Me.recordId = recordId
        Me.selectedMeasureId = If(selectedMeasureId, "")
        currentSelectedMeasureId = Me.selectedMeasureId

        Text = "Ölçüm Kaydı - Teknik Resim Üzerinde İnceleme"
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        MinimumSize = New Size(760, 560)
        BackColor = Color.White

        Dim split As New SplitContainer() With {
            .Dock = DockStyle.Fill,
            .Orientation = Orientation.Vertical,
            .SplitterWidth = 6
        }
        Controls.Add(split)

        AddHandler Shown, Sub() ResponsiveFormService.FitSplitContainer(split, 0.58R, 280, 360)

        split.Panel1.BackColor = Color.White

        Dim leftLayout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .BackColor = Color.White
        }
        leftLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 44.0F))
        leftLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        split.Panel1.Controls.Add(leftLayout)

        Dim pdfToolbar As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(8, 6, 8, 4),
            .BackColor = SystemColors.Control,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .AutoScroll = True
        }
        leftLayout.Controls.Add(pdfToolbar, 0, 0)

        Dim btnFit As New Button() With {.Text = "Fit", .Width = 58, .Height = 30, .Margin = New Padding(0, 0, 6, 5)}
        AddHandler btnFit.Click, Sub()
                                     currentPdfZoom = 100
                                     UpdateZoomLabel()
                                     ExecutePdfScript("if(window.externalFit){window.externalFit();}")
                                 End Sub

        Dim btnZoomOut As New Button() With {.Text = "-", .Width = 34, .Height = 30, .Margin = New Padding(0, 0, 4, 5)}
        AddHandler btnZoomOut.Click, Sub()
                                         currentPdfZoom = Math.Max(50, currentPdfZoom - 10)
                                         UpdateZoomLabel()
                                         ExecutePdfScript("if(window.externalSetZoom){window.externalSetZoom(" & (currentPdfZoom / 100.0R).ToString(System.Globalization.CultureInfo.InvariantCulture) & ");}")
                                     End Sub

        lblZoomValue.AutoSize = False
        lblZoomValue.Width = 48
        lblZoomValue.Height = 30
        lblZoomValue.TextAlign = ContentAlignment.MiddleCenter
        lblZoomValue.Margin = New Padding(0, 0, 4, 5)
        UpdateZoomLabel()

        Dim btnZoomIn As New Button() With {.Text = "+", .Width = 34, .Height = 30, .Margin = New Padding(0, 0, 10, 5)}
        AddHandler btnZoomIn.Click, Sub()
                                        currentPdfZoom = Math.Min(400, currentPdfZoom + 10)
                                        UpdateZoomLabel()
                                        ExecutePdfScript("if(window.externalSetZoom){window.externalSetZoom(" & (currentPdfZoom / 100.0R).ToString(System.Globalization.CultureInfo.InvariantCulture) & ");}")
                                    End Sub

        Dim btnUp As New Button() With {.Text = "↑", .Width = 34, .Height = 30, .Margin = New Padding(0, 0, 4, 5)}
        AddHandler btnUp.Click, Sub() ExecutePdfScript("if(window.externalScroll){window.externalScroll(0,-160);}")

        Dim btnDown As New Button() With {.Text = "↓", .Width = 34, .Height = 30, .Margin = New Padding(0, 0, 4, 5)}
        AddHandler btnDown.Click, Sub() ExecutePdfScript("if(window.externalScroll){window.externalScroll(0,160);}")

        Dim btnLeft As New Button() With {.Text = "←", .Width = 34, .Height = 30, .Margin = New Padding(0, 0, 4, 5)}
        AddHandler btnLeft.Click, Sub() ExecutePdfScript("if(window.externalScroll){window.externalScroll(-160,0);}")

        Dim btnRight As New Button() With {.Text = "→", .Width = 34, .Height = 30, .Margin = New Padding(0, 0, 8, 5)}
        AddHandler btnRight.Click, Sub() ExecutePdfScript("if(window.externalScroll){window.externalScroll(160,0);}")

        Dim btnRotateLeft As New Button() With {.Text = "Sol 90", .Width = 62, .Height = 30, .Margin = New Padding(0, 0, 4, 5)}
        AddHandler btnRotateLeft.Click, Sub()
                                            ExecutePdfScript("if(window.externalRotate){window.externalRotate(-90);}")
                                        End Sub

        Dim btnRotateRight As New Button() With {.Text = "Sağ 90", .Width = 62, .Height = 30, .Margin = New Padding(0, 0, 4, 5)}
        AddHandler btnRotateRight.Click, Sub()
                                             ExecutePdfScript("if(window.externalRotate){window.externalRotate(90);}")
                                         End Sub

        pdfToolbar.Controls.AddRange({btnFit, btnZoomOut, lblZoomValue, btnZoomIn, btnUp, btnDown, btnLeft, btnRight, btnRotateLeft, btnRotateRight})

        pdfViewer.Dock = DockStyle.Fill
        leftLayout.Controls.Add(pdfViewer, 0, 1)

        Dim rightLayout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .BackColor = Color.White
        }
        rightLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 120.0F))
        rightLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        split.Panel2.Controls.Add(rightLayout)

        Dim top As New Panel() With {.Dock = DockStyle.Fill, .Padding = New Padding(12), .BackColor = SystemColors.Control}
        lblInfo.Dock = DockStyle.Fill
        lblInfo.Font = New Font("Segoe UI", 9.5F, FontStyle.Bold)
        lblInfo.TextAlign = ContentAlignment.MiddleLeft
        top.Controls.Add(lblInfo)
        rightLayout.Controls.Add(top, 0, 0)

        ConfigureGrid()
        ConfigureVisualGrid()

        Dim rightBody As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 4,
            .BackColor = Color.White
        }
        rightBody.RowStyles.Add(New RowStyle(SizeType.Absolute, 28.0F))
        rightBody.RowStyles.Add(New RowStyle(SizeType.Percent, 50.0F))
        rightBody.RowStyles.Add(New RowStyle(SizeType.Absolute, 28.0F))
        rightBody.RowStyles.Add(New RowStyle(SizeType.Percent, 50.0F))

        lblMeasurementTitle.Text = "Ölçüm Sonuçları"
        lblMeasurementTitle.Dock = DockStyle.Fill
        lblMeasurementTitle.TextAlign = ContentAlignment.MiddleLeft
        lblMeasurementTitle.Padding = New Padding(8, 0, 0, 0)
        lblMeasurementTitle.BackColor = Color.WhiteSmoke
        lblMeasurementTitle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)

        lblVisualTitle.Text = "Görsel Kontrol Sonuçları"
        lblVisualTitle.Dock = DockStyle.Fill
        lblVisualTitle.TextAlign = ContentAlignment.MiddleLeft
        lblVisualTitle.Padding = New Padding(8, 0, 0, 0)
        lblVisualTitle.BackColor = Color.WhiteSmoke
        lblVisualTitle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)

        Dim measurementHeader As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 1,
            .BackColor = Color.WhiteSmoke,
            .Padding = New Padding(0),
            .Margin = New Padding(0)
        }
        measurementHeader.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        measurementHeader.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 178.0F))

        Dim btnEditMeasurement As New Button() With {
            .Text = "Seçili Ölçüyü Düzenle",
            .Dock = DockStyle.Fill,
            .Visible = AppState.IsAdmin,
            .Enabled = AppState.IsAdmin,
            .Margin = New Padding(4, 2, 6, 2),
            .BackColor = Color.FromArgb(31, 71, 126),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat
        }
        AddHandler btnEditMeasurement.Click, AddressOf EditSelectedMeasurement_Click

        measurementHeader.Controls.Add(lblMeasurementTitle, 0, 0)
        measurementHeader.Controls.Add(btnEditMeasurement, 1, 0)

        rightBody.Controls.Add(measurementHeader, 0, 0)
        rightBody.Controls.Add(grid, 0, 1)
        rightBody.Controls.Add(lblVisualTitle, 0, 2)
        rightBody.Controls.Add(visualGrid, 0, 3)

        rightLayout.Controls.Add(rightBody, 0, 1)

        AddHandler Shown, AddressOf FrmMeasurementReview_Shown
        AddHandler FormClosed, AddressOf FrmMeasurementReview_FormClosed
    End Sub

    Private Sub UpdateZoomLabel()
        lblZoomValue.Text = currentPdfZoom.ToString() & "%"
    End Sub

    Private Async Sub ExecutePdfScript(script As String)
        Try
            If pdfViewer Is Nothing OrElse pdfViewer.IsDisposed Then Return
            If pdfViewer.CoreWebView2 Is Nothing Then Return
            Await pdfViewer.ExecuteScriptAsync(script)
        Catch ex As Exception
            ErrorLogService.Log("FrmMeasurementReview.ExecutePdfScript", ex)
        End Try
    End Sub

    Private Sub ReviewWebMessageReceived(sender As Object, e As CoreWebView2WebMessageReceivedEventArgs)
        Try
            Dim msg As String = e.TryGetWebMessageAsString()
            If String.IsNullOrWhiteSpace(msg) Then Return

            If msg.StartsWith("zoom|", StringComparison.OrdinalIgnoreCase) Then
                Dim zText = msg.Substring(5).Trim()
                Dim z As Integer = currentPdfZoom
                If Integer.TryParse(zText, z) Then
                    currentPdfZoom = Math.Max(10, Math.Min(400, z))
                    UpdateZoomLabel()
                End If
                Return
            End If

            If msg.StartsWith("select|", StringComparison.OrdinalIgnoreCase) Then
                SelectMeasurementRow(msg.Substring(7))
            End If
        Catch ex As Exception
            ErrorLogService.Log("FrmMeasurementReview.HandleWebMessage", ex)
        End Try
    End Sub

    Private Async Sub FrmMeasurementReview_Shown(sender As Object, e As EventArgs)
        Try
            Dim measurementRows = DataService.GetMeasurementRows().
                Where(Function(r) String.Equals(DataService.GetValue(r, "RecordId"), recordId, StringComparison.OrdinalIgnoreCase)).
                ToList()

            Dim closedRow = DataService.GetClosedEyeRows().
                FirstOrDefault(Function(r) String.Equals(DataService.GetValue(r, "RecordId"), recordId, StringComparison.OrdinalIgnoreCase))

            Dim sourceRow As Dictionary(Of String, String) = Nothing
            If measurementRows.Count > 0 Then
                sourceRow = measurementRows(0)
            ElseIf closedRow IsNot Nothing Then
                sourceRow = closedRow
            End If

            If sourceRow Is Nothing Then
                Throw New InvalidOperationException("Seçili kayıt bulunamadı.")
            End If

            Dim trCode = DataService.GetValue(sourceRow, "TrCode")
            Dim drawingRev = DataService.GetValue(sourceRow, "DrawingRev")
            Dim drawingScope = ProductInfo.NormalizeDrawingScope(DataService.GetValue(sourceRow, "DrawingScope"))
            Dim lotNo = DataService.GetValue(sourceRow, "LotNo")
            Dim serialNo = DataService.GetValue(sourceRow, "SerialNo")
            Dim eyeCount = DataService.GetValue(sourceRow, "EyeCount")
            Dim eyeNo = DataService.GetValue(sourceRow, "EyeNo")
            Dim operatorName = DataService.GetValue(sourceRow, "OperatorName").Trim()
            If operatorName = "" Then operatorName = "-"

            lblInfo.Text = $"Kayıt No: {recordId}    Ölçümü Yapan: {operatorName}" & Environment.NewLine &
                           $"TR / Revizyon: {trCode} / {drawingRev}    İş Emri No: {lotNo}    Seri: {serialNo}    Göz: {eyeNo}/{eyeCount}"

            LoadGrid(measurementRows, closedRow)
            LoadVisualGrid(recordId)

            Dim product = DataService.GetProducts(False).
                FirstOrDefault(Function(p) String.Equals(p.TrCode, trCode, StringComparison.OrdinalIgnoreCase) AndAlso
                                            String.Equals(p.DrawingRev, drawingRev, StringComparison.OrdinalIgnoreCase) AndAlso
                                            String.Equals(ProductInfo.NormalizeDrawingScope(p.DrawingScope), drawingScope, StringComparison.OrdinalIgnoreCase))

            If product Is Nothing Then
                Throw New InvalidOperationException("Bu kayda ait teknik resim ürün listesinde bulunamadı.")
            End If

            lblInfo.Text = $"Kayıt No: {recordId}    Ölçümü Yapan: {operatorName}" & Environment.NewLine &
                           $"TR / Revizyon: {trCode} / {drawingRev}    İş Emri No: {lotNo}    Seri: {serialNo}    Göz: {eyeNo}/{eyeCount}" & Environment.NewLine &
                           $"Plastik Kodu: {If(product.PlasticCode = "", "-", product.PlasticCode)}    Ürün Adı: {If(product.ProductName = "", "-", product.ProductName)}    Malzeme: {If(product.Material = "", "-", product.Material)}    Renk: {If(product.ColorName = "", "-", product.ColorName)}    Kalıp Göz Adedi: {If(product.MoldCavityCount = "", "-", product.MoldCavityCount)}    Kalıp Kodu: {If(product.MoldCode = "", "-", product.MoldCode)}"

            Dim imageUri As String = ""
            If TempFileService.IsEncryptedPdf(product.DrawingFile) Then
                currentTempPdf = TempFileService.DecryptEncryptedPdfToTemp(product.DrawingFile)
                currentTempPng = PdfRenderService.RenderFirstPageToPng(currentTempPdf)
                imageUri = New Uri(currentTempPng).AbsoluteUri
            ElseIf TempFileService.IsEncryptedDxf(product.DrawingFile) Then
                currentTempPdf = TempFileService.DecryptEncryptedDrawingToTemp(product.DrawingFile)
                Dim render = DxfRenderService.RenderToSvg(currentTempPdf)
                currentTempPng = render.SvgPath
                imageUri = New Uri(currentTempPng).AbsoluteUri
            Else
                Throw New InvalidOperationException("Bu kayda ait teknik resim türü desteklenmiyor: " & product.DrawingFile)
            End If

            currentImageUri = imageUri
            currentTempHtml = Path.Combine(AppPaths.TempDir, "pdf_review_" & Guid.NewGuid().ToString("N") & ".html")

            File.WriteAllText(currentTempHtml, BuildHtml(imageUri, measurementRows, currentSelectedMeasureId), New UTF8Encoding(False))

            Await pdfViewer.EnsureCoreWebView2Async()
            If Not reviewWebMessageHooked Then
                AddHandler pdfViewer.CoreWebView2.WebMessageReceived, AddressOf ReviewWebMessageReceived
                reviewWebMessageHooked = True
            End If

            pdfViewer.Source = New Uri(currentTempHtml)
            AuditService.Log("MEASUREMENT_REVIEW_OPEN", trCode, drawingRev, "RecordId=" & recordId)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Teknik resim görüntülenemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Close()
        End Try
    End Sub

    Private Sub ConfigureGrid()
        grid.Dock = DockStyle.Fill
        grid.ReadOnly = True
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.MultiSelect = False
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
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
        grid.RowTemplate.Height = 26
        grid.ScrollBars = ScrollBars.Both
        AddHandler grid.CellFormatting, AddressOf Grid_CellFormatting
        AddHandler grid.CellClick, AddressOf Grid_CellClick
        AddHandler grid.CellDoubleClick, AddressOf Grid_CellDoubleClick
    End Sub

    Private Sub Grid_CellClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 OrElse Not grid.Columns.Contains("MeasureId") Then Return
        Dim measureId = Convert.ToString(grid.Rows(e.RowIndex).Cells("MeasureId").Value).Trim()
        If measureId = "" Then Return
        currentSelectedMeasureId = measureId
        ExecutePdfScript("if(window.externalFocusMarker){window.externalFocusMarker('" & Js(measureId) & "');}")
    End Sub

    Private Sub Grid_CellDoubleClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 OrElse Not AppState.IsAdmin Then Return
        grid.CurrentCell = grid.Rows(e.RowIndex).Cells("MeasureId")
        EditSelectedMeasurement_Click(grid, EventArgs.Empty)
    End Sub

    Private Sub SelectMeasurementRow(measureId As String)
        measureId = If(measureId, "").Trim()
        If measureId = "" OrElse Not grid.Columns.Contains("MeasureId") Then Return
        currentSelectedMeasureId = measureId

        For Each row As DataGridViewRow In grid.Rows
            If String.Equals(Convert.ToString(row.Cells("MeasureId").Value), measureId, StringComparison.OrdinalIgnoreCase) Then
                grid.ClearSelection()
                row.Selected = True
                grid.CurrentCell = row.Cells("MeasureId")
                If row.Index >= 0 Then grid.FirstDisplayedScrollingRowIndex = row.Index
                Exit For
            End If
        Next
    End Sub

    Private Async Sub EditSelectedMeasurement_Click(sender As Object, e As EventArgs)
        Try
            AuthorizationService.Require(AppState.IsAdmin, "Geçmiş Ölçüm Düzeltme")
            If grid.CurrentRow Is Nothing OrElse Not grid.Columns.Contains("MeasureId") Then
                MessageBox.Show("Önce düzenlenecek ölçüm satırını seçin.", "Ölçüm seçin", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim measureId = Convert.ToString(grid.CurrentRow.Cells("MeasureId").Value).Trim()
            If measureId = "" Then
                MessageBox.Show("Bu satır düzenlenebilir bir ölçüm kaydı değildir.", "Ölçüm düzenleme", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim eyeNo = GetGridCellValue(grid.CurrentRow, "EyeNo")
            Dim measurementDate = GetGridCellValue(grid.CurrentRow, "MeasurementDate")
            Dim candidates = DataService.GetMeasurementRows().
                Where(Function(r) String.Equals(DataService.GetValue(r, "RecordId").Trim(), recordId, StringComparison.OrdinalIgnoreCase) AndAlso
                                  String.Equals(DataService.GetValue(r, "MeasureId").Trim(), measureId, StringComparison.OrdinalIgnoreCase)).
                ToList()

            If eyeNo <> "" Then
                candidates = candidates.
                    Where(Function(r) String.Equals(DataService.GetValue(r, "EyeNo").Trim(), eyeNo, StringComparison.OrdinalIgnoreCase)).
                    ToList()
            End If
            If candidates.Count > 1 AndAlso measurementDate <> "" Then
                candidates = candidates.
                    Where(Function(r) String.Equals(DataService.GetValue(r, "MeasurementDate").Trim(), measurementDate, StringComparison.OrdinalIgnoreCase)).
                    ToList()
            End If

            If candidates.Count <> 1 Then
                MessageBox.Show(
                    "Seçili ölçüm satırı güvenli biçimde belirlenemedi. Lütfen listeyi yenileyip tekrar deneyin.",
                    "Ölçüm düzenleme",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning)
                Return
            End If

            currentSelectedMeasureId = measureId
            If MeasurementCorrectionUiService.EditMeasurement(Me, candidates(0)) Then
                Await ReloadMeasurementResultsAsync()
            End If
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            ErrorLogService.Log("FrmMeasurementReview.EditMeasurement", ex)
            MessageBox.Show(ex.Message, "Ölçüm düzenlenemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Shared Function GetGridCellValue(row As DataGridViewRow, columnName As String) As String
        If row Is Nothing OrElse row.DataGridView Is Nothing OrElse Not row.DataGridView.Columns.Contains(columnName) Then Return ""
        Return Convert.ToString(row.Cells(columnName).Value).Trim()
    End Function

    Private Async Function ReloadMeasurementResultsAsync() As Threading.Tasks.Task
        Dim measurementRows = DataService.GetMeasurementRows().
            Where(Function(r) String.Equals(DataService.GetValue(r, "RecordId"), recordId, StringComparison.OrdinalIgnoreCase)).
            ToList()
        Dim closedRow = DataService.GetClosedEyeRows().
            FirstOrDefault(Function(r) String.Equals(DataService.GetValue(r, "RecordId"), recordId, StringComparison.OrdinalIgnoreCase))

        LoadGrid(measurementRows, closedRow)

        If currentTempHtml <> "" AndAlso currentImageUri <> "" AndAlso File.Exists(currentTempHtml) Then
            File.WriteAllText(
                currentTempHtml,
                BuildHtml(currentImageUri, measurementRows, currentSelectedMeasureId),
                New UTF8Encoding(False))
            If pdfViewer.CoreWebView2 IsNot Nothing Then
                pdfViewer.Reload()
                Await Threading.Tasks.Task.Delay(80)
            End If
        End If
    End Function

    Private Sub ConfigureVisualGrid()
        visualGrid.Dock = DockStyle.Fill
        visualGrid.ReadOnly = True
        visualGrid.AllowUserToAddRows = False
        visualGrid.AllowUserToDeleteRows = False
        visualGrid.MultiSelect = False
        visualGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        visualGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
        visualGrid.RowHeadersVisible = False
        visualGrid.BackgroundColor = Color.White
        visualGrid.BorderStyle = BorderStyle.FixedSingle
        visualGrid.GridColor = Color.Gainsboro
        visualGrid.EnableHeadersVisualStyles = False
        visualGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 235, 235)
        visualGrid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        visualGrid.DefaultCellStyle.BackColor = Color.White
        visualGrid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248)
        visualGrid.RowTemplate.Height = 26
        visualGrid.ScrollBars = ScrollBars.Both
        AddHandler visualGrid.CellFormatting, AddressOf VisualGrid_CellFormatting
    End Sub

    Private Sub LoadGrid(measurementRows As List(Of Dictionary(Of String, String)), closedRow As Dictionary(Of String, String))
        Dim dt As New DataTable()
        dt.Columns.Add("MeasureId")
        dt.Columns.Add("MeasureName")
        dt.Columns.Add("Nominal")
        dt.Columns.Add("LowerLimit")
        dt.Columns.Add("UpperLimit")
        dt.Columns.Add("MeasuredValue")
        dt.Columns.Add("Result")
        dt.Columns.Add("Note")
        dt.Columns.Add("EyeNo")
        dt.Columns.Add("MeasurementDate")

        If measurementRows.Count = 0 AndAlso closedRow IsNot Nothing Then
            Dim dr = dt.NewRow()
            dr("MeasureId") = ""
            dr("MeasureName") = "GÖZ KAPALI"
            dr("Result") = "GÖZ KAPALI"
            dr("Note") = DataService.GetValue(closedRow, "Reason")
            dt.Rows.Add(dr)
        Else
            For Each r In measurementRows
                Dim dr = dt.NewRow()
                dr("MeasureId") = DataService.GetValue(r, "MeasureId")
                dr("MeasureName") = DataService.GetValue(r, "MeasureName")
                dr("Nominal") = DataService.GetValue(r, "Nominal")
                dr("LowerLimit") = DataService.GetValue(r, "LowerLimit")
                dr("UpperLimit") = DataService.GetValue(r, "UpperLimit")
                dr("MeasuredValue") = DataService.GetValue(r, "MeasuredValue")
                dr("Result") = DataService.GetValue(r, "Result")
                dr("Note") = DataService.GetValue(r, "Note")
                dr("EyeNo") = DataService.GetValue(r, "EyeNo")
                dr("MeasurementDate") = DataService.GetValue(r, "MeasurementDate")
                dt.Rows.Add(dr)
            Next
        End If

        grid.DataSource = dt
        lblMeasurementTitle.Text = "Ölçüm Sonuçları: " & dt.Rows.Count.ToString() & " satır"
        SetHeader("MeasureId", "Ölçü No", 130, 0)
        SetHeader("MeasureName", "Ölçü Adı", 160, 1)
        SetHeader("Nominal", "Nominal", 75, 2)
        SetHeader("LowerLimit", "Alt Limit", 75, 3)
        SetHeader("UpperLimit", "Üst Limit", 75, 4)
        SetHeader("MeasuredValue", "Girilen", 75, 5)
        SetHeader("Result", "Sonuç", 80, 6)
        SetHeader("Note", "Not", 220, 7)
        If grid.Columns.Contains("EyeNo") Then grid.Columns("EyeNo").Visible = False
        If grid.Columns.Contains("MeasurementDate") Then grid.Columns("MeasurementDate").Visible = False

        If currentSelectedMeasureId <> "" AndAlso grid.Columns.Contains("MeasureId") Then
            For Each row As DataGridViewRow In grid.Rows
                If String.Equals(Convert.ToString(row.Cells("MeasureId").Value), currentSelectedMeasureId, StringComparison.OrdinalIgnoreCase) Then
                    row.Selected = True
                    grid.CurrentCell = row.Cells("MeasureId")
                    grid.FirstDisplayedScrollingRowIndex = Math.Max(0, row.Index)
                    Exit For
                End If
            Next
        End If
    End Sub

    Private Sub LoadVisualGrid(recordId As String)
        Dim visualRows = DataService.GetVisualControlRows().
            Where(Function(r) String.Equals(DataService.GetValue(r, "RecordId"), recordId, StringComparison.OrdinalIgnoreCase)).
            ToList()

        Dim dt As New DataTable()
        dt.Columns.Add("ControlDate")
        dt.Columns.Add("ControlName")
        dt.Columns.Add("Result")
        dt.Columns.Add("Note")

        If visualRows.Count = 0 Then
            Dim dr = dt.NewRow()
            dr("ControlDate") = ""
            dr("ControlName") = "Görsel kontrol kaydı yok"
            dr("Result") = "YOK"
            dr("Note") = ""
            dt.Rows.Add(dr)
        Else
            For Each r In visualRows
                Dim dr = dt.NewRow()
                dr("ControlDate") = DataService.GetValue(r, "ControlDate")
                dr("ControlName") = DataService.GetValue(r, "ControlName")
                dr("Result") = DataService.GetValue(r, "Result")
                dr("Note") = DataService.GetValue(r, "Note")
                dt.Rows.Add(dr)
            Next
        End If

        visualGrid.DataSource = dt
        SetVisualHeader("ControlDate", "Kontrol Tarihi", 135, 0)
        SetVisualHeader("ControlName", "Görsel Kontrol", 190, 1)
        SetVisualHeader("Result", "Sonuç", 95, 2)
        SetVisualHeader("Note", "Not", 260, 3)

        lblVisualTitle.Text = "Görsel Kontrol Sonuçları: " & visualRows.Count.ToString() & " kayıt"
    End Sub

    Private Sub SetVisualHeader(columnName As String, headerText As String, width As Integer, displayIndex As Integer)
        If visualGrid.Columns.Contains(columnName) Then
            Dim c = visualGrid.Columns(columnName)
            c.HeaderText = headerText
            c.Width = width
            c.DisplayIndex = Math.Min(displayIndex, visualGrid.Columns.Count - 1)
        End If
    End Sub

    Private Sub SetHeader(columnName As String, headerText As String, width As Integer, displayIndex As Integer)
        If grid.Columns.Contains(columnName) Then
            Dim c = grid.Columns(columnName)
            c.HeaderText = headerText
            c.Width = width
            c.DisplayIndex = Math.Min(displayIndex, grid.Columns.Count - 1)
        End If
    End Sub

    Private Sub Grid_CellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If e.RowIndex < 0 OrElse Not grid.Columns.Contains("Result") Then Return

        Dim resultText = Convert.ToString(grid.Rows(e.RowIndex).Cells("Result").Value)
        If String.Equals(resultText, "OK", StringComparison.OrdinalIgnoreCase) Then
            grid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.Honeydew
        ElseIf String.Equals(resultText, "NOK", StringComparison.OrdinalIgnoreCase) Then
            grid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.MistyRose
        ElseIf String.Equals(resultText, "HATALI", StringComparison.OrdinalIgnoreCase) Then
            grid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.LightYellow
        ElseIf String.Equals(resultText, "GÖZ KAPALI", StringComparison.OrdinalIgnoreCase) Then
            grid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.LightSteelBlue
        End If
    End Sub

    Private Sub VisualGrid_CellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If e.RowIndex < 0 OrElse Not visualGrid.Columns.Contains("Result") Then Return

        Dim resultText = Convert.ToString(visualGrid.Rows(e.RowIndex).Cells("Result").Value).Trim().ToUpperInvariant()

        If resultText = "UYGUN" OrElse resultText = "OK" Then
            visualGrid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.Honeydew
            visualGrid.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.DarkGreen
        ElseIf resultText = "UYGUN DEĞİL" OrElse resultText = "UYGUNSUZ" OrElse resultText = "NOK" Then
            visualGrid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.MistyRose
            visualGrid.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.DarkRed
        ElseIf resultText = "YOK" Then
            visualGrid.Rows(e.RowIndex).DefaultCellStyle.BackColor = Color.WhiteSmoke
            visualGrid.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.DimGray
        Else
            visualGrid.Rows(e.RowIndex).DefaultCellStyle.BackColor = If(e.RowIndex Mod 2 = 0, Color.White, Color.FromArgb(248, 248, 248))
            visualGrid.Rows(e.RowIndex).DefaultCellStyle.ForeColor = Color.Black
        End If
    End Sub

    Private Function BuildHtml(imageUri As String, measurementRows As List(Of Dictionary(Of String, String)), selectedMeasureId As String) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>")
        sb.AppendLine("<style>")
        sb.AppendLine("html,body{margin:0;width:100%;height:100%;overflow:hidden;background:#ffffff;font-family:Segoe UI,Arial;}")
        sb.AppendLine("#viewport{position:relative;width:100%;height:100%;overflow:auto;background:#ffffff;}")
        sb.AppendLine("#stage{position:absolute;left:0;top:0;background:#ffffff;}")
        sb.AppendLine("#pdf{position:absolute;left:0;top:0;display:block;user-select:none;-webkit-user-drag:none;pointer-events:none;transform-origin:center center;}")
        sb.AppendLine("#overlay{position:absolute;left:0;top:0;width:100%;height:100%;z-index:20;pointer-events:none;}")
        sb.AppendLine(".m{position:absolute;transform:translate(-50%,-50%);width:34px;min-width:34px;height:34px;padding:0;border-radius:50%;border:2px solid #ffffff;background:rgba(220,53,69,.94);color:#ffffff;font:800 12px/30px Segoe UI,Arial;text-align:center;box-shadow:0 1px 4px rgba(0,0,0,.35);white-space:nowrap;pointer-events:auto;cursor:pointer;}")
        sb.AppendLine(".ok{background:rgba(40,167,69,.94);}")
        sb.AppendLine(".nok{background:rgba(220,53,69,.94);}")
        sb.AppendLine(".bad{background:rgba(255,193,7,.96);color:#222;}")
        sb.AppendLine(".sel{box-shadow:0 0 0 3px rgba(255,204,0,.65),0 1px 4px rgba(0,0,0,.35);border-color:#ffcc00;}")
        sb.AppendLine("</style></head><body>")
        sb.AppendLine("<div id='viewport'><div id='stage'><img id='pdf' src='" & WebUtility.HtmlEncode(imageUri) & "'/><div id='overlay'></div></div></div>")
        sb.AppendLine("<script>")
        sb.AppendLine("const viewport=document.getElementById('viewport'); const stage=document.getElementById('stage'); const img=document.getElementById('pdf'); const overlay=document.getElementById('overlay');")
        sb.AppendLine("const markers={}; let selectedId=''; let zoom=1; let baseW=0; let baseH=0; let rotation=0;")
        sb.AppendLine("function imageAspect(){ const w=img.naturalWidth||1; const h=img.naturalHeight||1; return w/h; }")
        sb.AppendLine("function visibleAspect(){ const a=imageAspect(); return (rotation%180===0)?a:(1/a); }")
        sb.AppendLine("function origToStage(x,y){ x=parseFloat(x)||0; y=parseFloat(y)||0; if(rotation===90){ return {x:100-y,y:x}; } if(rotation===180){ return {x:100-x,y:100-y}; } if(rotation===270){ return {x:y,y:100-x}; } return {x:x,y:y}; }")
        sb.AppendLine("function layoutImage(){ const sw=stage.clientWidth, sh=stage.clientHeight; if(rotation%180===0){ img.style.left='0px'; img.style.top='0px'; img.style.width=sw+'px'; img.style.height=sh+'px'; } else { img.style.width=sh+'px'; img.style.height=sw+'px'; img.style.left=((sw-sh)/2)+'px'; img.style.top=((sh-sw)/2)+'px'; } img.style.transform='rotate('+rotation+'deg)'; }")
        sb.AppendLine("function updateMarkerPosition(btn){ const pt=origToStage(btn.dataset.x,btn.dataset.y); btn.style.left=pt.x+'%'; btn.style.top=pt.y+'%'; }")
        sb.AppendLine("function updateAllMarkers(){ Object.keys(markers).forEach(function(k){ updateMarkerPosition(markers[k]); }); }")
        sb.AppendLine("function placeStage(){ const sw=baseW*zoom, sh=baseH*zoom; stage.style.width=sw+'px'; stage.style.height=sh+'px'; const left=Math.max(0,(viewport.clientWidth-sw)/2); const top=Math.max(0,(viewport.clientHeight-sh)/2); stage.style.left=left+'px'; stage.style.top=top+'px'; layoutImage(); updateAllMarkers(); }")
        sb.AppendLine("function fitStage(){ const fitMargin=.98; const vw=Math.max(10,viewport.clientWidth*fitMargin); const vh=Math.max(10,viewport.clientHeight*fitMargin); const a=visibleAspect(); let w=vw; let h=w/a; if(h>vh){ h=vh; w=h*a; } baseW=w; baseH=h; zoom=1; placeStage(); viewport.scrollLeft=0; viewport.scrollTop=0; notifyZoom(); }")
        sb.AppendLine("function notifyZoom(){ try{ if(window.chrome && window.chrome.webview){ window.chrome.webview.postMessage('zoom|' + Math.round(zoom*100)); } }catch(e){} }")
        sb.AppendLine("function applyZoom(newZoom,clientX,clientY){ if(!baseW||!baseH) fitStage(); const old=zoom; const sx=viewport.scrollLeft,sy=viewport.scrollTop; const hasPoint=(typeof clientX==='number'&&typeof clientY==='number'); zoom=Math.max(.5,Math.min(4,newZoom)); if(hasPoint){ const rect=viewport.getBoundingClientRect(); const px=(clientX-rect.left+viewport.scrollLeft-(parseFloat(stage.style.left)||0))/old; const py=(clientY-rect.top+viewport.scrollTop-(parseFloat(stage.style.top)||0))/old; placeStage(); viewport.scrollLeft=px*zoom+(parseFloat(stage.style.left)||0)-(clientX-rect.left); viewport.scrollTop=py*zoom+(parseFloat(stage.style.top)||0)-(clientY-rect.top); } else { placeStage(); if(zoom<=1){viewport.scrollLeft=0;viewport.scrollTop=0;}else{viewport.scrollLeft=sx;viewport.scrollTop=sy;} } notifyZoom(); }")
        sb.AppendLine("viewport.addEventListener('wheel',function(ev){ if(ev.ctrlKey){ ev.preventDefault(); applyZoom(zoom*(ev.deltaY<0?1.12:.89),ev.clientX,ev.clientY); } },{passive:false});")
        sb.AppendLine("function centerMarker(btn){if(!btn)return;setTimeout(function(){const stageLeft=parseFloat(stage.style.left)||0;const stageTop=parseFloat(stage.style.top)||0;viewport.scrollLeft=Math.max(0,stageLeft+btn.offsetLeft-(viewport.clientWidth/2));viewport.scrollTop=Math.max(0,stageTop+btn.offsetTop-(viewport.clientHeight/2));},20);}")
        sb.AppendLine("function focusMarker(btn){if(!btn)return;if(zoom<1.8){applyZoom(1.8);}centerMarker(btn);}")
        sb.AppendLine("function selectMarker(id,focus){ selectedId=id||''; Object.keys(markers).forEach(function(k){markers[k].classList.toggle('sel',k===selectedId);}); if(focus&&markers[selectedId])focusMarker(markers[selectedId]); }")
        sb.AppendLine("window.externalSetZoom=function(z){applyZoom(parseFloat(z));if(selectedId&&markers[selectedId])centerMarker(markers[selectedId]);};")
        sb.AppendLine("window.externalFit=function(){ fitStage(); };")
        sb.AppendLine("window.externalFocusMarker=function(id){ selectMarker(id,true); };")
        sb.AppendLine("window.externalScroll=function(dx,dy){ viewport.scrollLeft+=dx; viewport.scrollTop+=dy; };")
        sb.AppendLine("window.externalRotate=function(delta){ rotation=(rotation+delta+360)%360; fitStage(); if(selectedId&&markers[selectedId])setTimeout(function(){focusMarker(markers[selectedId]);},30); };")
        sb.AppendLine("window.addEventListener('resize',fitStage);")
        Dim balloonIndex As Integer = 0
        For Each r In measurementRows
            balloonIndex += 1
            Dim id = Js(DataService.GetValue(r, "MeasureId"))
            If id = "" Then Continue For
            Dim xValue = NormalizePercentValue(DataService.GetValue(r, "XPercent"))
            Dim yValue = NormalizePercentValue(DataService.GetValue(r, "YPercent"))
            If xValue <= 0D OrElse yValue <= 0D Then Continue For
            Dim xText = NumberUtil.DecToCsv(xValue)
            Dim yText = NumberUtil.DecToCsv(yValue)

            Dim resultText = DataService.GetValue(r, "Result")
            Dim cls As String = "m"
            If String.Equals(resultText, "OK", StringComparison.OrdinalIgnoreCase) Then
                cls &= " ok"
            ElseIf String.Equals(resultText, "NOK", StringComparison.OrdinalIgnoreCase) Then
                cls &= " nok"
            ElseIf String.Equals(resultText, "HATALI", StringComparison.OrdinalIgnoreCase) Then
                cls &= " bad"
            End If

            If String.Equals(id, selectedMeasureId, StringComparison.OrdinalIgnoreCase) Then
                cls &= " sel"
            End If

            Dim balloonNo As Integer = 0
            Integer.TryParse(DataService.GetValue(r, "SortNo"), balloonNo)
            If balloonNo <= 0 Then balloonNo = balloonIndex
            Dim title = "Balon " & balloonNo.ToString() & " | " & id & " | " & DataService.GetValue(r, "MeasureName") &
                        " | Grup: " & DataService.GetValue(r, "MeasurementGroup") &
                        " | Değer: " & DataService.GetValue(r, "MeasuredValue") & " | Sonuç: " & resultText
            sb.AppendLine("(function(){var b=document.createElement('button');b.type='button';b.className='" & cls & "';b.dataset.id='" & id & "';b.dataset.x='" & xText & "';b.dataset.y='" & yText & "';b.textContent='" & balloonNo.ToString() & "';b.title='" & Js(title) & "';b.addEventListener('click',function(ev){ev.preventDefault();ev.stopPropagation();selectMarker(this.dataset.id,true);if(window.chrome&&window.chrome.webview){window.chrome.webview.postMessage('select|'+this.dataset.id);}});overlay.appendChild(b);markers['" & id & "']=b;updateMarkerPosition(b);})();")
        Next
        sb.AppendLine("const initialSelectedId='" & Js(selectedMeasureId) & "';")
        sb.AppendLine("function initializeView(){fitStage();if(initialSelectedId&&markers[initialSelectedId]){selectMarker(initialSelectedId,false);}requestAnimationFrame(function(){fitStage();});setTimeout(function(){fitStage();},180);}")
        sb.AppendLine("if(img.complete){setTimeout(initializeView,60);}else{img.onload=function(){initializeView();};}")
        sb.AppendLine("</script></body></html>")
        Return sb.ToString()
    End Function

    Private Function Js(value As String) As String
        If value Is Nothing Then Return ""
        Return value.Replace("\", "\\").Replace("'", "\'").Replace(vbCr, "").Replace(vbLf, " ")
    End Function

    Private Shared Function NormalizePercentValue(value As String) As Decimal
        Dim result = NumberUtil.CsvToDec(value)
        While result > 100D
            result /= 100D
        End While
        If result < 0D Then Return 0D
        If result > 100D Then Return 100D
        Return result
    End Function

    Private Sub FrmMeasurementReview_FormClosed(sender As Object, e As FormClosedEventArgs)
        TempFileService.TryDeleteTempPdf(currentTempPdf)
        TempFileService.TryDeleteTempPdf(currentTempPng)
        TempFileService.TryDeleteTempPdf(currentTempHtml)
        pdfViewer.Dispose()
    End Sub
End Class
