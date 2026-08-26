Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Net
Imports System.Text
Imports System.Text.Json
Imports System.Windows.Forms
Imports Microsoft.Web.WebView2.Core
Imports Microsoft.Web.WebView2.WinForms

Public Class FrmCadDimensionPreview
    Inherits Form

    Private ReadOnly grid As New DataGridView()
    Private ReadOnly drawingViewer As New WebView2()
    Private ReadOnly lblSummary As New Label()
    Private ReadOnly lblPreviewZoom As New Label()
    Private ReadOnly layerFilterList As New CheckedListBox()
    Private ReadOnly candidates As List(Of CadDimensionCandidate)
    Private ReadOnly sourceDrawingPath As String
    Private ReadOnly previewSplit As New SplitContainer()
    Private tempSvgPath As String = ""
    Private tempHtmlPath As String = ""
    Private drawingPreviewReady As Boolean = False
    Private drawingMessageHandlerAttached As Boolean = False
    Private layerFilterUpdating As Boolean = False

    Public ReadOnly Property SelectedCandidates As List(Of CadDimensionCandidate)
        Get
            Return ReadCandidatesFromGrid(True)
        End Get
    End Property

    Public Sub New(items As IEnumerable(Of CadDimensionCandidate), sourceFileName As String, Optional drawingPath As String = "")
        candidates = If(items, Enumerable.Empty(Of CadDimensionCandidate)()).ToList()
        sourceDrawingPath = If(drawingPath, "").Trim()
        AppIconService.Apply(Me)
        Text = "CAD/DXF Kontrol Ölçüsü Adayları"
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        MinimumSize = New Size(1050, 620)
        BackColor = Color.White

        Dim main As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .Padding = New Padding(10),
            .BackColor = Color.White
        }
        main.RowStyles.Add(New RowStyle(SizeType.Absolute, 116.0F))
        main.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        main.RowStyles.Add(New RowStyle(SizeType.Absolute, 58.0F))
        Controls.Add(main)

        Dim header As New Panel() With {.Dock = DockStyle.Fill, .BackColor = Color.FromArgb(242, 247, 252)}
        Dim title As New Label() With {
            .Text = "CAD/DXF ölçü adaylarını gözden geçirin",
            .Font = New Font("Segoe UI", 13.0F, FontStyle.Bold),
            .AutoSize = True,
            .Location = New Point(12, 8)
        }
        lblSummary.Text = $"Dosya: {sourceFileName} | Aday: {candidates.Count} | İşaretli: {candidates.Where(Function(candidate) candidate.IsSelected).Count()}"
        lblSummary.AutoSize = True
        lblSummary.Location = New Point(14, 40)
        lblSummary.ForeColor = Color.FromArgb(45, 70, 95)
        header.Controls.AddRange({title, lblSummary})
        AddLayerFilterControls(header)
        main.Controls.Add(header, 0, 0)

        ConfigureGrid()
        ConfigureDrawingPreview()
        previewSplit.Dock = DockStyle.Fill
        previewSplit.Orientation = Orientation.Vertical
        previewSplit.SplitterWidth = 7
        previewSplit.Panel1MinSize = 25
        previewSplit.Panel2MinSize = 25
        previewSplit.BackColor = Color.FromArgb(225, 232, 240)

        Dim drawingPanel As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .BackColor = Color.White
        }
        drawingPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 30.0F))
        drawingPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))
        drawingPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim drawingTitle As New Label() With {
            .Text = "Çizim önizlemesi - balonlar ölçü yazısının solunda gösterilir",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(10, 0, 0, 0),
            .ForeColor = Color.FromArgb(45, 70, 95),
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        }
        drawingPanel.Controls.Add(drawingTitle, 0, 0)
        drawingPanel.Controls.Add(CreateDrawingPreviewTools(), 0, 1)
        drawingPanel.Controls.Add(drawingViewer, 0, 2)

        previewSplit.Panel1.Controls.Add(drawingPanel)
        previewSplit.Panel2.Controls.Add(grid)
        main.Controls.Add(previewSplit, 0, 1)

        Dim actions As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.RightToLeft,
            .WrapContents = False,
            .Padding = New Padding(8),
            .BackColor = Color.WhiteSmoke
        }

        Dim btnImport As New Button() With {
            .Text = "Seçilenleri Aktar",
            .Width = 160,
            .Height = 34,
            .BackColor = Color.FromArgb(220, 238, 255),
            .ForeColor = Color.FromArgb(20, 65, 120),
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .Cursor = Cursors.Hand,
            .FlatStyle = FlatStyle.Flat
        }
        AddHandler btnImport.Click, AddressOf Import_Click

        Dim btnCancel As New Button() With {.Text = "İptal", .Width = 100, .Height = 34, .Cursor = Cursors.Hand}
        AddHandler btnCancel.Click, Sub()
                                        DialogResult = DialogResult.Cancel
                                        Close()
                                    End Sub

        Dim btnSelectAll As New Button() With {.Text = "Tümünü Seç", .Width = 110, .Height = 34, .Cursor = Cursors.Hand}
        AddHandler btnSelectAll.Click, Sub() SetAllSelected(True)
        Dim btnClearAll As New Button() With {.Text = "Seçimi Temizle", .Width = 125, .Height = 34, .Cursor = Cursors.Hand}
        AddHandler btnClearAll.Click, Sub() SetAllSelected(False)

        actions.Controls.AddRange({btnImport, btnCancel, btnClearAll, btnSelectAll})
        main.Controls.Add(actions, 0, 2)

        LoadRows()
        PopulateLayerFilter()
        AddHandler Shown, AddressOf FrmCadDimensionPreview_Shown
        AddHandler FormClosed, AddressOf FrmCadDimensionPreview_FormClosed
    End Sub

    Private Sub AddLayerFilterControls(header As Panel)
        Dim lblLayer As New Label() With {
            .Text = "Layer seçimi",
            .AutoSize = True,
            .Location = New Point(14, 72),
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .ForeColor = Color.FromArgb(45, 70, 95)
        }

        layerFilterList.CheckOnClick = True
        layerFilterList.MultiColumn = True
        layerFilterList.IntegralHeight = False
        layerFilterList.HorizontalScrollbar = True
        layerFilterList.Location = New Point(110, 62)
        layerFilterList.Size = New Size(520, 44)
        layerFilterList.Font = New Font("Segoe UI", 9.0F, FontStyle.Regular)
        AddHandler layerFilterList.ItemCheck,
            Sub()
                If layerFilterUpdating Then Return
                BeginInvoke(New Action(AddressOf ApplyLayerSelection))
            End Sub

        Dim btnAllLayers As New Button() With {
            .Text = "Tüm layerlar",
            .Location = New Point(642, 66),
            .Size = New Size(105, 30),
            .Cursor = Cursors.Hand
        }
        AddHandler btnAllLayers.Click, Sub() SetAllLayerChecks(True)

        Dim btnClearLayers As New Button() With {
            .Text = "Layer seçimini temizle",
            .Location = New Point(755, 66),
            .Size = New Size(150, 30),
            .Cursor = Cursors.Hand
        }
        AddHandler btnClearLayers.Click, Sub() SetAllLayerChecks(False)

        Dim lblHint As New Label() With {
            .Text = "İşaretli layer'lardaki DIMENSION ölçüleri aktarılır.",
            .AutoSize = True,
            .Location = New Point(915, 72),
            .ForeColor = Color.FromArgb(85, 100, 120),
            .Font = New Font("Segoe UI", 8.5F, FontStyle.Regular)
        }

        header.Controls.AddRange({lblLayer, layerFilterList, btnAllLayers, btnClearLayers, lblHint})
    End Sub

    Private Sub ConfigureGrid()
        grid.Dock = DockStyle.Fill
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.RowHeadersVisible = False
        grid.MultiSelect = False
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.AutoGenerateColumns = False
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
        grid.BackgroundColor = Color.White
        grid.BorderStyle = BorderStyle.FixedSingle
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(230, 235, 241)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        grid.ColumnHeadersHeight = 38
        grid.RowTemplate.Height = 30

        grid.Columns.Add(New DataGridViewCheckBoxColumn() With {.Name = "Selected", .HeaderText = "AL", .Width = 46})
        grid.Columns.Add(TextColumn("Layer", "LAYER", 125, True))
        grid.Columns.Add(TextColumn("MeasureId", "ÖLÇÜ NO", 130))
        grid.Columns.Add(TextColumn("MeasureName", "ÖLÇÜ ADI", 180))
        grid.Columns.Add(TextColumn("Nominal", "NOMİNAL", 85))
        grid.Columns.Add(TextColumn("LowerTolerance", "ALT TOL.", 80))
        grid.Columns.Add(TextColumn("UpperTolerance", "ÜST TOL.", 80))
        grid.Columns.Add(TextColumn("Unit", "BİRİM", 60))
        grid.Columns.Add(TextColumn("PageNo", "SAYFA", 60))
        grid.Columns.Add(TextColumn("XPercent", "X %", 65))
        grid.Columns.Add(TextColumn("YPercent", "Y %", 65))
        grid.Columns.Add(TextColumn("Layout", "LAYOUT", 80, True))
        grid.Columns.Add(TextColumn("Type", "ÖLÇÜ TİPİ", 150, True))
        grid.Columns.Add(TextColumn("Warning", "UYARI", 220, True))

        AddHandler grid.CurrentCellDirtyStateChanged,
            Sub()
                If grid.IsCurrentCellDirty Then grid.CommitEdit(DataGridViewDataErrorContexts.Commit)
            End Sub
        AddHandler grid.CellValueChanged, Sub()
                                              UpdateSummary()
                                              UpdateDrawingMarkers()
                                          End Sub
        AddHandler grid.DataError,
            Sub(sender, e)
                e.ThrowException = False
            End Sub
    End Sub

    Private Sub ConfigureDrawingPreview()
        drawingViewer.Dock = DockStyle.Fill
        drawingViewer.AllowExternalDrop = False
        drawingViewer.DefaultBackgroundColor = Color.White
        AddHandler drawingViewer.NavigationCompleted, AddressOf DrawingViewer_NavigationCompleted
    End Sub

    Private Function CreateDrawingPreviewTools() As Control
        Dim tools As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .Padding = New Padding(4, 4, 4, 2),
            .BackColor = Color.WhiteSmoke
        }

        Dim btnFit = PreviewToolButton("Fit", 54)
        AddHandler btnFit.Click, Sub() ExecuteDrawingPreviewScript("if(window.externalFit){window.externalFit();}")

        Dim btnZoomOut = PreviewToolButton("-", 34)
        AddHandler btnZoomOut.Click, Sub() ExecuteDrawingPreviewScript("if(window.externalZoomBy){window.externalZoomBy(0.9);}")

        lblPreviewZoom.Text = "100%"
        lblPreviewZoom.Width = 58
        lblPreviewZoom.Height = 28
        lblPreviewZoom.TextAlign = ContentAlignment.MiddleCenter
        lblPreviewZoom.Margin = New Padding(2, 0, 8, 4)
        lblPreviewZoom.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)

        Dim btnZoomIn = PreviewToolButton("+", 34)
        AddHandler btnZoomIn.Click, Sub() ExecuteDrawingPreviewScript("if(window.externalZoomBy){window.externalZoomBy(1.1);}")

        Dim btnUp = PreviewToolButton("↑", 34)
        AddHandler btnUp.Click, Sub() ExecuteDrawingPreviewScript("if(window.externalScroll){window.externalScroll(0,-120);}")

        Dim btnDown = PreviewToolButton("↓", 34)
        AddHandler btnDown.Click, Sub() ExecuteDrawingPreviewScript("if(window.externalScroll){window.externalScroll(0,120);}")

        Dim btnLeft = PreviewToolButton("←", 34)
        AddHandler btnLeft.Click, Sub() ExecuteDrawingPreviewScript("if(window.externalScroll){window.externalScroll(-120,0);}")

        Dim btnRight = PreviewToolButton("→", 34)
        AddHandler btnRight.Click, Sub() ExecuteDrawingPreviewScript("if(window.externalScroll){window.externalScroll(120,0);}")

        Dim btnRotateLeft = PreviewToolButton("Sol 90", 62)
        AddHandler btnRotateLeft.Click, Sub() ExecuteDrawingPreviewScript("if(window.externalRotate){window.externalRotate(-90);}")

        Dim btnRotateRight = PreviewToolButton("Sağ 90", 62)
        AddHandler btnRotateRight.Click, Sub() ExecuteDrawingPreviewScript("if(window.externalRotate){window.externalRotate(90);}")

        tools.Controls.AddRange({btnFit, btnZoomOut, lblPreviewZoom, btnZoomIn, btnUp, btnDown, btnLeft, btnRight, btnRotateLeft, btnRotateRight})
        Return tools
    End Function

    Private Function PreviewToolButton(text As String, width As Integer) As Button
        Return New Button() With {
            .Text = text,
            .Width = width,
            .Height = 28,
            .Margin = New Padding(0, 0, 6, 4),
            .Cursor = Cursors.Hand,
            .FlatStyle = FlatStyle.System
        }
    End Function

    Private Sub ExecuteDrawingPreviewScript(script As String)
        Try
            If drawingViewer.CoreWebView2 Is Nothing Then Return
            drawingViewer.CoreWebView2.ExecuteScriptAsync(script)
        Catch ex As Exception
            ErrorLogService.Log("FrmCadDimensionPreview.ExecuteDrawingPreviewScript", ex)
        End Try
    End Sub

    Private Async Sub FrmCadDimensionPreview_Shown(sender As Object, e As EventArgs)
        ResponsiveFormService.FitSplitContainer(previewSplit, 0.52R, 300, 430)
        Await LoadDrawingPreviewAsync()
    End Sub

    Private Async Function LoadDrawingPreviewAsync() As Threading.Tasks.Task
        drawingPreviewReady = False

        Try
            Await drawingViewer.EnsureCoreWebView2Async()
            If Not drawingMessageHandlerAttached Then
                AddHandler drawingViewer.CoreWebView2.WebMessageReceived, AddressOf DrawingViewer_WebMessageReceived
                drawingMessageHandlerAttached = True
            End If

            If sourceDrawingPath = "" OrElse Not File.Exists(sourceDrawingPath) Then
                drawingViewer.NavigateToString(BuildMessageHtml("Çizim önizlemesi için kaynak dosya bulunamadı. Adayları listeden düzenleyebilirsiniz."))
                Return
            End If

            If Not String.Equals(Path.GetExtension(sourceDrawingPath), ".dxf", StringComparison.OrdinalIgnoreCase) Then
                drawingViewer.NavigateToString(BuildMessageHtml("Çizim önizlemesi şu anda DXF dosyaları için gösterilir. DWG adaylarını listeden kontrol edebilirsiniz."))
                Return
            End If

            Dim render = Await Threading.Tasks.Task.Run(Function() DxfRenderService.RenderToSvg(sourceDrawingPath))
            tempSvgPath = render.SvgPath
            tempHtmlPath = Path.Combine(AppPaths.TempDir, "cad_dimension_preview_" & Guid.NewGuid().ToString("N") & ".html")
            File.WriteAllText(tempHtmlPath, BuildDrawingPreviewHtml(New Uri(tempSvgPath).AbsoluteUri, render.AspectRatioText), New UTF8Encoding(False))
            drawingViewer.CoreWebView2.Navigate(New Uri(tempHtmlPath).AbsoluteUri)
        Catch ex As Exception
            ErrorLogService.Log("FrmCadDimensionPreview.LoadDrawingPreviewAsync", ex)
            Try
                drawingViewer.NavigateToString(BuildMessageHtml("Çizim önizlemesi açılamadı: " & ex.Message))
            Catch
            End Try
        End Try
    End Function

    Private Sub DrawingViewer_NavigationCompleted(sender As Object, e As CoreWebView2NavigationCompletedEventArgs)
        drawingPreviewReady = True
        UpdateDrawingMarkers()
    End Sub

    Private Sub DrawingViewer_WebMessageReceived(sender As Object, e As CoreWebView2WebMessageReceivedEventArgs)
        Try
            Dim msg = If(e.TryGetWebMessageAsString(), "")
            If msg.StartsWith("zoom|", StringComparison.OrdinalIgnoreCase) Then
                lblPreviewZoom.Text = msg.Substring(5).Trim() & "%"
            End If
        Catch ex As Exception
            ErrorLogService.Log("FrmCadDimensionPreview.DrawingViewer_WebMessageReceived", ex)
        End Try
    End Sub

    Private Sub UpdateDrawingMarkers()
        If Not drawingPreviewReady OrElse drawingViewer.CoreWebView2 Is Nothing Then Return

        Try
            Dim markers As New List(Of Dictionary(Of String, Object))()
            For Each row As DataGridViewRow In grid.Rows
                If Not row.Visible Then Continue For
                Dim isSelected = Convert.ToBoolean(If(row.Cells("Selected").Value, False))
                Dim xPercent As Decimal
                Dim yPercent As Decimal
                If Not NumberUtil.TryParseDecimal(Convert.ToString(row.Cells("XPercent").Value), xPercent) Then Continue For
                If Not NumberUtil.TryParseDecimal(Convert.ToString(row.Cells("YPercent").Value), yPercent) Then Continue For

                Dim measureId = Convert.ToString(row.Cells("MeasureId").Value).Trim()
                If measureId = "" Then measureId = (row.Index + 1).ToString()

                markers.Add(New Dictionary(Of String, Object) From {
                    {"n", row.Index + 1},
                    {"x", Math.Max(0D, Math.Min(100D, xPercent))},
                    {"y", Math.Max(0D, Math.Min(100D, yPercent))},
                    {"selected", isSelected},
                    {"title", measureId & " - " & Convert.ToString(row.Cells("MeasureName").Value)}
                })
            Next

            Dim json = JsonSerializer.Serialize(markers)
            drawingViewer.CoreWebView2.ExecuteScriptAsync("if(window.setMarkers){window.setMarkers(" & json & ");}")
        Catch ex As Exception
            ErrorLogService.Log("FrmCadDimensionPreview.UpdateDrawingMarkers", ex)
        End Try
    End Sub

    Private Function BuildDrawingPreviewHtml(svgUri As String, aspectRatioText As String) As String
        Return BuildInteractiveDrawingPreviewHtml(svgUri, aspectRatioText)

        Dim safeSvgUri = WebUtility.HtmlEncode(svgUri)
        Dim safeAspect = If(String.IsNullOrWhiteSpace(aspectRatioText), "1 / 1", aspectRatioText)

        Return "<!doctype html>" & vbCrLf &
               "<html><head><meta charset=""utf-8"">" & vbCrLf &
               "<style>" & vbCrLf &
               "html,body{margin:0;width:100%;height:100%;overflow:hidden;background:#f5f7fb;font-family:Segoe UI,Arial,sans-serif;color:#1f2937;}" & vbCrLf &
               "#toolbar{height:34px;display:flex;align-items:center;gap:8px;padding:0 10px;background:#eef4fb;border-bottom:1px solid #cbd5e1;font-size:12px;box-sizing:border-box;}" & vbCrLf &
               "#viewport{height:calc(100% - 34px);display:flex;align-items:center;justify-content:center;padding:10px;box-sizing:border-box;overflow:auto;}" & vbCrLf &
               "#canvas{position:relative;background:white;border:1px solid #cbd5e1;box-shadow:0 2px 10px rgba(15,23,42,.08);aspect-ratio:" & safeAspect & ";width:min(100%,calc((100vh - 70px) * 1.35));max-height:100%;}" & vbCrLf &
               "#drawing{display:block;width:100%;height:100%;object-fit:contain;}" & vbCrLf &
               "#overlay{position:absolute;left:0;top:0;right:0;bottom:0;pointer-events:none;}" & vbCrLf &
               ".marker{position:absolute;width:24px;height:24px;border-radius:50%;background:#d9153f;color:white;border:2px solid #8f0925;box-shadow:0 1px 4px rgba(0,0,0,.35);display:flex;align-items:center;justify-content:center;font-size:12px;font-weight:700;transform:translate(-50%,-50%);}" & vbCrLf &
               ".marker.off{background:#9ca3af;border-color:#6b7280;opacity:.55;}" & vbCrLf &
               ".hint{color:#475569;}" & vbCrLf &
               "</style></head><body>" & vbCrLf &
               "<div id=""toolbar""><b>Çizim önizlemesi</b><span class=""hint"">Balonlar aday konumunu gösterir; seçimi listeden açıp kapatabilirsiniz.</span></div>" & vbCrLf &
               "<div id=""viewport""><div id=""canvas""><img id=""drawing"" src=""" & safeSvgUri & """><div id=""overlay""></div></div></div>" & vbCrLf &
               "<script>" & vbCrLf &
               "window.setMarkers=function(markers){const overlay=document.getElementById('overlay');overlay.innerHTML='';(markers||[]).forEach(m=>{const d=document.createElement('div');d.className='marker'+(m.selected?'':' off');d.textContent=m.n;d.title=m.title||'';d.style.left=Number(m.x).toFixed(2)+'%';d.style.top=Number(m.y).toFixed(2)+'%';overlay.appendChild(d);});};" & vbCrLf &
               "</script></body></html>"
    End Function

    Private Function BuildInteractiveDrawingPreviewHtml(svgUri As String, aspectRatioText As String) As String
        Dim safeSvgUri = WebUtility.HtmlEncode(svgUri)
        Dim safeAspect = If(String.IsNullOrWhiteSpace(aspectRatioText), "1 / 1", aspectRatioText).Replace("'"c, " "c)

        Return "<!doctype html>" & vbCrLf &
               "<html><head><meta charset=""utf-8"">" & vbCrLf &
               "<style>" & vbCrLf &
               "html,body{margin:0;width:100%;height:100%;overflow:hidden;background:#f5f7fb;font-family:Segoe UI,Arial,sans-serif;color:#1f2937;}" & vbCrLf &
               "#viewport{position:relative;width:100%;height:100%;overflow:auto;background:#f8fafc;box-sizing:border-box;}" & vbCrLf &
               "#stage{position:relative;margin:10px auto;}" & vbCrLf &
               "#canvas{position:absolute;left:0;top:0;background:white;border:1px solid #cbd5e1;box-shadow:0 2px 10px rgba(15,23,42,.08);transform-origin:0 0;box-sizing:border-box;}" & vbCrLf &
               "#drawing{position:absolute;left:0;top:0;width:100%;height:100%;object-fit:contain;}" & vbCrLf &
               "#overlay{position:absolute;left:0;top:0;right:0;bottom:0;pointer-events:none;}" & vbCrLf &
               ".marker{position:absolute;width:24px;height:24px;border-radius:50%;background:#d9153f;color:white;border:2px solid #8f0925;box-shadow:0 1px 4px rgba(0,0,0,.35);display:flex;align-items:center;justify-content:center;font-size:12px;font-weight:700;transform:translate(-50%,-50%);}" & vbCrLf &
               ".marker.off{background:#9ca3af;border-color:#6b7280;opacity:.55;}" & vbCrLf &
               "</style></head><body>" & vbCrLf &
               "<div id=""viewport""><div id=""stage""><div id=""canvas""><img id=""drawing"" src=""" & safeSvgUri & """><div id=""overlay""></div></div></div></div>" & vbCrLf &
               "<script>" & vbCrLf &
               "const viewport=document.getElementById('viewport');const stage=document.getElementById('stage');const canvas=document.getElementById('canvas');const drawing=document.getElementById('drawing');const overlay=document.getElementById('overlay');" & vbCrLf &
               "const ap='" & safeAspect & "'.split('/').map(v=>parseFloat(v));let aspectW=(ap[0]&&ap[0]>0)?ap[0]:1;let aspectH=(ap[1]&&ap[1]>0)?ap[1]:1;let baseW=900,baseH=650,zoom=1,rotation=0;" & vbCrLf &
               "function notifyZoom(){if(window.chrome&&chrome.webview){chrome.webview.postMessage('zoom|'+Math.round(zoom*100));}}" & vbCrLf &
               "function visibleRatio(){const r=aspectW/aspectH;return (rotation%180===0)?r:(1/r);}" & vbCrLf &
               "function placeStage(){const sw=baseW*zoom,sh=baseH*zoom;const rotated=(rotation%180!==0);const boxW=rotated?sh:sw,boxH=rotated?sw:sh;stage.style.width=boxW+'px';stage.style.height=boxH+'px';canvas.style.width=sw+'px';canvas.style.height=sh+'px';let t='translate(0px,0px) rotate(0deg)';if(rotation===90){t='translate('+sh+'px,0px) rotate(90deg)';}else if(rotation===180){t='translate('+sw+'px,'+sh+'px) rotate(180deg)';}else if(rotation===270){t='translate(0px,'+sw+'px) rotate(270deg)';}canvas.style.transform=t;notifyZoom();}" & vbCrLf &
               "function fitStage(){const vw=Math.max(80,viewport.clientWidth-24),vh=Math.max(80,viewport.clientHeight-24);const vr=visibleRatio();let visW=vw,visH=visW/vr;if(visH>vh){visH=vh;visW=visH*vr;}if(rotation%180===0){baseW=visW;baseH=visH;}else{baseW=visH;baseH=visW;}zoom=1;placeStage();viewport.scrollLeft=0;viewport.scrollTop=0;}" & vbCrLf &
               "function applyZoom(newZoom,clientX,clientY){const old=zoom;const hasPoint=(typeof clientX==='number'&&typeof clientY==='number');const sx=viewport.scrollLeft,sy=viewport.scrollTop;zoom=Math.max(0.25,Math.min(6,newZoom));if(hasPoint){const rect=viewport.getBoundingClientRect();const px=(clientX-rect.left+viewport.scrollLeft)/old;const py=(clientY-rect.top+viewport.scrollTop)/old;placeStage();viewport.scrollLeft=px*zoom-(clientX-rect.left);viewport.scrollTop=py*zoom-(clientY-rect.top);}else{placeStage();viewport.scrollLeft=sx;viewport.scrollTop=sy;}}" & vbCrLf &
               "viewport.addEventListener('wheel',function(ev){if(ev.ctrlKey){ev.preventDefault();applyZoom(zoom*(ev.deltaY<0?1.12:0.89),ev.clientX,ev.clientY);}}, {passive:false});" & vbCrLf &
               "window.externalFit=function(){fitStage();};window.externalZoomBy=function(f){applyZoom(zoom*(f||1));};window.externalScroll=function(dx,dy){viewport.scrollLeft+=(dx||0);viewport.scrollTop+=(dy||0);};window.externalRotate=function(delta){rotation=(rotation+(delta||0)+360)%360;fitStage();};" & vbCrLf &
               "window.setMarkers=function(markers){overlay.innerHTML='';(markers||[]).forEach(m=>{const d=document.createElement('div');d.className='marker'+(m.selected?'':' off');d.textContent=m.n;d.title=m.title||'';d.style.left=Number(m.x).toFixed(2)+'%';d.style.top=Number(m.y).toFixed(2)+'%';overlay.appendChild(d);});};" & vbCrLf &
               "window.addEventListener('resize',fitStage);if(drawing.complete){setTimeout(fitStage,30);}else{drawing.onload=function(){fitStage();};}" & vbCrLf &
               "</script></body></html>"
    End Function

    Private Function BuildMessageHtml(message As String) As String
        Return "<!doctype html><html><head><meta charset=""utf-8"">" &
               "<style>html,body{height:100%;margin:0;background:#f8fafc;font-family:Segoe UI,Arial,sans-serif;color:#334155;}body{display:flex;align-items:center;justify-content:center;text-align:center;padding:24px;box-sizing:border-box}.box{max-width:520px;border:1px solid #cbd5e1;background:white;border-radius:10px;padding:22px;box-shadow:0 2px 10px rgba(15,23,42,.08)}</style>" &
               "</head><body><div class=""box"">" & WebUtility.HtmlEncode(message) & "</div></body></html>"
    End Function

    Private Function TextColumn(name As String, header As String, width As Integer, Optional readOnlyColumn As Boolean = False) As DataGridViewTextBoxColumn
        Return New DataGridViewTextBoxColumn() With {
            .Name = name,
            .HeaderText = header,
            .Width = width,
            .ReadOnly = readOnlyColumn,
            .SortMode = DataGridViewColumnSortMode.Automatic
        }
    End Function

    Private Sub LoadRows()
        For Each candidate In candidates
            Dim rowIndex = grid.Rows.Add(
                candidate.IsSelected,
                candidate.LayerName,
                candidate.SuggestedMeasureId,
                candidate.MeasureName,
                NumberUtil.DecToCsv(candidate.Nominal),
                NumberUtil.DecToCsv(candidate.LowerTolerance),
                NumberUtil.DecToCsv(candidate.UpperTolerance),
                candidate.Unit,
                candidate.PageNo.ToString(),
                NumberUtil.DecToCsv(candidate.XPercent),
                NumberUtil.DecToCsv(candidate.YPercent),
                candidate.LayoutName,
                candidate.DimensionType,
                candidate.WarningText)
            grid.Rows(rowIndex).Tag = candidate
            If candidate.WarningText <> "" Then
                grid.Rows(rowIndex).DefaultCellStyle.BackColor = Color.LightYellow
            End If
        Next
        UpdateSummary()
    End Sub

    Private Sub PopulateLayerFilter()
        layerFilterUpdating = True
        Try
            layerFilterList.Items.Clear()
            Dim layers = candidates.
                Select(Function(candidate) DisplayLayerName(candidate.LayerName)).
                Where(Function(layer) layer <> "").
                Distinct(StringComparer.OrdinalIgnoreCase).
                OrderBy(Function(layer) layer, StringComparer.OrdinalIgnoreCase).
                ToList()

            For Each layer In layers
                layerFilterList.Items.Add(layer, True)
            Next
        Finally
            layerFilterUpdating = False
        End Try

        ApplyLayerSelection()
    End Sub

    Private Shared Function DisplayLayerName(layerName As String) As String
        Dim value = If(layerName, "").Trim()
        If value = "" Then Return "(boş layer)"
        Return value
    End Function

    Private Sub SetAllLayerChecks(value As Boolean)
        layerFilterUpdating = True
        Try
            For i As Integer = 0 To layerFilterList.Items.Count - 1
                layerFilterList.SetItemChecked(i, value)
            Next
        Finally
            layerFilterUpdating = False
        End Try

        ApplyLayerSelection()
    End Sub

    Private Sub ApplyLayerSelection()
        If layerFilterUpdating Then Return

        Dim checkedLayers As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each item In layerFilterList.CheckedItems
            checkedLayers.Add(Convert.ToString(item))
        Next

        Try
            grid.ClearSelection()
            grid.CurrentCell = Nothing
        Catch
        End Try

        For Each row As DataGridViewRow In grid.Rows
            Dim layerName = DisplayLayerName(Convert.ToString(row.Cells("Layer").Value))
            Dim isIncluded = checkedLayers.Contains(layerName)
            row.Visible = isIncluded
            row.Cells("Selected").Value = isIncluded
        Next

        UpdateSummary()
        UpdateDrawingMarkers()
    End Sub

    Private Sub SetAllSelected(value As Boolean)
        For Each row As DataGridViewRow In grid.Rows
            If Not row.Visible Then Continue For
            row.Cells("Selected").Value = value
        Next
        UpdateSummary()
        UpdateDrawingMarkers()
    End Sub

    Private Sub UpdateSummary()
        Dim visibleCount = grid.Rows.Cast(Of DataGridViewRow)().
            Count(Function(row) row.Visible)
        Dim selectedCount = grid.Rows.Cast(Of DataGridViewRow)().
            Where(Function(row) row.Visible).
            Count(Function(row) Convert.ToBoolean(If(row.Cells("Selected").Value, False)))
        Dim filePrefix = lblSummary.Text.Split("|"c)(0).Trim()
        lblSummary.Text = filePrefix & " | Aday: " & visibleCount.ToString() & "/" & grid.Rows.Count.ToString() & " | Secili: " & selectedCount.ToString()
        Return
        lblSummary.Text = $"{filePrefix} | Aday: {grid.Rows.Count} | İşaretli: {selectedCount}"
    End Sub

    Private Sub Import_Click(sender As Object, e As EventArgs)
        Dim selected = ReadCandidatesFromGrid(True)
        If selected.Count = 0 Then
            MessageBox.Show("Aktarılacak en az bir ölçü seçiniz.", "Ölçü seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        DialogResult = DialogResult.OK
        Close()
    End Sub

    Private Function ReadCandidatesFromGrid(selectedOnly As Boolean) As List(Of CadDimensionCandidate)
        Dim result As New List(Of CadDimensionCandidate)()
        Dim usedIds As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        For Each row As DataGridViewRow In grid.Rows
            If selectedOnly AndAlso Not row.Visible Then Continue For
            Dim isSelected = Convert.ToBoolean(If(row.Cells("Selected").Value, False))
            If selectedOnly AndAlso Not isSelected Then Continue For

            Dim candidate = TryCast(row.Tag, CadDimensionCandidate)
            If candidate Is Nothing Then Continue For

            Dim measureId = Convert.ToString(row.Cells("MeasureId").Value).Trim()
            Dim measureName = Convert.ToString(row.Cells("MeasureName").Value).Trim()
            Dim unitText = Convert.ToString(row.Cells("Unit").Value).Trim()
            Dim nominal As Decimal
            Dim lowerTolerance As Decimal
            Dim upperTolerance As Decimal
            Dim xPercent As Decimal
            Dim yPercent As Decimal
            Dim pageNo As Integer

            If measureId = "" OrElse Not usedIds.Add(measureId) Then
                Throw New InvalidOperationException("Ölçü numaraları boş veya mükerrer olamaz: " & measureId)
            End If
            If Not NumberUtil.TryParseDecimal(Convert.ToString(row.Cells("Nominal").Value), nominal) OrElse
               Not NumberUtil.TryParseDecimal(Convert.ToString(row.Cells("LowerTolerance").Value), lowerTolerance) OrElse
               Not NumberUtil.TryParseDecimal(Convert.ToString(row.Cells("UpperTolerance").Value), upperTolerance) OrElse
               Not NumberUtil.TryParseDecimal(Convert.ToString(row.Cells("XPercent").Value), xPercent) OrElse
               Not NumberUtil.TryParseDecimal(Convert.ToString(row.Cells("YPercent").Value), yPercent) Then
                Throw New InvalidOperationException("Nominal, tolerans ve X/Y alanları sayısal olmalıdır. Ölçü: " & measureId)
            End If
            If Not Integer.TryParse(Convert.ToString(row.Cells("PageNo").Value), pageNo) OrElse pageNo < 1 Then
                Throw New InvalidOperationException("PDF sayfa numarası en az 1 olmalıdır. Ölçü: " & measureId)
            End If
            If xPercent < 0D OrElse xPercent > 100D OrElse yPercent < 0D OrElse yPercent > 100D Then
                Throw New InvalidOperationException("X/Y yüzdeleri 0-100 arasında olmalıdır. Ölçü: " & measureId)
            End If

            candidate.IsSelected = isSelected
            candidate.SuggestedMeasureId = measureId
            candidate.MeasureName = measureName
            candidate.Nominal = nominal
            candidate.LowerTolerance = -Math.Abs(lowerTolerance)
            candidate.UpperTolerance = Math.Abs(upperTolerance)
            candidate.Unit = If(unitText = "", "mm", unitText)
            candidate.PageNo = pageNo
            candidate.XPercent = xPercent
            candidate.YPercent = yPercent
            result.Add(candidate)
        Next

        Return result
    End Function

    Private Sub FrmCadDimensionPreview_FormClosed(sender As Object, e As FormClosedEventArgs)
        Try
            drawingViewer.Dispose()
        Catch
        End Try

        Try
            If tempHtmlPath <> "" AndAlso File.Exists(tempHtmlPath) Then File.Delete(tempHtmlPath)
        Catch ex As Exception
            ErrorLogService.Log("FrmCadDimensionPreview.DeleteTempHtml", ex)
        End Try

        Try
            If tempSvgPath <> "" AndAlso File.Exists(tempSvgPath) Then File.Delete(tempSvgPath)
        Catch ex As Exception
            ErrorLogService.Log("FrmCadDimensionPreview.DeleteTempSvg", ex)
        End Try
    End Sub
End Class
