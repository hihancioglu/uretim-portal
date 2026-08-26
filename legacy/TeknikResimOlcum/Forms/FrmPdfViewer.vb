Imports System.Diagnostics
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.IO
Imports System.Net
Imports System.Text
Imports System.Windows.Forms
Imports Microsoft.Web.WebView2.WinForms

Public Class FrmPdfViewer
    Inherits Form

    Private ReadOnly viewerPanel As New Panel()
    Private ReadOnly picture As New PictureBox()
    Private ReadOnly webViewer As New WebView2()
    Private ReadOnly lblInfo As New Label()
    Private ReadOnly lblZoomValue As New Label()
    Private ReadOnly drawingFileName As String
    Private ReadOnly showExternalButton As Boolean

    Private tempPdf As String = ""
    Private tempPng As String = ""
    Private tempHtml As String = ""
    Private originalImage As Image = Nothing
    Private shownImage As Image = Nothing
    Private currentPdfZoom As Integer = 100
    Private currentRotation As Integer = 0
    Private isDxfViewer As Boolean = False

    Public Sub New(encDrawingFileName As String, titleText As String, Optional showExternalButton As Boolean = True)
        AppIconService.Apply(Me)
        drawingFileName = encDrawingFileName
        Me.showExternalButton = showExternalButton
        Text = titleText
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        MinimumSize = New Size(640, 480)
        BackColor = Color.White

        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .BackColor = Color.White
        }
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 44.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Controls.Add(layout)

        Dim toolbar As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(8, 6, 8, 4),
            .BackColor = SystemColors.Control,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .AutoScroll = True
        }
        layout.Controls.Add(toolbar, 0, 0)

        Dim btnFit As New Button() With {.Text = "Fit", .Width = 58, .Height = 30, .Margin = New Padding(0, 0, 6, 5)}
        AddHandler btnFit.Click, AddressOf Fit_Click

        Dim btnZoomOut As New Button() With {.Text = "-", .Width = 34, .Height = 30, .Margin = New Padding(0, 0, 4, 5)}
        AddHandler btnZoomOut.Click, Sub() SetZoom(currentPdfZoom - 10)

        lblZoomValue.AutoSize = False
        lblZoomValue.Width = 52
        lblZoomValue.Height = 30
        lblZoomValue.TextAlign = ContentAlignment.MiddleCenter
        lblZoomValue.Margin = New Padding(0, 0, 4, 5)
        UpdateZoomLabel()

        Dim btnZoomIn As New Button() With {.Text = "+", .Width = 34, .Height = 30, .Margin = New Padding(0, 0, 10, 5)}
        AddHandler btnZoomIn.Click, Sub() SetZoom(currentPdfZoom + 10)

        Dim btnUp As New Button() With {.Text = "↑", .Width = 34, .Height = 30, .Margin = New Padding(0, 0, 4, 5)}
        AddHandler btnUp.Click, Sub() ScrollViewer(0, -160)

        Dim btnDown As New Button() With {.Text = "↓", .Width = 34, .Height = 30, .Margin = New Padding(0, 0, 4, 5)}
        AddHandler btnDown.Click, Sub() ScrollViewer(0, 160)

        Dim btnLeft As New Button() With {.Text = "←", .Width = 34, .Height = 30, .Margin = New Padding(0, 0, 4, 5)}
        AddHandler btnLeft.Click, Sub() ScrollViewer(-160, 0)

        Dim btnRight As New Button() With {.Text = "→", .Width = 34, .Height = 30, .Margin = New Padding(0, 0, 8, 5)}
        AddHandler btnRight.Click, Sub() ScrollViewer(160, 0)

        Dim btnRotateLeft As New Button() With {.Text = "Sol 90", .Width = 62, .Height = 30, .Margin = New Padding(0, 0, 4, 5)}
        AddHandler btnRotateLeft.Click, Sub() RotateView(-90)

        Dim btnRotateRight As New Button() With {.Text = "Sağ 90", .Width = 62, .Height = 30, .Margin = New Padding(0, 0, 10, 5)}
        AddHandler btnRotateRight.Click, Sub() RotateView(90)

        lblInfo.AutoSize = False
        lblInfo.Width = 700
        lblInfo.Height = 30
        lblInfo.TextAlign = ContentAlignment.MiddleLeft
        lblInfo.Margin = New Padding(8, 0, 0, 5)
        lblInfo.Text = "Teknik resim açılıyor..."

        toolbar.Controls.AddRange({btnFit, btnZoomOut, lblZoomValue, btnZoomIn, btnUp, btnDown, btnLeft, btnRight, btnRotateLeft, btnRotateRight})

        If showExternalButton Then
            Dim btnExternal As New Button() With {.Text = "Harici Aç", .Width = 90, .Height = 30, .Margin = New Padding(8, 0, 0, 5)}
            AddHandler btnExternal.Click, AddressOf OpenExternal_Click
            toolbar.Controls.Add(btnExternal)
        End If

        toolbar.Controls.Add(lblInfo)

        viewerPanel.Dock = DockStyle.Fill
        viewerPanel.AutoScroll = True
        viewerPanel.BackColor = Color.White
        viewerPanel.BorderStyle = BorderStyle.FixedSingle
        layout.Controls.Add(viewerPanel, 0, 1)

        picture.SizeMode = PictureBoxSizeMode.StretchImage
        picture.BackColor = Color.White
        viewerPanel.Controls.Add(picture)

        webViewer.Dock = DockStyle.Fill
        webViewer.Visible = False
        viewerPanel.Controls.Add(webViewer)

        AddHandler Shown, AddressOf FrmPdfViewer_Shown
        AddHandler FormClosed, AddressOf FrmPdfViewer_FormClosed
        AddHandler viewerPanel.Resize, Sub()
                                           If originalImage IsNot Nothing AndAlso currentPdfZoom <= 0 Then FitToScreen()
                                           CenterPictureIfNeeded()
                                       End Sub
    End Sub

    Private Async Sub FrmPdfViewer_Shown(sender As Object, e As EventArgs)
        Try
            If Not TempFileService.IsEncryptedPdf(drawingFileName) Then
                If Not TempFileService.IsEncryptedDxf(drawingFileName) Then
                    Throw New InvalidDataException("Desteklenmeyen teknik resim türü: " & drawingFileName)
                End If

                isDxfViewer = True
                picture.Visible = False
                webViewer.Visible = True

                tempPdf = TempFileService.DecryptEncryptedDrawingToTemp(drawingFileName)
                Dim render = DxfRenderService.RenderToSvg(tempPdf)
                tempPng = render.SvgPath
                tempHtml = Path.Combine(AppPaths.TempDir, "dxf_viewer_" & Guid.NewGuid().ToString("N") & ".html")
                File.WriteAllText(tempHtml, BuildDxfViewerHtml(New Uri(tempPng).AbsoluteUri), New UTF8Encoding(False))

                Await webViewer.EnsureCoreWebView2Async()
                webViewer.Source = New Uri(tempHtml)
                lblInfo.Text = "DXF teknik resim: " & drawingFileName
                AuditService.Log("DXF_VIEW_INTERNAL", "", "", "Program içinde DXF görüntüleyici ile açıldı: " & drawingFileName)
                Return
            End If

            isDxfViewer = False
            webViewer.Visible = False
            picture.Visible = True

            tempPdf = TempFileService.DecryptEncryptedPdfToTemp(drawingFileName)
            tempPng = PdfRenderService.RenderFirstPageToPng(tempPdf)

            originalImage = Image.FromFile(tempPng)
            currentRotation = 0
            FitToScreen()

            lblInfo.Text = "Teknik resim: " & drawingFileName
            AuditService.Log("PDF_VIEW_INTERNAL", "", "", "Program içinde WinForms görüntüleyici ile açıldı: " & drawingFileName)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "PDF açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub Fit_Click(sender As Object, e As EventArgs)
        FitToScreen()
    End Sub

    Private Sub FitToScreen()
        If isDxfViewer Then
            ExecuteDxfScript("if(window.externalFit){window.externalFit();}")
            Return
        End If

        If originalImage Is Nothing Then Return

        Dim baseSize = GetRotatedBaseSize()
        Dim availableW = Math.Max(1, viewerPanel.ClientSize.Width - 24)
        Dim availableH = Math.Max(1, viewerPanel.ClientSize.Height - 24)

        Dim z = CInt(Math.Floor(Math.Min(CDbl(availableW) / CDbl(baseSize.Width), CDbl(availableH) / CDbl(baseSize.Height)) * 100.0R))
        If z < 10 Then z = 10
        If z > 400 Then z = 400

        SetZoom(z)
    End Sub

    Private Function GetRotatedBaseSize() As Size
        If originalImage Is Nothing Then Return New Size(1, 1)

        If currentRotation = 90 OrElse currentRotation = 270 Then
            Return New Size(originalImage.Height, originalImage.Width)
        End If

        Return New Size(originalImage.Width, originalImage.Height)
    End Function

    Private Sub SetZoom(zoomValue As Integer)
        If isDxfViewer Then
            currentPdfZoom = Math.Max(10, Math.Min(400, zoomValue))
            UpdateZoomLabel()
            ExecuteDxfScript("if(window.externalSetZoom){window.externalSetZoom(" & currentPdfZoom.ToString(Globalization.CultureInfo.InvariantCulture) & ");}")
            Return
        End If

        If originalImage Is Nothing Then Return

        currentPdfZoom = Math.Max(10, Math.Min(400, zoomValue))
        UpdateZoomLabel()
        ApplyImageToPicture()
    End Sub

    Private Sub UpdateZoomLabel()
        lblZoomValue.Text = currentPdfZoom.ToString() & "%"
    End Sub

    Private Sub RotateView(delta As Integer)
        If isDxfViewer Then
            currentRotation = (currentRotation + delta + 360) Mod 360
            ExecuteDxfScript("if(window.externalRotate){window.externalRotate(" & delta.ToString(Globalization.CultureInfo.InvariantCulture) & ");}")
            Return
        End If

        If originalImage Is Nothing Then Return

        currentRotation = (currentRotation + delta + 360) Mod 360
        FitToScreen()
    End Sub

    Private Sub ApplyImageToPicture()
        If originalImage Is Nothing Then Return

        Dim oldImage = shownImage
        shownImage = CreateRotatedImage(originalImage, currentRotation)
        picture.Image = shownImage

        If oldImage IsNot Nothing AndAlso Not Object.ReferenceEquals(oldImage, originalImage) Then
            oldImage.Dispose()
        End If

        Dim scaledW = Math.Max(1, CInt(Math.Round(shownImage.Width * currentPdfZoom / 100.0R)))
        Dim scaledH = Math.Max(1, CInt(Math.Round(shownImage.Height * currentPdfZoom / 100.0R)))

        viewerPanel.AutoScrollMinSize = New Size(scaledW, scaledH)
        picture.Size = New Size(scaledW, scaledH)
        CenterPictureIfNeeded()
    End Sub

    Private Function CreateRotatedImage(source As Image, rotation As Integer) As Image
        Dim bmp As New Bitmap(source)

        If rotation = 90 Then
            bmp.RotateFlip(RotateFlipType.Rotate90FlipNone)
        ElseIf rotation = 180 Then
            bmp.RotateFlip(RotateFlipType.Rotate180FlipNone)
        ElseIf rotation = 270 Then
            bmp.RotateFlip(RotateFlipType.Rotate270FlipNone)
        End If

        Return bmp
    End Function

    Private Sub CenterPictureIfNeeded()
        If picture.Image Is Nothing Then Return

        Dim x = Math.Max(0, (viewerPanel.ClientSize.Width - picture.Width) \ 2)
        Dim y = Math.Max(0, (viewerPanel.ClientSize.Height - picture.Height) \ 2)

        If picture.Width >= viewerPanel.ClientSize.Width Then x = 0
        If picture.Height >= viewerPanel.ClientSize.Height Then y = 0

        picture.Location = New Point(x, y)
    End Sub

    Private Sub ScrollViewer(dx As Integer, dy As Integer)
        If isDxfViewer Then
            ExecuteDxfScript("if(window.externalScroll){window.externalScroll(" & dx.ToString(Globalization.CultureInfo.InvariantCulture) & "," & dy.ToString(Globalization.CultureInfo.InvariantCulture) & ");}")
            Return
        End If

        Dim newX = Math.Max(0, -viewerPanel.AutoScrollPosition.X + dx)
        Dim newY = Math.Max(0, -viewerPanel.AutoScrollPosition.Y + dy)
        viewerPanel.AutoScrollPosition = New Point(newX, newY)
    End Sub

    Private Sub ExecuteDxfScript(script As String)
        Try
            If webViewer.CoreWebView2 IsNot Nothing Then
                webViewer.ExecuteScriptAsync(script)
            End If
        Catch ex As Exception
            ErrorLogService.Log("FrmPdfViewer.ExecuteDxfScript", ex)
        End Try
    End Sub

    Private Sub OpenExternal_Click(sender As Object, e As EventArgs)
        Try
            If String.IsNullOrWhiteSpace(tempPdf) OrElse Not File.Exists(tempPdf) Then Return
            Dim psi As New ProcessStartInfo(tempPdf) With {.UseShellExecute = True}
            Process.Start(psi)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Teknik resim açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Function BuildDxfViewerHtml(imageUri As String) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>")
        sb.AppendLine("<style>")
        sb.AppendLine("html,body{margin:0;width:100%;height:100%;overflow:hidden;background:#fff;font-family:Segoe UI,Arial;}")
        sb.AppendLine("#viewport{position:relative;width:100%;height:100%;overflow:auto;background:#fff;}")
        sb.AppendLine("#stage{position:absolute;left:0;top:0;background:#fff;}")
        sb.AppendLine("#content{position:absolute;left:0;top:0;transform-origin:top left;}")
        sb.AppendLine("#drawing{position:absolute;left:0;top:0;display:block;user-select:none;-webkit-user-drag:none;}")
        sb.AppendLine("</style></head><body>")
        sb.AppendLine("<div id='viewport'><div id='stage'><div id='content'><img id='drawing' src='" & WebUtility.HtmlEncode(imageUri) & "'/></div></div></div>")
        sb.AppendLine("<script>")
        sb.AppendLine("const viewport=document.getElementById('viewport'); const stage=document.getElementById('stage'); const content=document.getElementById('content'); const img=document.getElementById('drawing');")
        sb.AppendLine("let zoom=100; let rotation=0;")
        sb.AppendLine("function naturalW(){return img.naturalWidth||1;} function naturalH(){return img.naturalHeight||1;}")
        sb.AppendLine("function rotatedBaseSize(){const w=naturalW(),h=naturalH(); return (rotation%180===0)?{w:w,h:h}:{w:h,h:w};}")
        sb.AppendLine("function applyTransform(){const scaledW=naturalW()*(zoom/100.0); const scaledH=naturalH()*(zoom/100.0); let stageW=scaledW,stageH=scaledH,transform='translate(0px,0px) rotate(0deg)';")
        sb.AppendLine("if(rotation===90){stageW=scaledH;stageH=scaledW;transform='translate('+scaledH+'px,0px) rotate(90deg)';} else if(rotation===180){transform='translate('+scaledW+'px,'+scaledH+'px) rotate(180deg)';} else if(rotation===270){stageW=scaledH;stageH=scaledW;transform='translate(0px,'+scaledW+'px) rotate(270deg)';}")
        sb.AppendLine("content.style.width=scaledW+'px'; content.style.height=scaledH+'px'; content.style.transform=transform; img.style.width=scaledW+'px'; img.style.height=scaledH+'px'; stage.style.width=stageW+'px'; stage.style.height=stageH+'px'; stage.style.left=Math.max(0,(viewport.clientWidth-stageW)/2)+'px'; stage.style.top=Math.max(0,(viewport.clientHeight-stageH)/2)+'px';}")
        sb.AppendLine("function fitStage(){const base=rotatedBaseSize(); const vw=Math.max(1,viewport.clientWidth-8); const vh=Math.max(1,viewport.clientHeight-8); zoom=Math.max(10,Math.min(400,Math.floor(Math.min(vw/base.w,vh/base.h)*100))); applyTransform();}")
        sb.AppendLine("window.externalSetZoom=function(z){zoom=Math.max(10,Math.min(400,z||100)); applyTransform();};")
        sb.AppendLine("window.externalFit=function(){fitStage();}; window.externalScroll=function(dx,dy){viewport.scrollLeft+=dx; viewport.scrollTop+=dy;}; window.externalRotate=function(delta){rotation=(rotation+delta+360)%360; fitStage();};")
        sb.AppendLine("window.addEventListener('resize', fitStage); img.onload=fitStage;")
        sb.AppendLine("</script></body></html>")
        Return sb.ToString()
    End Function

    Private Sub FrmPdfViewer_FormClosed(sender As Object, e As FormClosedEventArgs)
        Try
            picture.Image = Nothing

            If shownImage IsNot Nothing Then
                shownImage.Dispose()
                shownImage = Nothing
            End If

            If originalImage IsNot Nothing Then
                originalImage.Dispose()
                originalImage = Nothing
            End If
        Catch ex As Exception
            ErrorLogService.Log("FrmPdfViewer.FormClosed.DisposeImage", ex)
        End Try

        TempFileService.TryDeleteTempPdf(tempPdf)
        TempFileService.TryDeleteTempPdf(tempPng)
        TempFileService.TryDeleteTempPdf(tempHtml)
        webViewer.Dispose()
    End Sub
End Class
