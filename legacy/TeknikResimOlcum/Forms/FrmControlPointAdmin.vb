Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Net
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Windows.Forms
Imports Microsoft.Web.WebView2.Core
Imports Microsoft.Web.WebView2.WinForms

Public Class FrmControlPointAdmin
    Inherits Form

    Private ReadOnly cboProduct As New ComboBox()
    Private ReadOnly txtProductFilter As New TextBox()
    Private allProducts As New List(Of ProductInfo)()
    Private ReadOnly grid As New DataGridView()
    Private ReadOnly lblPointCount As New Label()
    Private ReadOnly txtId As New TextBox()
    Private ReadOnly txtName As New TextBox()
    Private ReadOnly txtNominal As New TextBox()
    Private ReadOnly txtLowerTol As New TextBox()
    Private ReadOnly txtUpperTol As New TextBox()
    Private ReadOnly txtUnit As New TextBox()
    Private ReadOnly txtSort As New TextBox()
    Private ReadOnly txtPage As New TextBox()
    Private ReadOnly txtX As New TextBox()
    Private ReadOnly txtY As New TextBox()
    Private ReadOnly cboMeasurementGroup As New ComboBox()
    Private ReadOnly cboSampleFrequency As New ComboBox()
    Private ReadOnly txtMetaTrCode As New TextBox()
    Private ReadOnly txtMetaProductName As New TextBox()
    Private ReadOnly txtMetaPlasticCode As New TextBox()
    Private ReadOnly txtMetaMaterial As New TextBox()
    Private ReadOnly txtMetaColorName As New TextBox()
    Private ReadOnly txtMetaMoldCavityCount As New TextBox()
    Private ReadOnly txtMetaMoldCode As New TextBox()
    Private ReadOnly btnIncompleteProducts As New Button()
    Private ReadOnly chkMandatory As New CheckBox()
    Private ReadOnly chkCritical As New CheckBox()
    Private ReadOnly chkCapture As New CheckBox()
    Private ReadOnly btnGroupAreaCapture As New Button()
    Private ReadOnly pdfViewer As New WebView2()
    Private ReadOnly lblPdfInfo As New Label()
    Private currentTempPdf As String = ""
    Private currentTempPng As String = ""
    Private currentTempHtml As String = ""
    Private webMessageHooked As Boolean = False
    Private navigationHooked As Boolean = False
    Private pendingHighlightMeasureId As String = ""
    Private currentPdfZoom As Integer = 100
    Private ReadOnly lblZoomText As New Label()
    Private groupAreaCaptureEnabled As Boolean = False

    Public Sub New()
        AuthorizationService.Require(AppState.CanViewTechnicalDrawingAdmin, "Kontrol Olculeri")
        AppIconService.Apply(Me)
        Text = "Kontrol Ölçüleri - PDF Üzerinden Tanımlama"
        If Not AppState.CanOpenTechnicalDrawingAdmin Then Text &= " - Salt Okunur"
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        Size = New Size(1450, 820)
        MinimumSize = New Size(760, 560)

        Dim split As New SplitContainer() With {
            .Dock = DockStyle.Fill,
            .Orientation = Orientation.Vertical,
            .SplitterWidth = 6
        }
        Controls.Add(split)
        AddHandler Shown, Sub() ResponsiveFormService.FitSplitContainer(split, 0.54R, 220, 360)

        BuildPdfPanel(split.Panel1)
        BuildRightPanel(split.Panel2)

        AddHandler FormClosed, AddressOf FrmControlPointAdmin_FormClosed
        LoadProducts()
    End Sub

    Private Sub BuildPdfPanel(parent As Control)
        parent.BackColor = Color.White
        pdfViewer.Dock = DockStyle.Fill
        parent.Controls.Add(pdfViewer)
    End Sub

    Private Sub BuildRightPanel(parent As Control)
        parent.BackColor = Color.White

        Dim rightLayout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 4,
            .BackColor = Color.White
        }
        rightLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 122.0F))
        rightLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 510.0F))
        rightLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
        rightLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        parent.Controls.Add(rightLayout)

        Dim pdfToolbar As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .BackColor = SystemColors.Control,
            .ColumnCount = 1,
            .RowCount = 3,
            .Padding = New Padding(8, 5, 8, 3)
        }
        pdfToolbar.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        pdfToolbar.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))
        pdfToolbar.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        rightLayout.Controls.Add(pdfToolbar, 0, 0)

        Dim pdfTools As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .AutoScroll = True,
            .Margin = New Padding(0),
            .BackColor = SystemColors.Control
        }
        pdfToolbar.Controls.Add(pdfTools, 0, 0)

        Dim pdfLocationTools As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .AutoScroll = False,
            .Margin = New Padding(0),
            .BackColor = SystemColors.Control
        }
        pdfToolbar.Controls.Add(pdfLocationTools, 0, 1)

        Dim btnLoadPdf As New Button() With {.Text = "Resim Göster", .Width = 105, .Height = 30, .Margin = New Padding(0, 0, 6, 5)}
        AddHandler btnLoadPdf.Click, AddressOf LoadPdf_Click

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

        lblZoomText.Text = "100%"
        lblZoomText.TextAlign = ContentAlignment.MiddleCenter
        lblZoomText.Width = 52
        lblZoomText.Height = 30
        lblZoomText.Margin = New Padding(0, 0, 4, 5)

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

        Dim btnRotateLeft As New Button() With {.Text = "Sol 90", .Width = 58, .Height = 30, .Margin = New Padding(0, 0, 4, 5)}
        AddHandler btnRotateLeft.Click, Sub()
                                            currentPdfZoom = 100
                                            UpdateZoomLabel()
                                            ExecutePdfScript("if(window.externalRotate){window.externalRotate(-90);}")
                                        End Sub

        Dim btnRotateRight As New Button() With {.Text = "Sağ 90", .Width = 58, .Height = 30, .Margin = New Padding(0, 0, 12, 5)}
        AddHandler btnRotateRight.Click, Sub()
                                             currentPdfZoom = 100
                                             UpdateZoomLabel()
                                             ExecutePdfScript("if(window.externalRotate){window.externalRotate(90);}")
                                         End Sub

        chkCapture.Text = "X/Y Yakala"
        chkCapture.Width = 95
        chkCapture.Height = 30
        chkCapture.Margin = New Padding(0, 2, 8, 5)

        Dim btnClearPos As New Button() With {.Text = "Konumu Temizle", .Width = 120, .Height = 30, .Margin = New Padding(0, 2, 6, 2)}
        AddHandler btnClearPos.Click, Sub()
                                          txtX.Clear()
                                          txtY.Clear()
                                      End Sub

        lblPdfInfo.Text = "X/Y Yakala açıkken teknik resme tıklayın; konum otomatik alınır."
        lblPdfInfo.Dock = DockStyle.Fill
        lblPdfInfo.AutoEllipsis = True
        lblPdfInfo.TextAlign = ContentAlignment.MiddleLeft
        lblPdfInfo.Margin = New Padding(2, 0, 2, 0)

        pdfTools.Controls.AddRange({btnLoadPdf, btnFit, btnZoomOut, lblZoomText, btnZoomIn, btnUp, btnDown, btnLeft, btnRight, btnRotateLeft, btnRotateRight, chkCapture})
        pdfLocationTools.Controls.Add(btnClearPos)
        If Not AppState.CanOpenTechnicalDrawingAdmin Then
            chkCapture.Visible = False
            btnClearPos.Visible = False
        End If
        pdfToolbar.Controls.Add(lblPdfInfo, 0, 2)
        AddHandler chkCapture.CheckedChanged, AddressOf Capture_CheckedChanged

        Dim top As New Panel() With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(12),
            .BackColor = SystemColors.Control,
            .AutoScroll = True,
            .AutoScrollMinSize = New Size(720, 500)
        }
        rightLayout.Controls.Add(top, 0, 1)

        AddLabel(top, "TR / Revizyon", 15, 15)
        cboProduct.SetBounds(125, 12, 300, 25)
        cboProduct.DropDownStyle = ComboBoxStyle.DropDownList
        AddHandler cboProduct.SelectedIndexChanged, Sub()
                                                       LoadProductMetadata()
                                                       LoadGrid()
                                                       ClearInputs()
                                                       LoadPdfToViewer(False)
                                                   End Sub

        AddLabel(top, "TR Filtre", 440, 15)
        txtProductFilter.SetBounds(505, 12, 150, 25)
        txtProductFilter.PlaceholderText = "TR / revizyon / ürün"
        AddHandler txtProductFilter.TextChanged, Sub() ApplyProductFilter()

        AddLabel(top, "TR Kodu", 15, 50)
        txtMetaTrCode.SetBounds(125, 47, 180, 25)
        txtMetaTrCode.ReadOnly = True
        txtMetaTrCode.BackColor = Color.WhiteSmoke

        AddLabel(top, "Ürün Adı", 320, 50)
        txtMetaProductName.SetBounds(400, 47, 230, 25)

        AddLabel(top, "Plastik Kodu", 15, 85)
        txtMetaPlasticCode.SetBounds(125, 82, 180, 25)

        AddLabel(top, "Malzeme", 320, 85)
        txtMetaMaterial.SetBounds(400, 82, 230, 25)

        AddLabel(top, "Renk", 15, 120)
        txtMetaColorName.SetBounds(125, 117, 180, 25)

        AddLabel(top, "Kalıp Göz Adedi", 320, 120)
        txtMetaMoldCavityCount.SetBounds(430, 117, 90, 25)

        AddLabel(top, "Kalıp Kodu", 15, 155)
        txtMetaMoldCode.SetBounds(125, 152, 180, 25)

        Dim btnMetaSave As New Button() With {.Text = "Ürün Bilgilerini Kaydet", .Left = 320, .Top = 150, .Width = 180, .Height = 30}
        AddHandler btnMetaSave.Click, AddressOf SaveProductMetadata_Click

        btnIncompleteProducts.SetBounds(510, 150, 145, 30)
        AddHandler btnIncompleteProducts.Click, AddressOf ShowIncompleteProducts_Click

        Dim sep As New Label() With {.Left = 15, .Top = 190, .Width = 640, .Height = 1, .BorderStyle = BorderStyle.Fixed3D}

        AddLabel(top, "Ölçü No", 15, 205)
        txtId.SetBounds(125, 202, 180, 25)
        txtId.ReadOnly = True
        txtId.BackColor = Color.WhiteSmoke
        AddLabel(top, "Ölçü Adı", 320, 205)
        txtName.SetBounds(400, 202, 230, 25)
        txtName.PlaceholderText = "Opsiyonel"

        AddLabel(top, "Nominal", 15, 240)
        txtNominal.SetBounds(125, 237, 105, 25)
        AddLabel(top, "Alt Tol.", 250, 240)
        txtLowerTol.SetBounds(315, 237, 90, 25)
        AddLabel(top, "Üst Tol.", 430, 240)
        txtUpperTol.SetBounds(500, 237, 90, 25)

        AddLabel(top, "Birim", 15, 275)
        txtUnit.SetBounds(125, 272, 105, 25)
        txtUnit.Text = "mm"
        AddLabel(top, "Sıra No", 250, 275)
        txtSort.SetBounds(315, 272, 90, 25)
        chkMandatory.Text = "Zorunlu ölçü"
        chkMandatory.SetBounds(430, 272, 160, 25)
        chkMandatory.Checked = True

        AddLabel(top, "PDF Sayfa", 15, 310)
        txtPage.SetBounds(125, 307, 65, 25)
        txtPage.Text = "1"
        AddLabel(top, "X %", 220, 310)
        txtX.SetBounds(260, 307, 90, 25)
        AddLabel(top, "Y %", 375, 310)
        txtY.SetBounds(415, 307, 90, 25)

        AddLabel(top, "Ölçüm Grubu", 15, 345)
        cboMeasurementGroup.SetBounds(125, 342, 120, 25)
        cboMeasurementGroup.DropDownStyle = ComboBoxStyle.DropDown
        cboMeasurementGroup.Items.AddRange({"Genel", "Kritik", "Fonksiyonel", "Montaj", "Görsel"})
        cboMeasurementGroup.Text = "Genel"

        AddLabel(top, "Sıklık", 260, 345)
        cboSampleFrequency.SetBounds(310, 342, 145, 25)
        cboSampleFrequency.DropDownStyle = ComboBoxStyle.DropDown
        cboSampleFrequency.Items.AddRange({"Her Kontrol", "İlk Ürün", "Saatlik", "Vardiya Başlangıcı", "Kontrol Planına Göre"})
        cboSampleFrequency.Text = "Her Kontrol"

        chkCritical.Text = "Kritik ölçü"
        chkCritical.SetBounds(475, 342, 130, 25)

        Dim btnImportCad As New Button() With {
            .Text = "DWG'den Ölçüleri Al",
            .Left = 15,
            .Top = 385,
            .Width = 185,
            .Height = 30,
            .Cursor = Cursors.Hand,
            .BackColor = Color.FromArgb(224, 239, 255),
            .ForeColor = Color.FromArgb(25, 70, 125),
            .FlatStyle = FlatStyle.Flat
        }
        AddHandler btnImportCad.Click, AddressOf ImportCadDimensions_Click

        Dim btnImportDxf As New Button() With {
            .Text = "DXF'den Ölçüleri Al",
            .Left = 210,
            .Top = 385,
            .Width = 185,
            .Height = 30,
            .Cursor = Cursors.Hand,
            .BackColor = Color.FromArgb(232, 245, 233),
            .ForeColor = Color.DarkGreen,
            .FlatStyle = FlatStyle.Flat
        }
        AddHandler btnImportDxf.Click, AddressOf ImportDxfDimensions_Click

        Dim btnSave As New Button() With {.Text = "Kaydet / Güncelle", .Left = 405, .Top = 385, .Width = 145, .Height = 30}
        AddHandler btnSave.Click, AddressOf Save_Click
        Dim btnPassive As New Button() With {.Text = "Seçili Ölçüyü Pasif Yap", .Left = 560, .Top = 385, .Width = 170, .Height = 30}
        AddHandler btnPassive.Click, AddressOf Passive_Click
        Dim btnNew As New Button() With {.Text = "Yeni Ölçü", .Left = 740, .Top = 385, .Width = 105, .Height = 30}
        AddHandler btnNew.Click, Sub() ClearInputs()

        Dim btnMoveUp As New Button() With {
            .Text = "Sıra ↑",
            .Left = 855,
            .Top = 385,
            .Width = 80,
            .Height = 30,
            .Cursor = Cursors.Hand
        }
        AddHandler btnMoveUp.Click, Sub() MoveSelectedControlPointOrder(-1)

        Dim btnMoveDown As New Button() With {
            .Text = "Sıra ↓",
            .Left = 945,
            .Top = 385,
            .Width = 80,
            .Height = 30,
            .Cursor = Cursors.Hand
        }
        AddHandler btnMoveDown.Click, Sub() MoveSelectedControlPointOrder(1)

        Dim btnDelete As New Button() With {
            .Text = "Seçili Ölçüyü Sil",
            .Left = 15,
            .Top = 420,
            .Width = 190,
            .Height = 30,
            .Cursor = Cursors.Hand,
            .BackColor = Color.MistyRose,
            .ForeColor = Color.DarkRed,
            .FlatStyle = FlatStyle.Flat
        }
        AddHandler btnDelete.Click, AddressOf Delete_Click

        btnGroupAreaCapture.Text = "Grup Alanı Çiz"
        btnGroupAreaCapture.SetBounds(215, 420, 170, 30)
        btnGroupAreaCapture.Cursor = Cursors.Cross
        btnGroupAreaCapture.BackColor = Color.Honeydew
        btnGroupAreaCapture.ForeColor = Color.DarkGreen
        btnGroupAreaCapture.FlatStyle = FlatStyle.Flat
        AddHandler btnGroupAreaCapture.Click, AddressOf GroupAreaCapture_Click

        Dim btnDeleteGroupArea As New Button() With {
            .Text = "Grup Alanını Sil",
            .Left = 395,
            .Top = 420,
            .Width = 160,
            .Height = 30,
            .Cursor = Cursors.Hand
        }
        AddHandler btnDeleteGroupArea.Click, AddressOf DeleteGroupArea_Click

        Dim btnSpcHistory As New Button() With {
            .Text = "SPC Geçmişi",
            .Left = 565,
            .Top = 420,
            .Width = 120,
            .Height = 30,
            .Cursor = Cursors.Hand,
            .BackColor = Color.FromArgb(245, 250, 255),
            .ForeColor = Color.FromArgb(25, 70, 125),
            .FlatStyle = FlatStyle.Flat
        }
        AddHandler btnSpcHistory.Click, AddressOf SpcHistory_Click

        Dim btnRevise As New Button() With {
            .Text = "Ölçüyü Revize Et",
            .Left = 695,
            .Top = 420,
            .Width = 140,
            .Height = 30,
            .Cursor = Cursors.Hand,
            .BackColor = Color.LemonChiffon,
            .ForeColor = Color.DarkGoldenrod,
            .FlatStyle = FlatStyle.Flat
        }
        AddHandler btnRevise.Click, AddressOf Revise_Click

        Dim actionLayout As New TableLayoutPanel() With {
            .Left = 11,
            .Top = 381,
            .Width = 820,
            .Height = 74,
            .ColumnCount = 12,
            .RowCount = 2,
            .Margin = New Padding(0),
            .Padding = New Padding(0),
            .BackColor = Color.Transparent,
            .Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        }
        For columnIndex As Integer = 0 To 11
            actionLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F / 12.0F))
        Next
        actionLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 50.0F))
        actionLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 50.0F))

        For Each button In {btnImportCad, btnImportDxf, btnSave, btnPassive, btnNew,
                            btnMoveUp, btnMoveDown, btnDelete, btnGroupAreaCapture, btnDeleteGroupArea, btnSpcHistory, btnRevise}
            button.Dock = DockStyle.Fill
            button.Height = 31
            button.Margin = New Padding(3)
            button.AutoEllipsis = False
        Next

        actionLayout.Controls.Add(btnImportCad, 0, 0)
        actionLayout.SetColumnSpan(btnImportCad, 2)
        actionLayout.Controls.Add(btnImportDxf, 2, 0)
        actionLayout.SetColumnSpan(btnImportDxf, 2)
        actionLayout.Controls.Add(btnSave, 4, 0)
        actionLayout.SetColumnSpan(btnSave, 2)
        actionLayout.Controls.Add(btnPassive, 6, 0)
        actionLayout.SetColumnSpan(btnPassive, 3)
        actionLayout.Controls.Add(btnNew, 9, 0)
        actionLayout.SetColumnSpan(btnNew, 3)

        actionLayout.Controls.Add(btnMoveUp, 0, 1)
        actionLayout.Controls.Add(btnMoveDown, 1, 1)
        actionLayout.Controls.Add(btnDelete, 2, 1)
        actionLayout.SetColumnSpan(btnDelete, 2)
        actionLayout.Controls.Add(btnGroupAreaCapture, 4, 1)
        actionLayout.SetColumnSpan(btnGroupAreaCapture, 2)
        actionLayout.Controls.Add(btnDeleteGroupArea, 6, 1)
        actionLayout.SetColumnSpan(btnDeleteGroupArea, 2)
        actionLayout.Controls.Add(btnSpcHistory, 8, 1)
        actionLayout.SetColumnSpan(btnSpcHistory, 2)
        actionLayout.Controls.Add(btnRevise, 10, 1)
        actionLayout.SetColumnSpan(btnRevise, 2)

        Dim resizeActionLayout As Action =
            Sub()
                If actionLayout.IsDisposed OrElse top.IsDisposed Then Return
                actionLayout.Width = Math.Max(820, top.ClientSize.Width - actionLayout.Left - 12)
            End Sub
        AddHandler top.ClientSizeChanged, Sub() resizeActionLayout.Invoke()
        resizeActionLayout.Invoke()

        Dim lblGroupAreaHelp As New Label() With {
            .Text = "Grup alanı: Grup adını seçin, Grup Alanı Çiz'e basın ve teknik resimde dikdörtgen çizin.",
            .Left = 15,
            .Top = 458,
            .Width = 640,
            .Height = 28,
            .ForeColor = Color.DimGray,
            .BackColor = Color.Transparent
        }

        top.Controls.AddRange({cboProduct, txtProductFilter, txtMetaTrCode, txtMetaProductName, txtMetaPlasticCode, txtMetaMaterial, txtMetaColorName, txtMetaMoldCavityCount, txtMetaMoldCode, btnMetaSave, btnIncompleteProducts, sep, txtId, txtName, txtNominal, txtLowerTol, txtUpperTol, txtUnit, txtSort, txtPage, txtX, txtY, cboMeasurementGroup, cboSampleFrequency, chkMandatory, chkCritical, actionLayout, lblGroupAreaHelp})

        If Not AppState.CanOpenTechnicalDrawingAdmin Then
            For Each box In {txtMetaProductName, txtMetaPlasticCode, txtMetaMaterial, txtMetaColorName,
                             txtMetaMoldCavityCount, txtMetaMoldCode, txtName, txtNominal, txtLowerTol,
                             txtUpperTol, txtUnit, txtSort, txtPage, txtX, txtY}
                box.ReadOnly = True
                box.BackColor = Color.FromArgb(245, 247, 250)
            Next
            cboMeasurementGroup.Enabled = False
            cboSampleFrequency.Enabled = False
            chkMandatory.Enabled = False
            chkCritical.Enabled = False
            btnMetaSave.Visible = False
            btnImportCad.Visible = False
            btnImportDxf.Visible = False
            btnSave.Visible = False
            btnPassive.Visible = False
            btnNew.Visible = False
            btnMoveUp.Visible = False
            btnMoveDown.Visible = False
            btnDelete.Visible = False
            btnGroupAreaCapture.Visible = False
            btnDeleteGroupArea.Visible = False
            btnRevise.Visible = False
            lblGroupAreaHelp.Text = "SALT OKUNUR: Ürün bilgileri, teknik resim ve tanımlı kontrol ölçüleri görüntülenebilir; değişiklik yapılamaz."
            lblGroupAreaHelp.ForeColor = Color.FromArgb(31, 71, 126)
            lblGroupAreaHelp.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        End If

        Dim listHeader As New Panel() With {.Dock = DockStyle.Fill, .Padding = New Padding(10, 6, 10, 4), .BackColor = Color.WhiteSmoke}
        lblPointCount.Text = "Tanımlı Ölçüler: 0 adet"
        lblPointCount.Dock = DockStyle.Fill
        lblPointCount.Font = New Font("Segoe UI", 10.0F, FontStyle.Bold)
        lblPointCount.ForeColor = Color.FromArgb(40, 40, 40)
        listHeader.Controls.Add(lblPointCount)
        rightLayout.Controls.Add(listHeader, 0, 2)

        ConfigureGrid()
        rightLayout.Controls.Add(grid, 0, 3)
    End Sub

    Private Sub ConfigureGrid()
        grid.Dock = DockStyle.Fill
        grid.ReadOnly = True
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.MultiSelect = False
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.RowHeadersVisible = False
        grid.ColumnHeadersVisible = True
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 235, 235)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        grid.BackgroundColor = Color.White
        grid.BorderStyle = BorderStyle.FixedSingle
        grid.GridColor = Color.Gainsboro
        grid.AutoGenerateColumns = False
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        grid.RowTemplate.Height = 26
        grid.Columns.Clear()
        grid.Columns.Add(MakeTextColumn("SortNo", "Sıra", 55, 55))
        grid.Columns.Add(MakeTextColumn("MeasureId", "Ölçü No", 160, 160))
        grid.Columns.Add(MakeTextColumn("MeasureName", "Ölçü Adı", 130, 130))
        grid.Columns.Add(MakeTextColumn("MeasurementGroup", "Grup", 85, 85))
        grid.Columns.Add(MakeTextColumn("SampleFrequency", "Numune Sıklığı", 95, 95))
        grid.Columns.Add(MakeTextColumn("IsCritical", "Kritik", 55, 55))
        grid.Columns.Add(MakeTextColumn("Nominal", "Nominal", 70, 70))
        grid.Columns.Add(MakeTextColumn("LowerLimit", "Alt Limit", 70, 70))
        grid.Columns.Add(MakeTextColumn("UpperLimit", "Üst Limit", 70, 70))
        grid.Columns.Add(MakeTextColumn("PageNo", "Sayfa", 45, 45))
        grid.Columns.Add(MakeTextColumn("XPercent", "X %", 50, 50))
        grid.Columns.Add(MakeTextColumn("YPercent", "Y %", 50, 50))
        grid.Columns.Add(MakeTextColumn("IsActive", "Aktif", 50, 50))
        grid.Columns.Add(MakeTextColumn("SpcKey", "SPC", 95, 70))
        grid.Columns.Add(MakeTextColumn("MeasureVersion", "V.", 45, 35))
        AddHandler grid.CellClick, AddressOf Grid_Click
        AddHandler grid.CellDoubleClick, AddressOf Grid_DoubleClick
    End Sub

    Private Function MakeTextColumn(name As String, header As String, width As Integer, fillWeight As Single) As DataGridViewTextBoxColumn
        Return New DataGridViewTextBoxColumn() With {
            .Name = name,
            .HeaderText = header,
            .Width = width,
            .MinimumWidth = 40,
            .FillWeight = fillWeight,
            .SortMode = DataGridViewColumnSortMode.Automatic
        }
    End Function

    Private Sub AddLabel(parent As Control, text As String, x As Integer, y As Integer)
        parent.Controls.Add(New Label() With {.Text = text, .Left = x, .Top = y + 3, .AutoSize = True, .BackColor = Color.Transparent})
    End Sub

    Private Sub LoadProductMetadata()
        Dim p = SelectedProduct()
        If p Is Nothing Then
            txtMetaTrCode.Clear()
            txtMetaProductName.Clear()
            txtMetaPlasticCode.Clear()
            txtMetaMaterial.Clear()
            txtMetaColorName.Clear()
            txtMetaMoldCavityCount.Clear()
            txtMetaMoldCode.Clear()
            Return
        End If

        txtMetaTrCode.Text = p.TrCode
        txtMetaProductName.Text = p.ProductName
        txtMetaPlasticCode.Text = p.PlasticCode
        txtMetaMaterial.Text = p.Material
        txtMetaColorName.Text = p.ColorName
        txtMetaMoldCavityCount.Text = p.MoldCavityCount
        txtMetaMoldCode.Text = p.MoldCode
    End Sub

    Private Sub SaveProductMetadata_Click(sender As Object, e As EventArgs)
        Try
            Dim p = SelectedProduct()
            If p Is Nothing Then Return

            p.ProductName = txtMetaProductName.Text.Trim()
            p.PlasticCode = txtMetaPlasticCode.Text.Trim()
            p.Material = txtMetaMaterial.Text.Trim()
            p.ColorName = txtMetaColorName.Text.Trim()
            p.MoldCavityCount = txtMetaMoldCavityCount.Text.Trim()
            p.MoldCode = txtMetaMoldCode.Text.Trim()

            DataService.SaveProductMetadata(p)
            AuditService.Log("PRODUCT_METADATA_SAVE", p.TrCode, p.DrawingRev,
                             $"PlastikKodu={p.PlasticCode}; Malzeme={p.Material}; Renk={p.ColorName}; KalipGozAdedi={p.MoldCavityCount}; KalipKodu={p.MoldCode}")

            Dim selectedKey = p.TrCode & "|" & p.DrawingRev & "|" & p.DrawingFile
            allProducts = DataService.GetProducts(True)
            ApplyProductFilter()
            For i As Integer = 0 To cboProduct.Items.Count - 1
                Dim item = TryCast(cboProduct.Items(i), ProductInfo)
                If item IsNot Nothing AndAlso (item.TrCode & "|" & item.DrawingRev & "|" & item.DrawingFile) = selectedKey Then
                    cboProduct.SelectedIndex = i
                    Exit For
                End If
            Next
            LoadProductMetadata()

            MessageBox.Show("Ürün bilgileri kaydedildi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Ürün bilgileri kaydedilemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub LoadProducts()
        allProducts = DataService.GetProducts(True)
        UpdateIncompleteProductButton()
        ApplyProductFilter()
    End Sub

    Private Sub UpdateIncompleteProductButton()
        Dim controlPointKeys = DataService.GetActiveControlPointProductKeys()
        Dim incompleteCount = allProducts.Where(Function(p) HasMissingProductInfoOrControlPoints(p, controlPointKeys)).Count()
        btnIncompleteProducts.Text = $"Eksik Ürünler ({incompleteCount})"
        btnIncompleteProducts.ForeColor = If(incompleteCount > 0, Color.DarkRed, SystemColors.ControlText)
        btnIncompleteProducts.Enabled = incompleteCount > 0
    End Sub

    Private Sub ShowIncompleteProducts_Click(sender As Object, e As EventArgs)
        allProducts = DataService.GetProducts(True)
        Dim controlPointKeys = DataService.GetActiveControlPointProductKeys()

        Dim incompleteProducts = allProducts.Where(Function(p) HasMissingProductInfoOrControlPoints(p, controlPointKeys)).ToList()
        btnIncompleteProducts.Text = $"Eksik Ürünler ({incompleteProducts.Count})"
        btnIncompleteProducts.ForeColor = If(incompleteProducts.Count > 0, Color.DarkRed, SystemColors.ControlText)
        btnIncompleteProducts.Enabled = incompleteProducts.Count > 0
        If incompleteProducts.Count = 0 Then
            MessageBox.Show("Eksik ürün bilgisi veya kontrol ölçüsü bulunmayan aktif kayıt yok.", "Eksik Ürün Bilgileri", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Using form As New FrmIncompleteProductInfo(incompleteProducts, controlPointKeys)
            If form.ShowDialog(Me) <> DialogResult.OK Then Return

            txtProductFilter.Clear()
            ApplyProductFilter()
            SelectProduct(form.SelectedTrCode, form.SelectedDrawingRev)
        End Using
    End Sub

    Private Shared Function HasMissingProductInfoOrControlPoints(product As ProductInfo,
                                                                 controlPointKeys As HashSet(Of String)) As Boolean
        If product Is Nothing Then Return False
        If product.HasIncompleteMetadata Then Return True

        Dim key = DataService.GetControlPointProductKey(product.TrCode, product.DrawingRev, product.DrawingScope)
        Return controlPointKeys Is Nothing OrElse Not controlPointKeys.Contains(key)
    End Function

    Private Sub SelectProduct(trCode As String, drawingRev As String)
        For i As Integer = 0 To cboProduct.Items.Count - 1
            Dim item = TryCast(cboProduct.Items(i), ProductInfo)
            If item Is Nothing Then Continue For
            If String.Equals(item.TrCode, trCode, StringComparison.OrdinalIgnoreCase) AndAlso
               String.Equals(item.DrawingRev, drawingRev, StringComparison.OrdinalIgnoreCase) Then
                cboProduct.SelectedIndex = i
                Return
            End If
        Next
    End Sub

    Private Sub ApplyProductFilter()
        Dim filterText As String = txtProductFilter.Text.Trim()
        Dim selectedKey As String = ""
        Dim current = SelectedProduct()
        If current IsNot Nothing Then selectedKey = current.TrCode & "|" & current.DrawingRev & "|" & current.DrawingFile

        Dim filtered As List(Of ProductInfo)
        If filterText = "" Then
            filtered = allProducts.ToList()
        Else
            Dim tokens = filterText.Split(New Char() {" "c, ";"c, ","c, "|"c}, StringSplitOptions.RemoveEmptyEntries)
            filtered = allProducts.Where(Function(p)
                                             Dim haystack As String = (p.TrCode & " " & p.DrawingRev & " " & p.ProductName & " " & p.DisplayName).ToUpperInvariant()
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
            Dim restoreIndex As Integer = filtered.FindIndex(Function(p) (p.TrCode & "|" & p.DrawingRev & "|" & p.DrawingFile) = selectedKey)
            cboProduct.SelectedIndex = If(restoreIndex >= 0, restoreIndex, 0)
        Else
            lblPointCount.Text = "Tanımlı Ölçüler: 0 adet   |   Filtreye uygun aktif teknik resim bulunamadı."
            grid.DataSource = Nothing
        End If
    End Sub

    Private Function SelectedProduct() As ProductInfo
        Return TryCast(cboProduct.SelectedItem, ProductInfo)
    End Function

    Private Sub LoadGrid()
        Dim p = SelectedProduct()
        grid.Rows.Clear()

        If p Is Nothing Then
            lblPointCount.Text = "Tanımlı Ölçüler: 0 adet"
            Return
        End If

        Dim points = DataService.GetControlPoints(p.TrCode, p.DrawingRev, False, p.DrawingScope)
        For Each cp In points
            Dim xText As String = If(cp.XPercent = 0D, "", NumberUtil.DecToCsv(cp.XPercent))
            Dim yText As String = If(cp.YPercent = 0D, "", NumberUtil.DecToCsv(cp.YPercent))
            Dim idx = grid.Rows.Add(cp.SortNo.ToString(),
                                    cp.MeasureId,
                                    cp.MeasureName,
                                    cp.MeasurementGroup,
                                    cp.SampleFrequency,
                                    If(String.Equals(cp.IsCritical, "YES", StringComparison.OrdinalIgnoreCase), "EVET", ""),
                                    NumberUtil.DecToCsv(cp.Nominal),
                                    NumberUtil.DecToCsv(cp.LowerLimit),
                                    NumberUtil.DecToCsv(cp.UpperLimit),
                                    cp.PageNo.ToString(),
                                    xText,
                                    yText,
                                    cp.IsActive,
                                    If(String.IsNullOrWhiteSpace(cp.SpcKey), cp.MeasureId, cp.SpcKey),
                                    cp.MeasureVersion.ToString())
            grid.Rows(idx).Tag = cp
            If Not String.Equals(cp.IsActive, "YES", StringComparison.OrdinalIgnoreCase) Then
                grid.Rows(idx).DefaultCellStyle.ForeColor = SystemColors.GrayText
            End If
        Next

        lblPointCount.Text = $"Tanımlı Ölçüler: {points.Count} adet   |   {p.TrCode} / {p.DrawingRev}"
    End Sub

    Private Sub UpdateZoomLabel()
        lblZoomText.Text = currentPdfZoom.ToString() & "%"
    End Sub

    Private Sub ExecutePdfScript(script As String)
        Try
            If pdfViewer.CoreWebView2 IsNot Nothing Then
                pdfViewer.CoreWebView2.ExecuteScriptAsync(script)
            End If
        Catch ex As Exception
            ErrorLogService.Log("FrmControlPointAdmin.ExecutePdfScript", ex)
        End Try
    End Sub

    Private Sub LoadPdf_Click(sender As Object, e As EventArgs)
        LoadPdfToViewer(True)
    End Sub

    Private Async Sub LoadPdfToViewer(showErrors As Boolean)
        Try
            Dim p = SelectedProduct()
            If p Is Nothing Then Return

            groupAreaCaptureEnabled = False
            btnGroupAreaCapture.Text = "Grup Alanı Çiz"
            btnGroupAreaCapture.BackColor = Color.Honeydew

            CleanupCurrentPdfFiles()

            Dim imageUri As String = ""
            Dim aspectText As String = "1 / 1"
            Dim sourceKind As String = "PDF"

            If TempFileService.IsEncryptedPdf(p.DrawingFile) Then
                currentTempPdf = TempFileService.DecryptEncryptedPdfToTemp(p.DrawingFile)
                currentTempPng = PdfRenderService.RenderFirstPageToPng(currentTempPdf)
                imageUri = New Uri(currentTempPng).AbsoluteUri
                aspectText = PdfRenderService.GetImageAspectRatioText(currentTempPng)
            ElseIf TempFileService.IsEncryptedDxf(p.DrawingFile) Then
                currentTempPdf = TempFileService.DecryptEncryptedDrawingToTemp(p.DrawingFile)
                Dim render = DxfRenderService.RenderToSvg(currentTempPdf)
                currentTempPng = render.SvgPath
                imageUri = New Uri(currentTempPng).AbsoluteUri
                aspectText = render.AspectRatioText
                sourceKind = "DXF"
            Else
                Throw New InvalidDataException("Desteklenmeyen teknik resim türü: " & p.DrawingFile)
            End If

            currentTempHtml = Path.Combine(AppPaths.TempDir, "pdf_view_" & Guid.NewGuid().ToString("N") & ".html")

            File.WriteAllText(currentTempHtml, BuildPdfWrapperHtml(imageUri, chkCapture.Checked, aspectText), New UTF8Encoding(False))

            Await pdfViewer.EnsureCoreWebView2Async()
            If Not webMessageHooked Then
                AddHandler pdfViewer.CoreWebView2.WebMessageReceived, AddressOf PdfMessageReceived
                webMessageHooked = True
            End If

            If Not navigationHooked Then
                AddHandler pdfViewer.CoreWebView2.NavigationCompleted, AddressOf PdfNavigationCompleted
                navigationHooked = True
            End If

            currentPdfZoom = 100
            UpdateZoomLabel()
            pdfViewer.Source = New Uri(currentTempHtml)
            lblPdfInfo.Text = sourceKind & " açık: " & p.TrCode & " / " & p.DrawingRev & "   |   Ölçü noktası için yakalama kutusunu işaretleyip teknik resme tıklayın."
            AuditService.Log(sourceKind & "_VIEW_INTERNAL", p.TrCode, p.DrawingRev, "Kontrol ölçüsü tanımlama ekranında açıldı.")
        Catch ex As Exception
            lblPdfInfo.Text = "Teknik resim açılamadı."
            If showErrors Then MessageBox.Show(ex.Message, "Teknik resim açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Function GetPdfPageAspectRatio(pdfPath As String) As String
        Try
            Dim raw As String = Encoding.ASCII.GetString(File.ReadAllBytes(pdfPath))

            Dim boxMatch As Match = Regex.Match(raw, "/CropBox\s*\[\s*([-0-9\.]+)\s+([-0-9\.]+)\s+([-0-9\.]+)\s+([-0-9\.]+)\s*\]", RegexOptions.IgnoreCase)
            If Not boxMatch.Success Then
                boxMatch = Regex.Match(raw, "/MediaBox\s*\[\s*([-0-9\.]+)\s+([-0-9\.]+)\s+([-0-9\.]+)\s+([-0-9\.]+)\s*\]", RegexOptions.IgnoreCase)
            End If

            If boxMatch.Success Then
                Dim x0 As Decimal = Decimal.Parse(boxMatch.Groups(1).Value, Globalization.CultureInfo.InvariantCulture)
                Dim y0 As Decimal = Decimal.Parse(boxMatch.Groups(2).Value, Globalization.CultureInfo.InvariantCulture)
                Dim x1 As Decimal = Decimal.Parse(boxMatch.Groups(3).Value, Globalization.CultureInfo.InvariantCulture)
                Dim y1 As Decimal = Decimal.Parse(boxMatch.Groups(4).Value, Globalization.CultureInfo.InvariantCulture)

                Dim w As Decimal = Math.Abs(x1 - x0)
                Dim h As Decimal = Math.Abs(y1 - y0)
                If w > 0D AndAlso h > 0D Then
                    Dim rotated As Boolean = Regex.IsMatch(raw, "/Rotate\s+(90|270)", RegexOptions.IgnoreCase)
                    Dim aspect As Decimal
                    If rotated Then
                        aspect = h / w
                    Else
                        aspect = w / h
                    End If

                    If aspect > 0.1D AndAlso aspect < 10D Then
                        Return aspect.ToString("0.########", Globalization.CultureInfo.InvariantCulture)
                    End If
                End If
            End If
        Catch ex As Exception
            ErrorLogService.Log("FrmControlPointAdmin.ReadPdfAspect", ex)
        End Try

        ' Teknik resimlerde varsayılan genellikle yatay A4'tür.
        Return "1.41421356"
    End Function

    Private Function BuildPdfWrapperHtml(pdfUri As String, captureEnabled As Boolean, pageAspect As String) As String
        Dim encodedPdfUri As String = WebUtility.HtmlEncode(pdfUri)
        Dim initialMode As String = If(captureEnabled, "true", "false")
        Dim sb As New StringBuilder()
        sb.AppendLine("<!DOCTYPE html>")
        sb.AppendLine("<html><head><meta charset='utf-8'>")
        sb.AppendLine("<style>")
        sb.AppendLine("html, body { margin:0; width:100%; height:100%; overflow:hidden; background:#fff; }")
        sb.AppendLine("#viewport { position:relative; width:100%; height:100%; overflow:auto; background:#fff; }")
        sb.AppendLine("#stage { position:absolute; left:0; top:0; background:#fff; }")
        sb.AppendLine("#pdf { position:absolute; border:0; z-index:1; background:#fff; pointer-events:none; display:block; image-rendering:auto; transform-origin:center center; }")
        sb.AppendLine("#overlay { position:absolute; left:0; top:0; width:100%; height:100%; z-index:20; pointer-events:none; }")
        sb.AppendLine("#capture { position:absolute; left:0; top:0; width:100%; height:100%; z-index:30; pointer-events:none; cursor:crosshair; }")
        sb.AppendLine("#areaCapture { position:absolute; left:0; top:0; width:100%; height:100%; z-index:31; pointer-events:none; cursor:crosshair; }")
        sb.AppendLine(".marker { position:absolute; transform:translate(-50%,-50%); width:30px; height:30px; min-width:30px; padding:0; border-radius:50%; border:2px solid #8b0000; background:#dc3545; color:#fff; font-size:11px; font-weight:800; line-height:26px; text-align:center; box-shadow:0 1px 4px rgba(0,0,0,.35); pointer-events:auto; cursor:pointer; }")
        sb.AppendLine(".marker.passive { background:#8a8a8a; border-color:#555; }")
        sb.AppendLine(".marker.critical { outline:3px solid rgba(139,0,0,.72); outline-offset:2px; }")
        sb.AppendLine(".marker.sel { border-color:#ffcc00; box-shadow:0 0 0 5px rgba(255,204,0,.55),0 1px 4px rgba(0,0,0,.35); }")
        sb.AppendLine(".marker:hover { filter:brightness(.95); transform:translate(-50%,-50%) scale(1.12); }")
        sb.AppendLine(".selected-point { position:absolute; transform:translate(-50%,-50%); width:24px; height:24px; border-radius:50%; border:4px solid #ffcc00; background:#dc3545; box-shadow:0 0 0 5px rgba(220,53,69,.35),0 2px 8px rgba(0,0,0,.55); z-index:80; pointer-events:auto; }")
        sb.AppendLine(".group-region { position:absolute; border:3px dashed rgba(25,86,155,.9); background:rgba(25,86,155,.10); box-sizing:border-box; pointer-events:none; }")
        sb.AppendLine(".group-region span { position:absolute; left:3px; top:3px; padding:2px 6px; border-radius:3px; background:rgba(25,86,155,.92); color:#fff; font:700 11px Segoe UI,Arial; }")
        sb.AppendLine(".area-draft { position:absolute; border:3px solid #ff8c00; background:rgba(255,140,0,.14); box-sizing:border-box; pointer-events:none; }")
        sb.AppendLine("</style></head><body>")
        sb.AppendLine("<div id='viewport'><div id='stage'>")
        sb.AppendLine("<img id='pdf' src='" & encodedPdfUri & "' alt='PDF Sayfası' />")
        sb.AppendLine("<div id='overlay'></div>")
        sb.AppendLine("<div id='capture'></div>")
        sb.AppendLine("<div id='areaCapture'></div>")
        sb.AppendLine("</div></div>")
        sb.AppendLine("<script>")
        sb.AppendLine("const viewport=document.getElementById('viewport'); const stage=document.getElementById('stage'); const cap=document.getElementById('capture'); const areaCap=document.getElementById('areaCapture'); const img=document.getElementById('pdf'); const overlay=document.getElementById('overlay'); let zoom=1; let baseW=0; let baseH=0; let rotation=0; const markers={}; const regions=[]; let selectedPoint=null; let selectedPointData=null; let areaStart=null; let areaDraft=null;")
        sb.AppendLine("function imageAspect(){ const w=img.naturalWidth||1; const h=img.naturalHeight||1; return w/h; }")
        sb.AppendLine("function visibleAspect(){ const a=imageAspect(); return (rotation%180===0)?a:(1/a); }")
        sb.AppendLine("function layoutImage(){ const sw=stage.clientWidth, sh=stage.clientHeight; if(rotation%180===0){ img.style.left='0px'; img.style.top='0px'; img.style.width=sw+'px'; img.style.height=sh+'px'; } else { img.style.width=sh+'px'; img.style.height=sw+'px'; img.style.left=((sw-sh)/2)+'px'; img.style.top=((sh-sw)/2)+'px'; } img.style.transform='rotate('+rotation+'deg)'; }")
        sb.AppendLine("function origToStage(x,y){ x=parseFloat(x)||0; y=parseFloat(y)||0; if(rotation===90){ return {x:100-y,y:x}; } if(rotation===180){ return {x:100-x,y:100-y}; } if(rotation===270){ return {x:y,y:100-x}; } return {x:x,y:y}; }")
        sb.AppendLine("function stageToOrig(sx,sy){ if(rotation===90){ return {x:sy,y:100-sx}; } if(rotation===180){ return {x:100-sx,y:100-sy}; } if(rotation===270){ return {x:100-sy,y:sx}; } return {x:sx,y:sy}; }")
        sb.AppendLine("function updateMarkerPosition(m){ const pt=origToStage(m.dataset.x,m.dataset.y); m.style.left=pt.x+'%'; m.style.top=pt.y+'%'; }")
        sb.AppendLine("function updateRegionPosition(region){ const points=[origToStage(region.dataset.left,region.dataset.top),origToStage(region.dataset.right,region.dataset.top),origToStage(region.dataset.left,region.dataset.bottom),origToStage(region.dataset.right,region.dataset.bottom)]; const xs=points.map(p=>p.x), ys=points.map(p=>p.y); const l=Math.min(...xs), r=Math.max(...xs), t=Math.min(...ys), b=Math.max(...ys); region.style.left=l+'%';region.style.top=t+'%';region.style.width=(r-l)+'%';region.style.height=(b-t)+'%'; }")
        sb.AppendLine("function updateAllMarkers(){ Object.keys(markers).forEach(function(k){ updateMarkerPosition(markers[k]); }); regions.forEach(updateRegionPosition); updateSelectedPointPosition(); }")
        sb.AppendLine("function updateSelectedPointPosition(){ if(!selectedPoint||!selectedPointData) return; const pt=origToStage(selectedPointData.x,selectedPointData.y); selectedPoint.style.left=pt.x+'%'; selectedPoint.style.top=pt.y+'%'; }")
        sb.AppendLine("function centerOriginalPoint(x,y){ const pt=origToStage(x,y); const sx=stage.clientWidth*pt.x/100+(parseFloat(stage.style.left)||0); const sy=stage.clientHeight*pt.y/100+(parseFloat(stage.style.top)||0); viewport.scrollLeft=Math.max(0,sx-viewport.clientWidth/2); viewport.scrollTop=Math.max(0,sy-viewport.clientHeight/2); }")
        sb.AppendLine("function placeStage(){ const sw=baseW*zoom, sh=baseH*zoom; stage.style.width=sw+'px'; stage.style.height=sh+'px'; const left=Math.max(0,(viewport.clientWidth-sw)/2); const top=Math.max(0,(viewport.clientHeight-sh)/2); stage.style.left=left+'px'; stage.style.top=top+'px'; layoutImage(); updateAllMarkers(); }")
        sb.AppendLine("function fitStage(){ const fitMargin=0.98; const vw=Math.max(10,viewport.clientWidth*fitMargin); const vh=Math.max(10,viewport.clientHeight*fitMargin); const a=visibleAspect(); let w=vw; let h=w/a; if(h>vh){ h=vh; w=h*a; } baseW=w; baseH=h; zoom=1; placeStage(); viewport.scrollLeft=0; viewport.scrollTop=0; notifyZoom(); }")
        sb.AppendLine("function notifyZoom(){ if(window.chrome && chrome.webview){ chrome.webview.postMessage('zoom|' + Math.round(zoom*100)); } }")
        sb.AppendLine("function applyZoom(newZoom, clientX, clientY){ if(!baseW||!baseH) fitStage(); const old=zoom; const sx=viewport.scrollLeft, sy=viewport.scrollTop; const hasPoint=(typeof clientX==='number' && typeof clientY==='number'); zoom=Math.max(0.5,Math.min(4,newZoom)); if(hasPoint){ const rect=viewport.getBoundingClientRect(); const px=(clientX-rect.left+viewport.scrollLeft-(parseFloat(stage.style.left)||0))/old; const py=(clientY-rect.top+viewport.scrollTop-(parseFloat(stage.style.top)||0))/old; placeStage(); viewport.scrollLeft=px*zoom+(parseFloat(stage.style.left)||0)-(clientX-rect.left); viewport.scrollTop=py*zoom+(parseFloat(stage.style.top)||0)-(clientY-rect.top); } else { placeStage(); if(zoom<=1){ viewport.scrollLeft=0; viewport.scrollTop=0; } else { viewport.scrollLeft=sx; viewport.scrollTop=sy; } } notifyZoom(); }")
        sb.AppendLine("viewport.addEventListener('wheel', function(ev){ if(ev.ctrlKey){ ev.preventDefault(); applyZoom(zoom*(ev.deltaY<0?1.12:0.89), ev.clientX, ev.clientY); } }, {passive:false});")
        sb.AppendLine("window.externalSetZoom=function(z){ applyZoom(z); };")
        sb.AppendLine("window.externalFit=function(){ fitStage(); };")
        sb.AppendLine("window.externalScroll=function(dx,dy){ viewport.scrollLeft += dx; viewport.scrollTop += dy; };")
        sb.AppendLine("window.externalRotate=function(delta){ rotation=(rotation+delta+360)%360; fitStage(); };")
        sb.AppendLine("window.setCaptureMode=function(v){ cap.style.pointerEvents = v ? 'auto' : 'none'; };")
        sb.AppendLine("window.setGroupAreaCapture=function(v){ areaCap.style.pointerEvents = v ? 'auto' : 'none'; if(!v && areaDraft){areaDraft.remove();areaDraft=null;areaStart=null;} };")
        sb.AppendLine("window.setCaptureMode(" & initialMode & ");")
        sb.AppendLine("window.setGroupAreaCapture(false);")
        sb.AppendLine("window.addEventListener('resize', fitStage);")
        sb.AppendLine("function delayedFit(){ setTimeout(fitStage,50); setTimeout(fitStage,250); setTimeout(fitStage,700); } if(img.complete){ delayedFit(); } else { img.onload=function(){ delayedFit(); }; }")
        sb.AppendLine("function centerMarker(id){ const m=markers[id]; if(!m) return false; centerOriginalPoint(parseFloat(m.dataset.x)||0,parseFloat(m.dataset.y)||0); return true; }")
        sb.AppendLine("window.clearControlHighlight=function(){ Object.keys(markers).forEach(function(k){ markers[k].classList.remove('sel'); }); if(selectedPoint){ selectedPoint.remove(); selectedPoint=null; selectedPointData=null; } };")
        sb.AppendLine("window.highlightControlPoint=function(id){ Object.keys(markers).forEach(function(k){ markers[k].classList.remove('sel'); }); if(markers[id]){ markers[id].classList.add('sel'); centerMarker(id); } };")
        sb.AppendLine("window.showSelectedPoint=function(id,x,y,title){ x=parseFloat(x)||0; y=parseFloat(y)||0; Object.keys(markers).forEach(function(k){ markers[k].classList.remove('sel'); }); if(markers[id]){ markers[id].classList.add('sel'); } if(!selectedPoint){ selectedPoint=document.createElement('div'); selectedPoint.className='selected-point'; overlay.appendChild(selectedPoint); } selectedPointData={id:id,x:x,y:y}; selectedPoint.title=title||id; updateSelectedPointPosition(); centerOriginalPoint(x,y); };")
        sb.AppendLine("cap.addEventListener('click', function(ev){ var r=cap.getBoundingClientRect(); var sx=(ev.clientX-r.left)*100/r.width; var sy=(ev.clientY-r.top)*100/r.height; var pt=stageToOrig(sx,sy); var x=pt.x.toFixed(2); var y=pt.y.toFixed(2); chrome.webview.postMessage(x+';'+y); ev.preventDefault(); ev.stopPropagation(); });")
        sb.AppendLine("function areaPoint(ev){ const r=areaCap.getBoundingClientRect(); return {x:Math.max(0,Math.min(100,(ev.clientX-r.left)*100/r.width)),y:Math.max(0,Math.min(100,(ev.clientY-r.top)*100/r.height))}; }")
        sb.AppendLine("areaCap.addEventListener('mousedown',function(ev){areaStart=areaPoint(ev);if(areaDraft)areaDraft.remove();areaDraft=document.createElement('div');areaDraft.className='area-draft';overlay.appendChild(areaDraft);ev.preventDefault();});")
        sb.AppendLine("areaCap.addEventListener('mousemove',function(ev){if(!areaStart||!areaDraft)return;const p=areaPoint(ev);areaDraft.style.left=Math.min(areaStart.x,p.x)+'%';areaDraft.style.top=Math.min(areaStart.y,p.y)+'%';areaDraft.style.width=Math.abs(p.x-areaStart.x)+'%';areaDraft.style.height=Math.abs(p.y-areaStart.y)+'%';});")
        sb.AppendLine("areaCap.addEventListener('mouseup',function(ev){if(!areaStart)return;const p=areaPoint(ev);const corners=[stageToOrig(areaStart.x,areaStart.y),stageToOrig(p.x,p.y)];const l=Math.min(corners[0].x,corners[1].x),r=Math.max(corners[0].x,corners[1].x),t=Math.min(corners[0].y,corners[1].y),b=Math.max(corners[0].y,corners[1].y);if(areaDraft){areaDraft.remove();areaDraft=null;}areaStart=null;if((r-l)>=1&&(b-t)>=1&&window.chrome&&chrome.webview){chrome.webview.postMessage('grouparea|'+l.toFixed(2)+'|'+t.toFixed(2)+'|'+r.toFixed(2)+'|'+b.toFixed(2));}ev.preventDefault();});")

        Dim p = SelectedProduct()
        If p IsNot Nothing Then
            For Each area In DataService.GetMeasurementGroupAreas(p.TrCode, p.DrawingRev, p.DrawingScope)
                Dim groupText = JsString(area.GroupName)
                Dim leftText = NumberUtil.DecToCsv(area.LeftPercent).Replace(",", ".")
                Dim topText = NumberUtil.DecToCsv(area.TopPercent).Replace(",", ".")
                Dim rightText = NumberUtil.DecToCsv(area.RightPercent).Replace(",", ".")
                Dim bottomText = NumberUtil.DecToCsv(area.BottomPercent).Replace(",", ".")
                sb.AppendLine("(function(){var r=document.createElement('div');r.className='group-region';r.dataset.left='" & leftText & "';r.dataset.top='" & topText & "';r.dataset.right='" & rightText & "';r.dataset.bottom='" & bottomText & "';var s=document.createElement('span');s.textContent='" & groupText & "';r.appendChild(s);overlay.appendChild(r);regions.push(r);updateRegionPosition(r);})();")
            Next

            Dim pointIndex As Integer = 0
            For Each cp In DataService.GetControlPoints(p.TrCode, p.DrawingRev, False, p.DrawingScope)
                pointIndex += 1
                If cp.XPercent > 0D AndAlso cp.YPercent > 0D Then
                    Dim cls = "marker"
                    If Not String.Equals(cp.IsActive, "YES", StringComparison.OrdinalIgnoreCase) Then cls &= " passive"
                    If String.Equals(cp.IsCritical, "YES", StringComparison.OrdinalIgnoreCase) Then cls &= " critical"
                    Dim xText = NumberUtil.DecToCsv(cp.XPercent)
                    Dim yText = NumberUtil.DecToCsv(cp.YPercent)
                    Dim idText = JsString(cp.MeasureId)
                    Dim balloonText = If(cp.SortNo > 0, cp.SortNo, pointIndex).ToString()
                    Dim titleText = JsString(cp.MeasureId & " | " & cp.MeasureName & " | X=" & xText & " Y=" & yText)
                    sb.AppendLine("(function(){var b=document.createElement('button');b.type='button';b.className='" & cls & "';b.dataset.id='" & idText & "';b.dataset.x='" & xText & "';b.dataset.y='" & yText & "';b.textContent='" & balloonText & "';b.title='Balon " & balloonText & " | " & titleText & "';b.setAttribute('aria-label','Balon " & balloonText & " | " & titleText & "');b.addEventListener('click',function(ev){ev.preventDefault();ev.stopPropagation();window.highlightControlPoint(this.dataset.id);if(window.chrome&&chrome.webview){chrome.webview.postMessage('select|'+this.dataset.id);}});overlay.appendChild(b);markers['" & idText & "']=b;updateMarkerPosition(b);})();")
                End If
            Next
        End If

        sb.AppendLine("</script></body></html>")
        Return sb.ToString()
    End Function

    Private Sub PdfNavigationCompleted(sender As Object, e As CoreWebView2NavigationCompletedEventArgs)
        TryApplyPendingHighlight()
    End Sub

    Private Sub TryApplyPendingHighlight()
        If String.IsNullOrWhiteSpace(pendingHighlightMeasureId) Then Return

        For Each row As DataGridViewRow In grid.Rows
            Dim cp = TryCast(row.Tag, ControlPoint)
            If cp Is Nothing Then Continue For

            If String.Equals(cp.MeasureId, pendingHighlightMeasureId, StringComparison.OrdinalIgnoreCase) Then
                pendingHighlightMeasureId = ""
                ExecuteHighlightScript(cp)
                Return
            End If
        Next
    End Sub

    Private Sub PdfMessageReceived(sender As Object, e As CoreWebView2WebMessageReceivedEventArgs)
        Try
            Dim msg As String = e.TryGetWebMessageAsString()
            If String.IsNullOrWhiteSpace(msg) Then Return
            If msg.StartsWith("zoom|", StringComparison.OrdinalIgnoreCase) Then
                Dim z As Integer = 100
                If Integer.TryParse(msg.Substring(5), z) Then
                    currentPdfZoom = z
                    UpdateZoomLabel()
                End If
                Return
            End If

            If msg.StartsWith("select|", StringComparison.OrdinalIgnoreCase) Then
                Dim measureId = msg.Substring(7)
                SelectControlPointRow(measureId)
                LoadControlPointFromSelectedRow()
                Return
            End If

            If msg.StartsWith("grouparea|", StringComparison.OrdinalIgnoreCase) Then
                Dim areaParts = msg.Split("|"c)
                If areaParts.Length = 5 Then
                    Dim leftPct As Decimal
                    Dim topPct As Decimal
                    Dim rightPct As Decimal
                    Dim bottomPct As Decimal
                    If NumberUtil.TryParseDecimal(areaParts(1), leftPct) AndAlso
                       NumberUtil.TryParseDecimal(areaParts(2), topPct) AndAlso
                       NumberUtil.TryParseDecimal(areaParts(3), rightPct) AndAlso
                       NumberUtil.TryParseDecimal(areaParts(4), bottomPct) Then

                        Dim p = SelectedProduct()
                        Dim groupName = cboMeasurementGroup.Text.Trim()
                        If p Is Nothing OrElse groupName = "" Then Return

                        Dim pageNo As Integer = 1
                        Integer.TryParse(txtPage.Text.Trim(), pageNo)
                        DataService.SaveMeasurementGroupArea(New MeasurementGroupArea With {
                            .TrCode = p.TrCode,
                            .DrawingRev = p.DrawingRev,
                            .DrawingScope = p.DrawingScope,
                            .GroupName = groupName,
                            .PageNo = Math.Max(1, pageNo),
                            .LeftPercent = leftPct,
                            .TopPercent = topPct,
                            .RightPercent = rightPct,
                            .BottomPercent = bottomPct
                        })
                        AuditService.Log(
                            "MEASUREMENT_GROUP_AREA_SAVE",
                            p.TrCode,
                            p.DrawingRev,
                            $"Group={groupName}; L={leftPct}; T={topPct}; R={rightPct}; B={bottomPct}")

                        groupAreaCaptureEnabled = False
                        btnGroupAreaCapture.Text = "Grup Alanı Çiz"
                        btnGroupAreaCapture.BackColor = Color.Honeydew
                        LoadPdfToViewer(False)
                        MessageBox.Show(groupName & " ölçüm grubu alanı kaydedildi.",
                                        "Grup alanı", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    End If
                End If
                Return
            End If

            Dim parts = msg.Split(";"c)
            If parts.Length >= 2 Then
                txtX.Text = parts(0)
                txtY.Text = parts(1)
                If txtPage.Text.Trim() = "" Then txtPage.Text = "1"
            End If
        Catch ex As Exception
            ErrorLogService.Log("FrmControlPointAdmin.ApplyCaptureCoordinates", ex)
        End Try
    End Sub

    Private Sub Capture_CheckedChanged(sender As Object, e As EventArgs)
        Try
            If chkCapture.Checked AndAlso groupAreaCaptureEnabled Then
                groupAreaCaptureEnabled = False
                btnGroupAreaCapture.Text = "Grup Alanı Çiz"
                btnGroupAreaCapture.BackColor = Color.Honeydew
                ExecutePdfScript("if(window.setGroupAreaCapture){window.setGroupAreaCapture(false);}")
            End If
            If pdfViewer.CoreWebView2 IsNot Nothing Then
                Dim mode As String = If(chkCapture.Checked, "true", "false")
                pdfViewer.CoreWebView2.ExecuteScriptAsync("if(window.setCaptureMode){window.setCaptureMode(" & mode & ");}")
            End If
        Catch ex As Exception
            ErrorLogService.Log("FrmControlPointAdmin.SetCaptureMode", ex)
        End Try
    End Sub

    Private Sub GroupAreaCapture_Click(sender As Object, e As EventArgs)
        Try
            Dim p = SelectedProduct()
            If p Is Nothing Then Return
            If cboMeasurementGroup.Text.Trim() = "" Then
                MessageBox.Show("Önce ölçüm grubu adını seçin veya yazın.",
                                "Ölçüm grubu", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            If pdfViewer.CoreWebView2 Is Nothing Then
                LoadPdfToViewer(True)
                Return
            End If

            groupAreaCaptureEnabled = Not groupAreaCaptureEnabled
            If groupAreaCaptureEnabled Then
                chkCapture.Checked = False
                btnGroupAreaCapture.Text = "Alan Çizimini İptal Et"
                btnGroupAreaCapture.BackColor = Color.Moccasin
                lblPdfInfo.Text = cboMeasurementGroup.Text.Trim() & " grubu için teknik resimde dikdörtgen alan çizin."
            Else
                btnGroupAreaCapture.Text = "Grup Alanı Çiz"
                btnGroupAreaCapture.BackColor = Color.Honeydew
            End If

            ExecutePdfScript("if(window.setGroupAreaCapture){window.setGroupAreaCapture(" & If(groupAreaCaptureEnabled, "true", "false") & ");}")
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Grup alanı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub DeleteGroupArea_Click(sender As Object, e As EventArgs)
        Try
            Dim p = SelectedProduct()
            Dim groupName = cboMeasurementGroup.Text.Trim()
            If p Is Nothing OrElse groupName = "" Then Return

            If MessageBox.Show(
                groupName & " grubuna tanımlı teknik resim alanı silinsin mi?",
                "Grup alanını sil",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) <> DialogResult.Yes Then Return

            DataService.DeleteMeasurementGroupArea(p.TrCode, p.DrawingRev, groupName, p.DrawingScope)
            AuditService.Log("MEASUREMENT_GROUP_AREA_DELETE", p.TrCode, p.DrawingRev, "Group=" & groupName)
            LoadPdfToViewer(False)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Grup alanı silinemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Function BuildNextMeasureId() As String
        Dim p = SelectedProduct()
        If p Is Nothing Then Return ""

        Dim prefix = BuildMeasureIdPrefix(p)
        Dim maxNo As Integer = 0
        For Each cp In DataService.GetControlPoints(p.TrCode, p.DrawingRev, False, p.DrawingScope)
            Dim id As String = If(cp.MeasureId, "")
            If id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then
                Dim suffix As String = id.Substring(prefix.Length)
                Dim n As Integer = 0
                If Integer.TryParse(suffix, n) AndAlso n > maxNo Then maxNo = n
            End If
        Next
        Return prefix & (maxNo + 1).ToString("000")
    End Function

    Private Function BuildMeasureIdPrefix(product As ProductInfo) As String
        If product Is Nothing Then Return "TR_REV_M"

        Dim trPart As String = SafeMeasureIdPart(product.TrCode)
        Dim revPart As String = SafeMeasureIdPart(product.DrawingRev)
        If trPart = "" Then trPart = "TR"
        If revPart = "" Then revPart = "REV"
        Return trPart & "_" & revPart & "_M"
    End Function

    Private Function SafeMeasureIdPart(value As String) As String
        Dim sb As New StringBuilder()
        For Each ch As Char In value.ToUpperInvariant()
            If Char.IsLetterOrDigit(ch) Then sb.Append(ch)
        Next
        Return sb.ToString()
    End Function

    Private Function GetNextSortNo() As Integer
        Dim p = SelectedProduct()
        If p Is Nothing Then Return 1
        Dim points = DataService.GetControlPoints(p.TrCode, p.DrawingRev, False, p.DrawingScope)
        If points.Count = 0 Then Return 1
        Return points.Max(Function(c) c.SortNo) + 1
    End Function

    Private Async Sub ImportCadDimensions_Click(sender As Object, e As EventArgs)
        Dim importButton = TryCast(sender, Button)
        Try
            Dim product = SelectedProduct()
            If product Is Nothing Then
                MessageBox.Show("Önce TR / Revizyon seçiniz.", "Ürün seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Using ofd As New OpenFileDialog()
                ofd.Title = "Kontrol ölçülerinin alınacağı DWG dosyasını seçin"
                ofd.Filter = "DWG Dosyası (*.dwg)|*.dwg"
                ofd.Multiselect = False
                If ofd.ShowDialog(Me) <> DialogResult.OK Then Return

                If importButton IsNot Nothing Then
                    importButton.Enabled = False
                    importButton.Text = "AutoCAD taranıyor..."
                End If
                UseWaitCursor = True

                Dim extraction = Await Threading.Tasks.Task.Run(
                    Function() AutoCadDimensionImportService.ExtractDimensions(ofd.FileName))

                ImportExtractedCadDimensions(product, extraction, Path.GetFileName(ofd.FileName), "DWG")
            End Using
        Catch ex As Exception
            ErrorLogService.Log("FrmControlPointAdmin.ImportCadDimensions", ex)
            MessageBox.Show(ex.Message, "DWG ölçü aktarımı başarısız", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            UseWaitCursor = False
            If importButton IsNot Nothing Then
                importButton.Enabled = True
                importButton.Text = "DWG'den Ölçüleri Al"
            End If
        End Try
    End Sub

    Private Async Sub ImportDxfDimensions_Click(sender As Object, e As EventArgs)
        Dim importButton = TryCast(sender, Button)
        Dim tempDxfPath As String = ""
        Try
            Dim product = SelectedProduct()
            If product Is Nothing Then
                MessageBox.Show("Önce TR / Revizyon seçiniz.", "Ürün seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim sourceDxfPath As String = ""
            Dim sourceFileName As String = ""
            If TempFileService.IsEncryptedDxf(product.DrawingFile) Then
                tempDxfPath = TempFileService.DecryptEncryptedDrawingToTemp(product.DrawingFile)
                sourceDxfPath = tempDxfPath
                sourceFileName = Path.GetFileNameWithoutExtension(product.DrawingFile)
            ElseIf If(product.DrawingFile, "").Trim().EndsWith(".dxf", StringComparison.OrdinalIgnoreCase) Then
                sourceDxfPath = AppPaths.ResolveDrawingFilePath(product.DrawingFile)
                sourceFileName = Path.GetFileName(product.DrawingFile)
            Else
                MessageBox.Show(
                    "Seçili ürünün kayıtlı teknik resmi DXF değil." & Environment.NewLine &
                    "DXF'den ölçü almak için Ürün / Teknik Resim Yönetimi ekranından bu ürüne DXF teknik resim yükleyiniz.",
                    "Kayıtlı DXF yok", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            If Not File.Exists(sourceDxfPath) Then
                Throw New FileNotFoundException("Kayıtlı DXF teknik resim dosyası bulunamadı.", sourceDxfPath)
            End If

            If importButton IsNot Nothing Then
                importButton.Enabled = False
                importButton.Text = "Kayıtlı DXF okunuyor..."
            End If
            UseWaitCursor = True

            Dim extraction = Await Threading.Tasks.Task.Run(
                Function() DxfDimensionImportService.ExtractDimensions(sourceDxfPath))

            ImportExtractedCadDimensions(product, extraction, sourceFileName, "DXF")
        Catch ex As Exception
            ErrorLogService.Log("FrmControlPointAdmin.ImportDxfDimensions", ex)
            MessageBox.Show(ex.Message, "DXF ölçü aktarımı başarısız", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            UseWaitCursor = False
            TempFileService.TryDeleteTempPdf(tempDxfPath)
            If importButton IsNot Nothing Then
                importButton.Enabled = True
                importButton.Text = "DXF'den Ölçüleri Al"
            End If
        End Try
    End Sub

    Private Sub ImportExtractedCadDimensions(product As ProductInfo,
                                             extraction As CadDimensionExtractionResult,
                                             sourceFileName As String,
                                             sourceKindText As String)
        If extraction.Candidates.Count = 0 Then
            MessageBox.Show(
                sourceKindText & " dosyasında gerçek DIMENSION nesnesi bulunamadı." & Environment.NewLine &
                "Yalnızca CAD/DXF içindeki DIMENSION nesneleri ölçü olarak tanınır. Patlatılmış çizgi veya düz TEXT/MTEXT ölçü yazıları otomatik alınmaz.",
                "Ölçü bulunamadı", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        PrepareCadCandidates(extraction.Candidates, product)

        Using preview As New FrmCadDimensionPreview(extraction.Candidates, sourceFileName, extraction.SourceDrawingPath)
            If preview.ShowDialog(Me) <> DialogResult.OK Then Return

            Dim selectedCandidates = preview.SelectedCandidates
            Dim controlPoints = selectedCandidates.
                Select(
                    Function(candidate) New ControlPoint With {
                        .TrCode = product.TrCode,
                        .DrawingRev = product.DrawingRev,
                        .DrawingScope = product.DrawingScope,
                        .MeasureId = candidate.SuggestedMeasureId,
                        .MeasureName = candidate.MeasureName,
                        .Nominal = candidate.Nominal,
                        .LowerTol = -Math.Abs(candidate.LowerTolerance),
                        .UpperTol = Math.Abs(candidate.UpperTolerance),
                        .LowerLimit = candidate.Nominal - Math.Abs(candidate.LowerTolerance),
                        .UpperLimit = candidate.Nominal + Math.Abs(candidate.UpperTolerance),
                        .PageNo = candidate.PageNo,
                        .XPercent = candidate.XPercent,
                        .YPercent = candidate.YPercent,
                        .Unit = candidate.Unit,
                        .IsMandatory = "YES",
                        .MeasurementGroup = "Genel",
                        .SampleFrequency = "Her Kontrol",
                        .IsCritical = "NO",
                        .SortNo = candidate.SortNo,
                        .IsActive = "YES"
                    }).
                ToList()

            DataService.SaveControlPointsBulk(controlPoints)
            AuditService.Log(
                "CONTROL_POINT_CAD_IMPORT",
                product.TrCode,
                product.DrawingRev,
                $"File={sourceFileName}; Imported={controlPoints.Count}; Tool={extraction.AutoCadToolPath}")

            LoadGrid()
            ClearInputs()
            LoadPdfToViewer(False)
            MessageBox.Show(
                $"{controlPoints.Count} kontrol ölçüsü {sourceKindText} çiziminden aktarıldı." & Environment.NewLine &
                "X/Y konumlarını teknik resim üzerinde kontrol etmeniz önerilir.",
                "Aktarım tamamlandı", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End Using
    End Sub

    Private Sub PrepareCadCandidates(candidates As List(Of CadDimensionCandidate), product As ProductInfo)
        Dim existingPoints = DataService.GetControlPoints(product.TrCode, product.DrawingRev, False, product.DrawingScope)
        Dim prefix = BuildMeasureIdPrefix(product)
        Dim maxMeasureNo As Integer = 0
        For Each point In existingPoints
            If Not point.MeasureId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then Continue For
            Dim numberText = point.MeasureId.Substring(prefix.Length)
            Dim numberValue As Integer
            If Integer.TryParse(numberText, numberValue) AndAlso numberValue > maxMeasureNo Then maxMeasureNo = numberValue
        Next

        Dim nextSortNo = If(existingPoints.Count = 0, 1, existingPoints.Max(Function(point) point.SortNo) + 1)
        Dim layoutPages = candidates.
            Select(Function(candidate) If(candidate.LayoutName, "").Trim()).
            Where(Function(layout) layout <> "").
            Distinct(StringComparer.OrdinalIgnoreCase).
            OrderBy(Function(layout) If(String.Equals(layout, "Model", StringComparison.OrdinalIgnoreCase), 0, 1)).
            ThenBy(Function(layout) layout, StringComparer.OrdinalIgnoreCase).
            Select(Function(layout, index) New With {.Layout = layout, .Page = index + 1}).
            ToDictionary(Function(item) item.Layout, Function(item) item.Page, StringComparer.OrdinalIgnoreCase)

        candidates.Sort(
            Function(left, right)
                Dim layoutCompare = StringComparer.OrdinalIgnoreCase.Compare(left.LayoutName, right.LayoutName)
                If layoutCompare <> 0 Then Return layoutCompare
                Dim yCompare = left.YPercent.CompareTo(right.YPercent)
                If yCompare <> 0 Then Return yCompare
                Return left.XPercent.CompareTo(right.XPercent)
            End Function)

        For index As Integer = 0 To candidates.Count - 1
            Dim candidate = candidates(index)
            candidate.SuggestedMeasureId = prefix & (maxMeasureNo + index + 1).ToString("000")
            candidate.SortNo = nextSortNo + index
            candidate.PageNo = 1
            If Not String.IsNullOrWhiteSpace(candidate.LayoutName) AndAlso layoutPages.ContainsKey(candidate.LayoutName) Then
                candidate.PageNo = layoutPages(candidate.LayoutName)
            End If

            Dim similarExists = existingPoints.Any(
                Function(point)
                    Return Math.Abs(point.Nominal - candidate.Nominal) <= 0.00001D AndAlso
                           point.XPercent > 0D AndAlso point.YPercent > 0D AndAlso
                           Math.Abs(point.XPercent - candidate.XPercent) <= 1.5D AndAlso
                           Math.Abs(point.YPercent - candidate.YPercent) <= 1.5D
                End Function)
            If similarExists Then
                candidate.IsSelected = False
                candidate.WarningText = AppendWarning(candidate.WarningText, "Benzer nominal ve konumda mevcut ölçü var.")
            End If
        Next
    End Sub

    Private Function AppendWarning(currentWarning As String, warning As String) As String
        If String.IsNullOrWhiteSpace(currentWarning) Then Return warning
        Return currentWarning.Trim() & " " & warning
    End Function

    Private Sub Save_Click(sender As Object, e As EventArgs)
        Try
            Dim p = SelectedProduct()
            If p Is Nothing Then Return
            If txtId.Text.Trim() = "" Then txtId.Text = BuildNextMeasureId()
            Dim nominal As Decimal
            Dim lowerTol As Decimal
            Dim upperTol As Decimal
            If Not NumberUtil.TryParseDecimal(txtNominal.Text, nominal) OrElse
               Not NumberUtil.TryParseDecimal(txtLowerTol.Text, lowerTol) OrElse
               Not NumberUtil.TryParseDecimal(txtUpperTol.Text, upperTol) Then
                MessageBox.Show("Nominal, Alt Tolerans ve Üst Tolerans sayısal olmalıdır.", "Hatalı değer", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim sortNo As Integer = 0
            Integer.TryParse(txtSort.Text.Trim(), sortNo)
            Dim pageNo As Integer = 1
            Integer.TryParse(txtPage.Text.Trim(), pageNo)
            If pageNo <= 0 Then pageNo = 1

            Dim xPct As Decimal = 0D
            Dim yPct As Decimal = 0D
            If txtX.Text.Trim() <> "" Then NumberUtil.TryParseDecimal(txtX.Text, xPct)
            If txtY.Text.Trim() <> "" Then NumberUtil.TryParseDecimal(txtY.Text, yPct)

            Dim cp As New ControlPoint With {
                .TrCode = p.TrCode,
                .DrawingRev = p.DrawingRev,
                .DrawingScope = p.DrawingScope,
                .MeasureId = txtId.Text.Trim(),
                .MeasureName = txtName.Text.Trim(),
                .Nominal = nominal,
                .LowerTol = -Math.Abs(lowerTol),
                .UpperTol = Math.Abs(upperTol),
                .LowerLimit = nominal - Math.Abs(lowerTol),
                .UpperLimit = nominal + Math.Abs(upperTol),
                .PageNo = pageNo,
                .XPercent = xPct,
                .YPercent = yPct,
                .Unit = If(txtUnit.Text.Trim() = "", "mm", txtUnit.Text.Trim()),
                .IsMandatory = If(chkMandatory.Checked, "YES", "NO"),
                .MeasurementGroup = If(cboMeasurementGroup.Text.Trim() = "", "Genel", cboMeasurementGroup.Text.Trim()),
                .SampleFrequency = If(cboSampleFrequency.Text.Trim() = "", "Her Kontrol", cboSampleFrequency.Text.Trim()),
                .IsCritical = If(chkCritical.Checked, "YES", "NO"),
                .SortNo = sortNo,
                .IsActive = "YES"
            }
            DataService.SaveControlPoint(cp)
            AuditService.Log("CONTROL_POINT_SAVE", cp.TrCode, cp.DrawingRev, cp.MeasureId & " - " & cp.MeasureName)
            LoadGrid()
            SelectControlPointRow(cp.MeasureId)
            ClearInputs()
            MessageBox.Show("Ölçü kaydedildi. Sağ alttaki listede gösterildi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub Revise_Click(sender As Object, e As EventArgs)
        Try
            Dim p = SelectedProduct()
            If p Is Nothing Then Return

            Dim original = GetCurrentControlPoint()
            If original Is Nothing Then
                MessageBox.Show("Revize etmek için listeden kayıtlı bir ölçü seçiniz.",
                                "Ölçü seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim nominal As Decimal
            Dim lowerTol As Decimal
            Dim upperTol As Decimal
            If Not NumberUtil.TryParseDecimal(txtNominal.Text, nominal) OrElse
               Not NumberUtil.TryParseDecimal(txtLowerTol.Text, lowerTol) OrElse
               Not NumberUtil.TryParseDecimal(txtUpperTol.Text, upperTol) Then
                MessageBox.Show("Nominal, Alt Tolerans ve Üst Tolerans sayısal olmalıdır.", "Hatalı değer", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim reason = Microsoft.VisualBasic.Interaction.InputBox(
                "Bu kontrol ölçüsü neden revize ediliyor?" & Environment.NewLine &
                "Örn: tolerans değişti, balon konumu düzeltildi, ölçü adı güncellendi.",
                "Kontrol ölçüsü revizyon nedeni",
                "")
            reason = If(reason, "").Trim()
            If reason = "" Then
                MessageBox.Show("Revizyon nedeni yazılmadan yeni versiyon oluşturulmadı.",
                                "Revizyon iptal", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim sortNo As Integer = 0
            Integer.TryParse(txtSort.Text.Trim(), sortNo)
            Dim pageNo As Integer = 1
            Integer.TryParse(txtPage.Text.Trim(), pageNo)
            If pageNo <= 0 Then pageNo = 1

            Dim xPct As Decimal = 0D
            Dim yPct As Decimal = 0D
            If txtX.Text.Trim() <> "" Then NumberUtil.TryParseDecimal(txtX.Text, xPct)
            If txtY.Text.Trim() <> "" Then NumberUtil.TryParseDecimal(txtY.Text, yPct)

            Dim revised As New ControlPoint With {
                .TrCode = p.TrCode,
                .DrawingRev = p.DrawingRev,
                .DrawingScope = p.DrawingScope,
                .MeasureId = original.MeasureId,
                .MeasureName = txtName.Text.Trim(),
                .Nominal = nominal,
                .LowerTol = -Math.Abs(lowerTol),
                .UpperTol = Math.Abs(upperTol),
                .LowerLimit = nominal - Math.Abs(lowerTol),
                .UpperLimit = nominal + Math.Abs(upperTol),
                .PageNo = pageNo,
                .XPercent = xPct,
                .YPercent = yPct,
                .Unit = If(txtUnit.Text.Trim() = "", "mm", txtUnit.Text.Trim()),
                .IsMandatory = If(chkMandatory.Checked, "YES", "NO"),
                .MeasurementGroup = If(cboMeasurementGroup.Text.Trim() = "", "Genel", cboMeasurementGroup.Text.Trim()),
                .SampleFrequency = If(cboSampleFrequency.Text.Trim() = "", "Her Kontrol", cboSampleFrequency.Text.Trim()),
                .IsCritical = If(chkCritical.Checked, "YES", "NO"),
                .SortNo = sortNo,
                .IsActive = "YES"
            }

            Dim created = DataService.ReviseControlPoint(p.TrCode, p.DrawingRev, original.MeasureId, p.DrawingScope, revised, reason)
            AuditService.Log("CONTROL_POINT_REVISE", p.TrCode, p.DrawingRev, original.MeasureId & " -> " & created.MeasureId & " / V" & created.MeasureVersion.ToString())
            LoadGrid()
            SelectControlPointRow(created.MeasureId)
            LoadControlPointFromSelectedRow()
            LoadPdfToViewer(False)
            MessageBox.Show("Ölçü revize edildi." & Environment.NewLine &
                            "Eski ölçü pasif yapıldı, yeni versiyon aktif oluşturuldu." & Environment.NewLine &
                            "Yeni Ölçü No: " & created.MeasureId & "   |   SPC: " & created.SpcKey & "   |   V" & created.MeasureVersion.ToString(),
                            "Revizyon tamamlandı", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            ErrorLogService.Log("FrmControlPointAdmin.Revise", ex)
            MessageBox.Show(ex.Message, "Revizyon hatası", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Function GetCurrentControlPoint() As ControlPoint
        If grid.CurrentRow IsNot Nothing Then
            Dim selected = TryCast(grid.CurrentRow.Tag, ControlPoint)
            If selected IsNot Nothing AndAlso
               (txtId.Text.Trim() = "" OrElse String.Equals(selected.MeasureId, txtId.Text.Trim(), StringComparison.OrdinalIgnoreCase)) Then
                Return selected
            End If
        End If

        Dim measureId = txtId.Text.Trim()
        If measureId = "" Then Return Nothing
        For Each row As DataGridViewRow In grid.Rows
            Dim cp = TryCast(row.Tag, ControlPoint)
            If cp IsNot Nothing AndAlso String.Equals(cp.MeasureId, measureId, StringComparison.OrdinalIgnoreCase) Then Return cp
        Next
        Return Nothing
    End Function

    Private Sub Passive_Click(sender As Object, e As EventArgs)
        Try
            Dim p = SelectedProduct()
            If p Is Nothing OrElse txtId.Text.Trim() = "" Then Return
            DataService.SetControlPointPassive(p.TrCode, p.DrawingRev, txtId.Text.Trim(), p.DrawingScope)
            AuditService.Log("CONTROL_POINT_PASSIVE", p.TrCode, p.DrawingRev, txtId.Text.Trim())
            LoadGrid()
            ClearInputs()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub Delete_Click(sender As Object, e As EventArgs)
        Try
            Dim p = SelectedProduct()
            Dim measureId = txtId.Text.Trim()
            If p Is Nothing OrElse measureId = "" Then
                MessageBox.Show("Silmek için listeden kayıtlı bir ölçü seçiniz.",
                                "Ölçü seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim answer = MessageBox.Show(
                "Seçili ölçü daha önce hiçbir ölçümde kullanılmadıysa kalıcı olarak silinecektir." & Environment.NewLine & Environment.NewLine &
                "Ölçü No: " & measureId & Environment.NewLine &
                "Ölçü Adı: " & If(txtName.Text.Trim() = "", "-", txtName.Text.Trim()) & Environment.NewLine & Environment.NewLine &
                "Devam etmek istiyor musunuz?",
                "Kontrol ölçüsünü sil",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning)
            If answer <> DialogResult.Yes Then Return

            DataService.DeleteUnusedControlPoint(p.TrCode, p.DrawingRev, measureId, p.DrawingScope)
            AuditService.Log("CONTROL_POINT_DELETE_UNUSED", p.TrCode, p.DrawingRev, measureId & " - " & txtName.Text.Trim())
            LoadGrid()
            ClearInputs()
            LoadPdfToViewer(False)
            MessageBox.Show("Kontrol ölçüsü kalıcı olarak silindi.",
                            "Ölçü silindi", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As InvalidOperationException
            MessageBox.Show(ex.Message, "Ölçü silinemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        Catch ex As Exception
            ErrorLogService.Log("FrmControlPointAdmin.DeleteControlPoint", ex)
            MessageBox.Show(ex.Message, "Silme hatası", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub MoveSelectedControlPointOrder(direction As Integer)
        Try
            Dim p = SelectedProduct()
            If p Is Nothing OrElse grid.CurrentRow Is Nothing Then Return

            Dim selectedPoint = TryCast(grid.CurrentRow.Tag, ControlPoint)
            If selectedPoint Is Nothing Then Return

            Dim orderedPoints = DataService.GetControlPoints(p.TrCode, p.DrawingRev, False, p.DrawingScope).
                OrderBy(Function(point) If(point.SortNo > 0, point.SortNo, Integer.MaxValue)).
                ThenBy(Function(point) point.MeasureId).
                ToList()

            If orderedPoints.Count <= 1 Then Return

            Dim currentIndex = orderedPoints.FindIndex(
                Function(point) String.Equals(point.MeasureId, selectedPoint.MeasureId, StringComparison.OrdinalIgnoreCase))
            If currentIndex < 0 Then Return

            Dim targetIndex = currentIndex + Math.Sign(direction)
            If targetIndex < 0 OrElse targetIndex >= orderedPoints.Count Then Return

            Dim movingPoint = orderedPoints(currentIndex)
            orderedPoints.RemoveAt(currentIndex)
            orderedPoints.Insert(targetIndex, movingPoint)

            For index As Integer = 0 To orderedPoints.Count - 1
                orderedPoints(index).SortNo = index + 1
            Next

            DataService.UpdateControlPointSortNos(orderedPoints)
            AuditService.Log("CONTROL_POINT_REORDER", p.TrCode, p.DrawingRev, movingPoint.MeasureId & " -> Sıra " & movingPoint.SortNo.ToString())

            LoadGrid()
            SelectControlPointRow(movingPoint.MeasureId)
            LoadControlPointFromSelectedRow()
            LoadPdfToViewer(False)
        Catch ex As Exception
            ErrorLogService.Log("FrmControlPointAdmin.MoveSelectedControlPointOrder", ex)
            MessageBox.Show(ex.Message, "Sıralama değiştirilemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub SpcHistory_Click(sender As Object, e As EventArgs)
        Try
            Dim cp = GetCurrentControlPoint()
            If cp Is Nothing Then
                MessageBox.Show("SPC geçmişini görmek için listeden bir kontrol ölçüsü seçiniz.",
                                "Ölçü seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Using frm As New FrmControlPointSpcHistory(cp)
                frm.ShowDialog(Me)
            End Using
        Catch ex As Exception
            ErrorLogService.Log("FrmControlPointAdmin.SpcHistory", ex)
            MessageBox.Show(ex.Message, "SPC geçmişi açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub Grid_Click(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 Then Return
        LoadControlPointFromRow(e.RowIndex)
    End Sub

    Private Sub Grid_DoubleClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 Then Return
        LoadControlPointFromRow(e.RowIndex)
    End Sub

    Private Sub LoadControlPointFromRow(rowIndex As Integer)
        If rowIndex < 0 OrElse rowIndex >= grid.Rows.Count Then Return
        Dim cp = TryCast(grid.Rows(rowIndex).Tag, ControlPoint)
        If cp Is Nothing Then Return
        txtId.Text = cp.MeasureId
        txtName.Text = cp.MeasureName
        txtNominal.Text = NumberUtil.DecToCsv(cp.Nominal)
        txtLowerTol.Text = NumberUtil.DecToCsv(cp.LowerTol)
        txtUpperTol.Text = NumberUtil.DecToCsv(cp.UpperTol)
        txtUnit.Text = cp.Unit
        txtSort.Text = cp.SortNo.ToString()
        txtPage.Text = cp.PageNo.ToString()
        txtX.Text = If(cp.XPercent = 0D, "", NumberUtil.DecToCsv(cp.XPercent))
        txtY.Text = If(cp.YPercent = 0D, "", NumberUtil.DecToCsv(cp.YPercent))
        cboMeasurementGroup.Text = If(String.IsNullOrWhiteSpace(cp.MeasurementGroup), "Genel", cp.MeasurementGroup)
        cboSampleFrequency.Text = If(String.IsNullOrWhiteSpace(cp.SampleFrequency), "Her Kontrol", cp.SampleFrequency)
        chkMandatory.Checked = String.Equals(cp.IsMandatory, "YES", StringComparison.OrdinalIgnoreCase)
        chkCritical.Checked = String.Equals(cp.IsCritical, "YES", StringComparison.OrdinalIgnoreCase)
        HighlightControlPointOnPdf(cp)
    End Sub

    Private Sub LoadControlPointFromSelectedRow()
        If grid.CurrentRow Is Nothing Then Return
        LoadControlPointFromRow(grid.CurrentRow.Index)
    End Sub

    Private Sub HighlightControlPointOnPdf(cp As ControlPoint)
        If cp Is Nothing Then Return

        If cp.XPercent > 0D AndAlso cp.YPercent > 0D Then
            If pdfViewer.CoreWebView2 Is Nothing OrElse String.IsNullOrWhiteSpace(currentTempHtml) OrElse Not File.Exists(currentTempHtml) Then
                pendingHighlightMeasureId = cp.MeasureId
                lblPdfInfo.Text = "Teknik resim açılıyor. Seçili ölçü noktası gösterilecek: " & cp.MeasureId
                LoadPdfToViewer(False)
            Else
                pendingHighlightMeasureId = ""
                ExecuteHighlightScript(cp)
            End If

            lblPdfInfo.Text = "Seçili ölçü noktası: " & cp.MeasureId & "   |   X %: " & NumberUtil.DecToCsv(cp.XPercent) & "   Y %: " & NumberUtil.DecToCsv(cp.YPercent)
        Else
            ExecutePdfScript("if(window.clearControlHighlight){window.clearControlHighlight();}")
            lblPdfInfo.Text = "Seçili ölçüde X/Y konumu tanımlı değil: " & cp.MeasureId
        End If
    End Sub

    Private Sub ExecuteHighlightScript(cp As ControlPoint)
        If cp Is Nothing Then Return
        Dim xText = NumberUtil.DecToCsv(cp.XPercent)
        Dim yText = NumberUtil.DecToCsv(cp.YPercent)
        Dim titleText = cp.MeasureId & " | " & cp.MeasureName & " | X=" & xText & " Y=" & yText
        Dim script As String =
            "if(window.showSelectedPoint){window.showSelectedPoint('" & JsString(cp.MeasureId) & "'," & xText & "," & yText & ",'" & JsString(titleText) & "');}" &
            "else if(window.highlightControlPoint){window.highlightControlPoint('" & JsString(cp.MeasureId) & "');}"
        ExecutePdfScript(script)
    End Sub

    Private Sub SelectControlPointRow(measureId As String)
        For Each row As DataGridViewRow In grid.Rows
            Dim cp = TryCast(row.Tag, ControlPoint)
            If cp Is Nothing Then Continue For
            If String.Equals(cp.MeasureId, measureId, StringComparison.OrdinalIgnoreCase) Then
                row.Selected = True
                grid.CurrentCell = row.Cells(0)
                Exit For
            End If
        Next
    End Sub

    Private Sub ClearInputs()
        txtId.Text = BuildNextMeasureId()
        txtName.Clear()
        txtNominal.Clear()
        txtLowerTol.Clear()
        txtUpperTol.Clear()
        txtUnit.Text = "mm"
        txtSort.Text = GetNextSortNo().ToString()
        txtPage.Text = "1"
        txtX.Clear()
        txtY.Clear()
        cboMeasurementGroup.Text = "Genel"
        cboSampleFrequency.Text = "Her Kontrol"
        chkMandatory.Checked = True
        chkCritical.Checked = False
        pendingHighlightMeasureId = ""
        ExecutePdfScript("if(window.clearControlHighlight){window.clearControlHighlight();}")
    End Sub

    Private Function JsString(value As String) As String
        If value Is Nothing Then Return ""
        Return value.Replace("\", "\\").Replace("'", "\'").Replace(vbCr, "").Replace(vbLf, " ")
    End Function

    Private Sub FrmControlPointAdmin_FormClosed(sender As Object, e As FormClosedEventArgs)
        Try
            pdfViewer.Dispose()
        Catch ex As Exception
            ErrorLogService.Log("FrmControlPointAdmin.FormClosed.DisposeViewer", ex)
        End Try
        CleanupCurrentPdfFiles()
    End Sub

    Private Sub CleanupCurrentPdfFiles()
        TempFileService.TryDeleteTempPdf(currentTempPdf)
        TempFileService.TryDeleteTempPdf(currentTempPng)
        TempFileService.TryDeleteTempPdf(currentTempHtml)
        currentTempPdf = ""
        currentTempPng = ""
        currentTempHtml = ""
    End Sub
End Class
