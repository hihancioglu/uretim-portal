Imports System.Data
Imports System.Drawing
Imports System.Globalization
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmProductionTicketEntry
    Inherits Form

    Private ReadOnly cboProduct As New ComboBox()
    Private ReadOnly txtProductFilter As New TextBox()
    Private ReadOnly txtMachineNo As New TextBox()
    Private ReadOnly cboMoldCode As New ComboBox()
    Private ReadOnly txtRawMaterial As New TextBox()
    Private ReadOnly txtWorkOrderNo As New TextBox()
    Private ReadOnly cboBindingReason As New ComboBox()
    Private ReadOnly txtMachineChangeReason As New TextBox()
    Private ReadOnly lblLastMoldInfo As New Label()
    Private ReadOnly txtNote As New TextBox()
    Private ReadOnly txtFinishNote As New TextBox()
    Private ReadOnly lblProductInfo As New Label()
    Private ReadOnly lblMoldWarning As New Label()
    Private ReadOnly btnOpenMoldTickets As New Button()
    Private ReadOnly gridActiveBindings As New DataGridView()
    Private ReadOnly initialTrCode As String
    Private ReadOnly initialMoldCode As String
    Private ReadOnly initialMachineNo As String
    Private ReadOnly initialStartNote As String
    Private allProducts As New List(Of ProductInfo)()
    Private suppressScrapAwarenessPopup As Boolean = True
    Private lastShownScrapAwarenessKey As String = ""

    Public Sub New(Optional initialTrCode As String = "", Optional initialMoldCode As String = "", Optional initialMachineNo As String = "", Optional initialStartNote As String = "")
        AuthorizationService.Require(AppState.CanOpenProductionBinding, "Kalip Baglama Takibi")
        AppIconService.Apply(Me)
        Me.initialTrCode = If(initialTrCode, "").Trim()
        Me.initialMoldCode = If(initialMoldCode, "").Trim()
        Me.initialMachineNo = If(initialMachineNo, "").Trim()
        Me.initialStartNote = If(initialStartNote, "").Trim()
        Text = "Kalıp Bağlama Takibi"
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        MinimumSize = New Size(760, 560)
        AutoScroll = False
        BackColor = Color.WhiteSmoke

        Dim main As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .BackColor = Color.WhiteSmoke,
            .Padding = New Padding(10)
        }
        main.RowStyles.Add(New RowStyle(SizeType.Absolute, 132.0F))
        main.RowStyles.Add(New RowStyle(SizeType.Absolute, 414.0F))
        main.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        main.RowStyles.Add(New RowStyle(SizeType.Absolute, 70.0F))
        Controls.Add(main)

        Dim productSection = BuildProductSection()
        Dim bindingSection = BuildBindingSection()
        Dim activeBindingsSection = BuildActiveBindingsSection()
        Dim bottomActions = BuildBottomActions()

        main.Controls.Add(productSection, 0, 0)
        main.Controls.Add(bindingSection, 0, 1)
        main.Controls.Add(activeBindingsSection, 0, 2)
        main.Controls.Add(bottomActions, 0, 3)

        Dim bottomFlow = bottomActions.Controls.OfType(Of FlowLayoutPanel)().FirstOrDefault()
        Dim isAdjustingLayout As Boolean = False
        Dim applyResponsiveLayout As Action =
            Sub()
                If isAdjustingLayout OrElse main.IsDisposed Then Return

                isAdjustingLayout = True
                Try
                    Dim dpiScale = Math.Max(1.0R, DeviceDpi / 96.0R)
                    Dim availableWidth = Math.Max(480, main.ClientSize.Width - main.Padding.Horizontal)
                    Dim availableHeight = Math.Max(360, main.ClientSize.Height - main.Padding.Vertical)

                    Dim bottomHeight = Math.Max(54, CInt(Math.Round(58 * dpiScale)))
                    If bottomFlow IsNot Nothing Then
                        bottomFlow.AutoScroll = False
                        bottomHeight = Math.Max(
                            bottomHeight,
                            bottomFlow.GetPreferredSize(New Size(availableWidth, 0)).Height + 4)
                    End If

                    Dim productPreferredHeight = productSection.GetPreferredSize(New Size(availableWidth, 0)).Height + productSection.Margin.Vertical
                    Dim productHeight = Math.Max(
                        CInt(Math.Round(128 * dpiScale)),
                        Math.Min(productPreferredHeight + CInt(Math.Round(16 * dpiScale)), CInt(Math.Round(154 * dpiScale))))

                    Dim minimumActiveListHeight = Math.Max(46, CInt(Math.Round(60 * dpiScale)))
                    Dim maximumBindingHeight = Math.Max(
                        240,
                        availableHeight - productHeight - bottomHeight - minimumActiveListHeight)
                    Dim preferredBindingHeight = Math.Max(
                        280,
                        Math.Min(CInt(Math.Round(414 * dpiScale)), bindingSection.GetPreferredSize(New Size(availableWidth, 0)).Height + bindingSection.Margin.Vertical))
                    Dim bindingHeight = Math.Min(preferredBindingHeight, maximumBindingHeight)

                    If productHeight + bindingHeight + bottomHeight + minimumActiveListHeight > availableHeight Then
                        Dim overflow = productHeight + bindingHeight + bottomHeight + minimumActiveListHeight - availableHeight
                        bindingHeight = Math.Max(220, bindingHeight - overflow)
                    End If

                    main.RowStyles(0).Height = productHeight
                    main.RowStyles(1).Height = bindingHeight
                    main.RowStyles(3).Height = bottomHeight
                    main.PerformLayout()
                Finally
                    isAdjustingLayout = False
                End Try
            End Sub

        AddHandler ClientSizeChanged, Sub() applyResponsiveLayout.Invoke()
        AddHandler Shown,
            Sub()
                applyResponsiveLayout.Invoke()
                If HasInitialBindingValues() Then
                    BeginInvoke(New MethodInvoker(Sub() ShowScrapAwarenessForSelectedProduct(False)))
                End If
            End Sub
        AddHandler DpiChanged,
            Sub()
                If IsHandleCreated AndAlso Not IsDisposed Then
                    BeginInvoke(New MethodInvoker(Sub() applyResponsiveLayout.Invoke()))
                End If
            End Sub
        applyResponsiveLayout.Invoke()

        LoadProducts()
        ClearInputs(True)
        If HasInitialBindingValues() Then ApplyInitialBindingValues()
        LoadActiveBindings()
        suppressScrapAwarenessPopup = False
    End Sub

    Private Function HasInitialBindingValues() As Boolean
        Return initialTrCode <> "" OrElse
               initialMoldCode <> "" OrElse
               initialMachineNo <> "" OrElse
               initialStartNote <> ""
    End Function

    Private Function BuildProductSection() As Control
        Dim grpProduct = CreateSectionGroup("1. Ürün / Teknik Resim Bilgisi")

        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 7,
            .RowCount = 2,
            .Padding = New Padding(8, 5, 8, 5),
            .BackColor = Color.White
        }
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 100.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 40.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 125.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 80.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 30.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 8.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 30.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        grpProduct.Controls.Add(layout)

        Dim lblProduct As Label = CreateFieldLabel("TR / Revizyon")
        layout.Controls.Add(lblProduct, 0, 0)

        cboProduct.Dock = DockStyle.Fill
        cboProduct.DropDownStyle = ComboBoxStyle.DropDownList
        AddHandler cboProduct.SelectedIndexChanged, AddressOf ProductChanged
        AddHandler cboProduct.SelectionChangeCommitted,
            Sub()
                If Not suppressScrapAwarenessPopup Then
                    ShowScrapAwarenessForSelectedProduct(False)
                End If
            End Sub
        layout.Controls.Add(cboProduct, 1, 0)

        Dim btnViewDrawing As New Button() With {.Text = "Teknik Resim", .Dock = DockStyle.Fill, .Height = 30}
        btnViewDrawing.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        AddHandler btnViewDrawing.Click, AddressOf ViewDrawing_Click
        layout.Controls.Add(btnViewDrawing, 2, 0)

        Dim lblFilter As Label = CreateFieldLabel("TR Filtre")
        layout.Controls.Add(lblFilter, 3, 0)

        txtProductFilter.Dock = DockStyle.Fill
        txtProductFilter.PlaceholderText = "TR / revizyon / ürün"
        AddHandler txtProductFilter.TextChanged, Sub() ApplyProductFilter()
        layout.Controls.Add(txtProductFilter, 4, 0)

        lblProductInfo.Dock = DockStyle.Fill
        lblProductInfo.Margin = New Padding(0, 2, 0, 0)
        lblProductInfo.ForeColor = Color.FromArgb(70, 70, 70)
        lblProductInfo.Font = New Font("Segoe UI", 9.0F, FontStyle.Regular)
        lblProductInfo.BackColor = Color.Transparent
        lblProductInfo.AutoEllipsis = True
        lblProductInfo.TextAlign = ContentAlignment.MiddleLeft
        layout.SetColumnSpan(lblProductInfo, 7)
        layout.Controls.Add(lblProductInfo, 0, 1)

        Return grpProduct
    End Function

    Private Function BuildBindingSection() As Control
        Dim wrap As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 1,
            .BackColor = Color.WhiteSmoke,
            .Margin = New Padding(0),
            .Padding = New Padding(0)
        }
        wrap.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        wrap.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))

        wrap.Controls.Add(BuildBindingEntryGroup(), 0, 0)
        wrap.Controls.Add(BuildMoldStatusGroup(), 1, 0)
        Return wrap
    End Function

    Private Function BuildBindingEntryGroup() As Control
        Dim grpBinding = CreateSectionGroup("2. Yeni Kalıp Bağlama Başlat")

        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 4,
            .RowCount = 5,
            .Padding = New Padding(8, 5, 8, 7),
            .BackColor = Color.White
        }
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 125.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 44.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 125.0F))
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 56.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 40.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        grpBinding.Controls.Add(layout)

        layout.Controls.Add(CreateFieldLabel("Makine"), 0, 0)
        txtMachineNo.Dock = DockStyle.Fill
        txtMachineNo.Margin = New Padding(6)
        txtMachineNo.PlaceholderText = "Makine seçin..."
        AddHandler txtMachineNo.TextChanged, Sub() RefreshLastMoldInfo()
        layout.Controls.Add(txtMachineNo, 1, 0)

        layout.Controls.Add(CreateFieldLabelRight("Kalıp Kodu"), 2, 0)
        cboMoldCode.Dock = DockStyle.Fill
        cboMoldCode.Margin = New Padding(6)
        cboMoldCode.DropDownStyle = ComboBoxStyle.DropDown
        cboMoldCode.AutoCompleteMode = AutoCompleteMode.SuggestAppend
        cboMoldCode.AutoCompleteSource = AutoCompleteSource.ListItems
        AddHandler cboMoldCode.TextChanged, Sub()
                                                LoadOpenMoldWarnings()
                                                RefreshLastMoldInfo()
                                            End Sub
        layout.Controls.Add(cboMoldCode, 3, 0)

        layout.Controls.Add(CreateFieldLabel("Hammadde"), 0, 1)
        txtRawMaterial.Dock = DockStyle.Fill
        txtRawMaterial.Margin = New Padding(6)
        txtRawMaterial.PlaceholderText = "Hammadde seçin..."
        layout.SetColumnSpan(txtRawMaterial, 3)
        layout.Controls.Add(txtRawMaterial, 1, 1)

        layout.Controls.Add(CreateFieldLabel("İş Emri No"), 0, 2)
        txtWorkOrderNo.Dock = DockStyle.Fill
        txtWorkOrderNo.Margin = New Padding(6)
        txtWorkOrderNo.PlaceholderText = "İş emri no girin..."
        layout.Controls.Add(txtWorkOrderNo, 1, 2)

        layout.Controls.Add(CreateFieldLabelRight("Bağlama Nedeni"), 2, 2)
        cboBindingReason.Dock = DockStyle.Fill
        cboBindingReason.Margin = New Padding(6)
        cboBindingReason.DropDownStyle = ComboBoxStyle.DropDownList
        cboBindingReason.Items.AddRange({"NORMAL BAĞLAMA", "MAKİNE DEĞİŞİMİ", "MAKİNE ARIZASI", "PLAN DEĞİŞİKLİĞİ", "KALIP BAKIMI", "DENEME ÜRETİMİ", "DİĞER"})
        cboBindingReason.SelectedIndex = 0
        layout.Controls.Add(cboBindingReason, 3, 2)

        layout.Controls.Add(CreateFieldLabel("Makine Değişim Nedeni"), 0, 3)
        txtMachineChangeReason.Dock = DockStyle.Fill
        txtMachineChangeReason.Margin = New Padding(6)
        txtMachineChangeReason.PlaceholderText = "İsteğe bağlı: önceki makineden farklıysa açıklama girilebilir"
        layout.SetColumnSpan(txtMachineChangeReason, 3)
        layout.Controls.Add(txtMachineChangeReason, 1, 3)

        layout.Controls.Add(CreateFieldLabel("Başlangıç Notu"), 0, 4)
        txtNote.Dock = DockStyle.Fill
        txtNote.Margin = New Padding(6)
        txtNote.Multiline = True
        txtNote.ScrollBars = ScrollBars.Vertical
        txtNote.PlaceholderText = "Opsiyonel not girin..."
        layout.SetColumnSpan(txtNote, 3)
        layout.Controls.Add(txtNote, 1, 4)

        Return grpBinding
    End Function

    Private Function BuildMoldStatusGroup() As Control
        Dim grpStatus = CreateSectionGroup("Kalıp Durumu / Uyarılar")
        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 4,
            .Padding = New Padding(8, 7, 8, 7),
            .BackColor = Color.White
        }
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 68.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 60.0F))
        grpStatus.Controls.Add(layout)

        lblLastMoldInfo.Dock = DockStyle.Fill
        lblLastMoldInfo.Margin = New Padding(0, 0, 0, 6)
        lblLastMoldInfo.ForeColor = Color.FromArgb(50, 60, 90)
        lblLastMoldInfo.BackColor = Color.FromArgb(236, 243, 252)
        lblLastMoldInfo.BorderStyle = BorderStyle.FixedSingle
        lblLastMoldInfo.TextAlign = ContentAlignment.MiddleCenter
        lblLastMoldInfo.Padding = New Padding(8)
        lblLastMoldInfo.AutoEllipsis = True
        lblLastMoldInfo.Text = "Kalıp seçildiğinde son makine bilgisi burada gösterilir."
        layout.Controls.Add(lblLastMoldInfo, 0, 0)

        Dim warningPanel As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .BackColor = Color.White,
            .Padding = New Padding(0, 0, 0, 6)
        }
        warningPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        warningPanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 30.0F))
        layout.Controls.Add(warningPanel, 0, 1)

        lblMoldWarning.Dock = DockStyle.Fill
        lblMoldWarning.BackColor = Color.FromArgb(255, 245, 235)
        lblMoldWarning.ForeColor = Color.DarkRed
        lblMoldWarning.BorderStyle = BorderStyle.FixedSingle
        lblMoldWarning.TextAlign = ContentAlignment.MiddleLeft
        lblMoldWarning.Padding = New Padding(8)
        lblMoldWarning.AutoEllipsis = True
        lblMoldWarning.Visible = False
        warningPanel.Controls.Add(lblMoldWarning, 0, 0)

        btnOpenMoldTickets.Text = "Kalıp Ticketları"
        btnOpenMoldTickets.Dock = DockStyle.Left
        btnOpenMoldTickets.Width = 135
        btnOpenMoldTickets.Margin = New Padding(0, 3, 0, 0)
        btnOpenMoldTickets.Visible = False
        AddHandler btnOpenMoldTickets.Click, AddressOf OpenMoldTickets_Click
        warningPanel.Controls.Add(btnOpenMoldTickets, 0, 1)

        Dim lblRule As New Label() With {
            .Text = "Bilgi: Sökülen kalıp ayrıca takip edilmediği için aynı kalıp yeni bağlantı olarak başlatılabilir." & Environment.NewLine &
                    "Önceki makine bilgisi yalnızca uyarı amaçlı gösterilir; makine değişim nedeni isteğe bağlıdır.",
            .Dock = DockStyle.Fill,
            .BackColor = Color.FromArgb(250, 250, 250),
            .BorderStyle = BorderStyle.FixedSingle,
            .ForeColor = Color.FromArgb(70, 70, 70),
            .Padding = New Padding(8),
            .TextAlign = ContentAlignment.MiddleLeft,
            .AutoEllipsis = True,
            .Margin = New Padding(0)
        }
        layout.Controls.Add(lblRule, 0, 2)

        Return grpStatus
    End Function

    Private Function BuildActiveBindingsSection() As Control
        Dim grpActive = CreateSectionGroup("3. Devam Eden Kalıp Bağlamaları")
        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 4,
            .Padding = New Padding(8, 5, 8, 7),
            .BackColor = Color.White
        }
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 30.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 17.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))
        grpActive.Controls.Add(layout)

        Dim info As New Label() With {
            .Text = "Bitirmek için aşağıdaki listeden devam eden bağlama kaydını seçin.",
            .Dock = DockStyle.Fill,
            .BackColor = Color.FromArgb(247, 247, 247),
            .BorderStyle = BorderStyle.FixedSingle,
            .Padding = New Padding(8, 0, 0, 0),
            .TextAlign = ContentAlignment.MiddleLeft,
            .ForeColor = Color.FromArgb(70, 70, 70)
        }
        layout.Controls.Add(info, 0, 0)

        ConfigureActiveGrid()
        layout.Controls.Add(gridActiveBindings, 0, 1)

        Dim lblFinish As New Label() With {
            .Text = "Bitiş Notu",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.BottomLeft
        }
        layout.Controls.Add(lblFinish, 0, 2)

        txtFinishNote.Dock = DockStyle.Fill
        txtFinishNote.PlaceholderText = "Bitiş notu / üretime veya kaliteye aktarılacak açıklama"
        layout.Controls.Add(txtFinishNote, 0, 3)

        Return grpActive
    End Function

    Private Function BuildBottomActions() As Control
        Dim panel As New Panel() With {.Dock = DockStyle.Fill, .BackColor = Color.White, .BorderStyle = BorderStyle.FixedSingle, .Margin = New Padding(0)}

        Dim flow As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = True,
            .Padding = New Padding(10, 10, 10, 8),
            .AutoScroll = False,
            .BackColor = Color.White
        }
        panel.Controls.Add(flow)

        Dim btnStart As Button = CreateActionButton("Bağlamayı Başlat", Color.FromArgb(44, 101, 226), Color.White, 190)
        AddHandler btnStart.Click, AddressOf StartBinding_Click

        Dim btnFinish As Button = CreateActionButton("Bağlamayı Bitir + Kalite Ticket Aç", Color.FromArgb(224, 246, 227), Color.DarkGreen, 270)
        AddHandler btnFinish.Click, AddressOf FinishBinding_Click

        Dim btnMachineChange As Button = CreateActionButton("Makine Değişimi", Color.FromArgb(255, 239, 221), Color.FromArgb(190, 90, 0), 165)
        AddHandler btnMachineChange.Click,
            Sub(sender As Object, e As EventArgs)
                cboBindingReason.Text = "MAKİNE DEĞİŞİMİ"
                StartBinding_Click(sender, e)
            End Sub

        Dim btnClear As Button = CreateActionButton("Temizle", Color.White, Color.FromArgb(45, 45, 45), 130)
        AddHandler btnClear.Click, AddressOf Clear_Click

        flow.Controls.AddRange({btnStart, btnFinish, btnMachineChange, btnClear})
        Return panel
    End Function

    Private Function CreateSectionGroup(title As String) As GroupBox
        Return New GroupBox() With {
            .Text = title,
            .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .Padding = New Padding(6),
            .Margin = New Padding(0, 0, 8, 8),
            .BackColor = Color.White
        }
    End Function

    Private Function CreateFieldLabel(text As String) As Label
        Return New Label() With {
            .Text = text,
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Regular),
            .BackColor = Color.Transparent,
            .Margin = New Padding(0, 3, 10, 3)
        }
    End Function

    Private Function CreateFieldLabelRight(text As String) As Label
        Return New Label() With {
            .Text = text,
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleRight,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Regular),
            .BackColor = Color.Transparent,
            .Margin = New Padding(8, 3, 12, 3)
        }
    End Function

    Private Function CreateActionButton(text As String, backColor As Color, foreColor As Color, width As Integer) As Button
        Return New Button() With {
            .Text = text,
            .Width = width,
            .Height = 36,
            .Margin = New Padding(6, 0, 6, 0),
            .BackColor = backColor,
            .ForeColor = foreColor,
            .FlatStyle = FlatStyle.Flat,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .UseVisualStyleBackColor = False
        }
    End Function


    Private Sub ConfigureActiveGrid()
        gridActiveBindings.Dock = DockStyle.Fill
        gridActiveBindings.ReadOnly = True
        gridActiveBindings.AllowUserToAddRows = False
        gridActiveBindings.AllowUserToDeleteRows = False
        gridActiveBindings.MultiSelect = False
        gridActiveBindings.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        gridActiveBindings.AutoGenerateColumns = False
        gridActiveBindings.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        gridActiveBindings.RowHeadersVisible = False
        gridActiveBindings.BackgroundColor = Color.White
        gridActiveBindings.BorderStyle = BorderStyle.FixedSingle
        gridActiveBindings.GridColor = Color.Gainsboro
        gridActiveBindings.EnableHeadersVisualStyles = False
        gridActiveBindings.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 235, 235)
        gridActiveBindings.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        gridActiveBindings.DefaultCellStyle.BackColor = Color.White
        gridActiveBindings.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248)
        gridActiveBindings.RowTemplate.Height = 26
        gridActiveBindings.ScrollBars = ScrollBars.Vertical

        gridActiveBindings.Columns.Clear()
        gridActiveBindings.Columns.Add(MakeColumn("BindingId", "Bağlama No", 150))
        gridActiveBindings.Columns.Add(MakeColumn("StartedAt", "Başlangıç", 135))
        gridActiveBindings.Columns.Add(MakeColumn("StartedBy", "Başlatan", 110))
        gridActiveBindings.Columns.Add(MakeColumn("MachineNo", "Makine", 105))
        gridActiveBindings.Columns.Add(MakeColumn("PreviousMachineNo", "Önceki Mak.", 105))
        gridActiveBindings.Columns.Add(MakeColumn("MoldCode", "Kalıp", 100))
        gridActiveBindings.Columns.Add(MakeColumn("BindingReason", "Neden", 140))
        gridActiveBindings.Columns.Add(MakeColumn("TrCode", "TR", 90))
        gridActiveBindings.Columns.Add(MakeColumn("ProductName", "Ürün", 160))
        gridActiveBindings.Columns.Add(MakeColumn("WorkOrderNo", "İş Emri", 105))

        AddHandler gridActiveBindings.CellDoubleClick, AddressOf ActiveGrid_DoubleClick
    End Sub

    Private Function MakeColumn(name As String, header As String, width As Integer) As DataGridViewTextBoxColumn
        Return New DataGridViewTextBoxColumn() With {
            .Name = name,
            .DataPropertyName = name,
            .HeaderText = header,
            .Width = width,
            .MinimumWidth = 55,
            .AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            .FillWeight = CSng(width),
            .SortMode = DataGridViewColumnSortMode.Automatic
        }
    End Function

    Private Sub LoadProducts()
        allProducts = DataService.GetProducts(True)
        ApplyProductFilter()
    End Sub

    Private Sub ApplyInitialBindingValues()
        If initialTrCode = "" AndAlso initialMoldCode = "" AndAlso initialMachineNo = "" AndAlso initialStartNote = "" Then Return

        If initialTrCode <> "" Then
            txtProductFilter.Text = initialTrCode
            SelectProductByTr(initialTrCode)
        End If

        If initialMachineNo <> "" Then txtMachineNo.Text = initialMachineNo

        Dim p = SelectedProduct()

        If initialMoldCode <> "" Then
            If Not cboMoldCode.Items.Cast(Of Object)().Any(Function(x) String.Equals(Convert.ToString(x), initialMoldCode, StringComparison.OrdinalIgnoreCase)) Then
                cboMoldCode.Items.Add(initialMoldCode)
            End If

            cboMoldCode.Text = initialMoldCode
        End If

        If initialStartNote <> "" AndAlso txtNote.Text.Trim() = "" Then txtNote.Text = initialStartNote

        If p IsNot Nothing AndAlso txtRawMaterial.Text.Trim() = "" Then txtRawMaterial.Text = p.Material

        LoadOpenMoldWarnings()
        RefreshLastMoldInfo()
    End Sub

    Private Sub SelectProductByTr(trCode As String)
        trCode = If(trCode, "").Trim()
        If trCode = "" Then Return

        Dim wantedTr = NormalizeTrCodeForSelection(trCode)
        For i As Integer = 0 To cboProduct.Items.Count - 1
            Dim p = TryCast(cboProduct.Items(i), ProductInfo)
            If p IsNot Nothing AndAlso String.Equals(NormalizeTrCodeForSelection(p.TrCode), wantedTr, StringComparison.OrdinalIgnoreCase) Then
                cboProduct.SelectedIndex = i
                Return
            End If
        Next
    End Sub

    Private Shared Function NormalizeTrCodeForSelection(value As String) As String
        Dim text = If(value, "").Trim().ToUpperInvariant()
        Return text.Replace(" ", "").Replace("-", "").Replace("_", "").Replace("/", "")
    End Function

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
                                             Dim haystack = (p.TrCode & " " & p.DrawingRev & " " & p.ProductName & " " & p.Material & " " & p.ColorName & " " & p.MoldCode).ToUpperInvariant()
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
        Else
            lblProductInfo.Text = "Ürün bulunamadı. Ürün / Teknik Resim Yönetimi ekranından kayıt yapılmalıdır."
            ClearMoldAndRawMaterial()
            LoadOpenMoldWarnings()
            RefreshLastMoldInfo()
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

        Dim cleaned = rawText.Replace(vbCr, ";").
                              Replace(vbLf, ";").
                              Replace("/", ";").
                              Replace("\", ";").
                              Replace(",", ";").
                              Replace("|", ";")
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
        If p Is Nothing Then
            ClearMoldAndRawMaterial()
            LoadOpenMoldWarnings()
            RefreshLastMoldInfo()
            Return
        End If

        lblProductInfo.Text = $"Ürün: {If(p.ProductName = "", "-", p.ProductName)} | " &
                              $"Malzeme: {If(p.Material = "", "-", p.Material)} | " &
                              $"Renk: {If(p.ColorName = "", "-", p.ColorName)} | " &
                              $"Kalıp Kodu: {If(p.MoldCode = "", "-", p.MoldCode)} | " &
                              $"Plastik Kodu: {If(p.PlasticCode = "", "-", p.PlasticCode)} | " &
                              $"Kalıp Göz Adedi: {If(p.MoldCavityCount = "", "-", p.MoldCavityCount)}"

        FillMoldCodeComboFromProduct(p)
        txtRawMaterial.Text = p.Material

        LoadOpenMoldWarnings()
        RefreshLastMoldInfo()
    End Sub

    Private Sub ShowScrapAwarenessForSelectedProduct(force As Boolean)
        Try
            Dim product = SelectedProduct()
            If product Is Nothing Then Return
            Dim summary = ScrapAwarenessService.GetSummaryForProduct(product, 12)
            If summary Is Nothing OrElse Not summary.HasData Then Return

            Dim key = summary.ProductKey & "|" & summary.SourceFileName & "|" & summary.SourceSavedAt
            If Not force AndAlso String.Equals(lastShownScrapAwarenessKey, key, StringComparison.OrdinalIgnoreCase) Then Return
            lastShownScrapAwarenessKey = key

            Using dialog As New FrmScrapAwarenessPareto(summary)
                dialog.ShowDialog(Me)
            End Using
        Catch ex As Exception
            ErrorLogService.Log("FrmProductionTicketEntry.ShowScrapAwarenessForSelectedProduct", ex)
        End Try
    End Sub

    Private Shared Function ProductHasTechnicalDrawing(p As ProductInfo) As Boolean
        If p Is Nothing OrElse String.IsNullOrWhiteSpace(p.DrawingFile) Then Return False

        Try
            Return IO.File.Exists(IO.Path.Combine(AppPaths.DrawingsDir, p.DrawingFile))
        Catch ex As Exception
            ErrorLogService.Log(
                "FrmProductionTicketEntry.ProductHasTechnicalDrawing",
                ex,
                "DrawingFile=" & If(p.DrawingFile, ""))
            Return False
        End Try
    End Function

    Private Sub ClearMoldAndRawMaterial()
        cboMoldCode.Items.Clear()
        cboMoldCode.Text = ""
        txtRawMaterial.Clear()
    End Sub

    Private Sub ViewDrawing_Click(sender As Object, e As EventArgs)
        Try
            Dim p = SelectedProduct()
            If p Is Nothing Then
                MessageBox.Show("Önce TR / Revizyon seçiniz.", "Teknik resim", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            If String.IsNullOrWhiteSpace(p.DrawingFile) Then
                MessageBox.Show("Seçili ürün için teknik resim dosyası tanımlı değil.", "Teknik resim", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Using viewer As New FrmPdfViewer(p.DrawingFile, "Teknik Resim - " & p.TrCode & " / " & p.DrawingRev, False)
                viewer.ShowDialog(Me)
            End Using

            AuditService.Log("PRODUCTION_TICKET_DRAWING_VIEW", p.TrCode, p.DrawingRev, "Kalıp bağlama ekranından teknik resim açıldı.")
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Teknik resim açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub LoadOpenMoldWarnings()
        Try
            Dim moldCode = cboMoldCode.Text.Trim()
            Dim openTickets = DataService.GetOpenMoldTickets(moldCode)

            If openTickets.Count = 0 Then
                lblMoldWarning.Visible = False
                btnOpenMoldTickets.Visible = False
                Return
            End If

            Dim firstTicket = openTickets(0)
            lblMoldWarning.Text = "DİKKAT: Bu kalıpta " & openTickets.Count.ToString() & " açık kalıp ticket var." & Environment.NewLine &
                                  "Son sorun: " & DataService.GetValue(firstTicket, "ProblemType") & " - " & DataService.GetValue(firstTicket, "ProblemDescription")
            lblMoldWarning.Visible = True
            btnOpenMoldTickets.Visible = True
        Catch ex As Exception
            ErrorLogService.Log("FrmProductionTicketEntry.LoadOpenMoldWarnings", ex)
            lblMoldWarning.Visible = False
            btnOpenMoldTickets.Visible = False
        End Try
    End Sub

    Private Sub OpenMoldTickets_Click(sender As Object, e As EventArgs)
        Using f As New FrmMoldTicketManagement(cboMoldCode.Text.Trim())
            f.ShowDialog(Me)
        End Using
        LoadOpenMoldWarnings()
    End Sub

    Private Function ValidateRequiredInputs() As Boolean
        Dim p = SelectedProduct()
        If p Is Nothing Then
            MessageBox.Show("TR / Revizyon seçilmelidir.", "Eksik bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return False
        End If

        If txtMachineNo.Text.Trim() = "" Then
            MessageBox.Show("Makine bilgisi zorunludur.", "Eksik bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            txtMachineNo.Focus()
            Return False
        End If

        If cboMoldCode.Text.Trim() = "" Then
            MessageBox.Show("Kalıp Kodu zorunludur.", "Eksik bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            cboMoldCode.Focus()
            Return False
        End If

        If txtRawMaterial.Text.Trim() = "" Then
            MessageBox.Show("Hammadde bilgisi zorunludur.", "Eksik bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            txtRawMaterial.Focus()
            Return False
        End If

        If cboBindingReason.Text.Trim() = "" Then
            MessageBox.Show("Bağlama nedeni seçilmelidir.", "Eksik bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            cboBindingReason.Focus()
            Return False
        End If

        Return True
    End Function

    Private Function BuildBindingId() As String
        Return "KBG-" & DateTime.Now.ToString("yyyyMMdd-HHmmss") & "-" & Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant()
    End Function

    Private Function BuildQualityTicketId() As String
        Return "TCK-" & DateTime.Now.ToString("yyyyMMdd-HHmmss") & "-" & Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant()
    End Function

    Private Function LastCompletedBindingForMold(moldCode As String) As Dictionary(Of String, String)
        moldCode = If(moldCode, "").Trim()
        If moldCode = "" Then Return Nothing

        Return DataService.GetMoldBindingRecords().
            Where(Function(r) String.Equals(DataService.GetValue(r, "Status"), "COMPLETED", StringComparison.OrdinalIgnoreCase) AndAlso
                              MoldCodeMatches(DataService.GetValue(r, "MoldCode"), moldCode)).
            OrderByDescending(Function(r) GetDateSortValue(DataService.GetValue(r, "CompletedAt"))).
            FirstOrDefault()
    End Function

    Private Function ActiveBindingForMold(moldCode As String) As Dictionary(Of String, String)
        moldCode = If(moldCode, "").Trim()
        If moldCode = "" Then Return Nothing

        Return DataService.GetMoldBindingRecords().
            FirstOrDefault(Function(r) String.Equals(DataService.GetValue(r, "Status"), "STARTED", StringComparison.OrdinalIgnoreCase) AndAlso
                                       MoldCodeMatches(DataService.GetValue(r, "MoldCode"), moldCode))
    End Function

    Private Function MoldCodeMatches(recordMoldCode As String, selectedMoldCode As String) As Boolean
        Dim selectedCodes = ParseMoldCodes(selectedMoldCode)
        If selectedCodes.Count = 0 Then Return False

        Dim recordCodes = ParseMoldCodes(recordMoldCode)
        If recordCodes.Count = 0 Then Return False

        Return selectedCodes.Any(Function(selectedCode)
                                     Return recordCodes.Any(Function(recordCode) String.Equals(recordCode, selectedCode, StringComparison.OrdinalIgnoreCase))
                                 End Function)
    End Function

    Private Function GetDateSortValue(text As String) As DateTime
        Dim d As DateTime
        If DateTime.TryParseExact(text,
                                  "yyyy-MM-dd HH:mm:ss",
                                  CultureInfo.InvariantCulture,
                                  DateTimeStyles.None,
                                  d) OrElse DateTime.TryParse(text, d) Then
            Return d
        End If

        Return DateTime.MinValue
    End Function

    Private Function GetPreviousMachineNo(moldCode As String) As String
        Dim last = LastCompletedBindingForMold(moldCode)
        If last Is Nothing Then Return ""
        Return DataService.GetValue(last, "MachineNo")
    End Function

    Private Function IsDifferentFromPreviousMachine() As Boolean
        Dim previousMachine = GetPreviousMachineNo(cboMoldCode.Text)
        Dim currentMachine = txtMachineNo.Text.Trim()

        Return previousMachine.Trim() <> "" AndAlso
               currentMachine.Trim() <> "" AndAlso
               Not String.Equals(previousMachine, currentMachine, StringComparison.OrdinalIgnoreCase)
    End Function

    Private Sub RefreshLastMoldInfo()
        Try
            Dim lastBinding = LastCompletedBindingForMold(cboMoldCode.Text)
            Dim previousMachine = If(lastBinding Is Nothing, "", DataService.GetValue(lastBinding, "MachineNo"))
            Dim previousTrCode = If(lastBinding Is Nothing, "", DataService.GetValue(lastBinding, "TrCode"))
            Dim currentMachine = txtMachineNo.Text.Trim()

            If previousMachine.Trim() = "" Then
                lblLastMoldInfo.Text = "Bu kalıp için daha önce tamamlanmış bağlama kaydı bulunamadı."
                lblLastMoldInfo.ForeColor = Color.DimGray
                Return
            End If

            If currentMachine.Trim() = "" Then
                lblLastMoldInfo.Text = "Son bağlı olduğu makine: " & previousMachine &
                                       "    |    Son TR: " & If(previousTrCode.Trim() = "", "-", previousTrCode)
                lblLastMoldInfo.ForeColor = Color.DimGray
                Return
            End If

            If String.Equals(previousMachine, currentMachine, StringComparison.OrdinalIgnoreCase) Then
                lblLastMoldInfo.Text = "Bu kalıp daha önce aynı makinedeydi: " & previousMachine &
                                       "    |    Son TR: " & If(previousTrCode.Trim() = "", "-", previousTrCode)
                lblLastMoldInfo.ForeColor = Color.DarkGreen
            Else
                lblLastMoldInfo.Text = "Bilgi: Kalıp son tamamlanan kayıtta " & previousMachine & " makinesindeydi; şimdi " & currentMachine &
                                       " makinesine bağlanıyor. Son TR: " & If(previousTrCode.Trim() = "", "-", previousTrCode) &
                                       ". Söküm takip edilmediği için bu yalnızca uyarıdır."
                lblLastMoldInfo.ForeColor = Color.DarkOrange
            End If
        Catch ex As Exception
            ErrorLogService.Log("FrmProductionTicketEntry.RefreshLastMoldInfo", ex)
            lblLastMoldInfo.Text = ""
        End Try
    End Sub

    Private Sub StartBinding_Click(sender As Object, e As EventArgs)
        Try
            If Not ValidateRequiredInputs() Then Return

            Dim p = SelectedProduct()
            Dim activeSameMold = ActiveBindingForMold(cboMoldCode.Text.Trim())

            If activeSameMold IsNot Nothing Then
                Dim answer = MessageBox.Show("Bu kalıp için devam eden bir bağlama kaydı görünüyor." & Environment.NewLine &
                                             "Sökülen kalıp ayrıca takip edilmediği için bu kayıt eski kalmış olabilir." & Environment.NewLine & Environment.NewLine &
                                             "Devam eden kayıt:" & Environment.NewLine &
                                             "Bağlama No: " & DataService.GetValue(activeSameMold, "BindingId") & Environment.NewLine &
                                             "Makine: " & DataService.GetValue(activeSameMold, "MachineNo") & Environment.NewLine &
                                             "Başlatan: " & DataService.GetValue(activeSameMold, "StartedBy") & Environment.NewLine &
                                             "Başlangıç: " & DataService.GetValue(activeSameMold, "StartedAt") & Environment.NewLine & Environment.NewLine &
                                             "Yine de yeni bağlantı başlatılsın mı?",
                                             "Kalıp için açık kayıt var", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                                             MessageBoxDefaultButton.Button2)
                If answer <> DialogResult.Yes Then Return
            End If

            Dim previousMachine = GetPreviousMachineNo(cboMoldCode.Text.Trim())
            Dim bindingId = BuildBindingId()
            Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")

            Dim startNote = txtNote.Text.Trim()
            Dim combinedNote = startNote
            If IsDifferentFromPreviousMachine() Then
                combinedNote = "Önceki makine bilgisi: " & previousMachine & " -> " & txtMachineNo.Text.Trim() &
                               If(txtMachineChangeReason.Text.Trim() <> "", " | Açıklama: " & txtMachineChangeReason.Text.Trim(), "") &
                               If(startNote <> "", " | Not: " & startNote, "")
            End If

            Dim row As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                {"BindingId", bindingId},
                {"Status", "STARTED"},
                {"StartedAt", nowText},
                {"StartedBy", AppState.CurrentUserName},
                {"StartComputerName", Environment.MachineName},
                {"CompletedAt", ""},
                {"CompletedBy", ""},
                {"CompletedComputerName", ""},
                {"MachineNo", txtMachineNo.Text.Trim()},
                {"PreviousMachineNo", previousMachine},
                {"MoldCode", cboMoldCode.Text.Trim()},
                {"TrCode", p.TrCode},
                {"DrawingRev", p.DrawingRev},
                {"ProductName", p.ProductName},
                {"Material", p.Material},
                {"ColorName", p.ColorName},
                {"PlasticCode", p.PlasticCode},
                {"RawMaterial", txtRawMaterial.Text.Trim()},
                {"WorkOrderNo", txtWorkOrderNo.Text.Trim()},
                {"BindingReason", cboBindingReason.Text.Trim()},
                {"MachineChangeReason", txtMachineChangeReason.Text.Trim()},
                {"StartNote", startNote},
                {"FinishNote", ""},
                {"Note", combinedNote},
                {"BindingDurationMin", ""},
                {"ProductionTicketId", ""}
            }

            DataService.AppendMoldBindingRecord(row)
            AuditService.Log("MOLD_BINDING_START", p.TrCode, p.DrawingRev,
                             $"BindingId={bindingId}; Machine={txtMachineNo.Text.Trim()}; PreviousMachine={previousMachine}; Mold={cboMoldCode.Text.Trim()}; Reason={cboBindingReason.Text.Trim()}; MachineChangeReason={txtMachineChangeReason.Text.Trim()}")

            LoadActiveBindings()
            MessageBox.Show("Kalıp bağlama başlatıldı." & Environment.NewLine &
                            "Bağlama No: " & bindingId & Environment.NewLine &
                            "Başlangıç: " & nowText,
                            "Bağlama başladı", MessageBoxButtons.OK, MessageBoxIcon.Information)

            DialogResult = DialogResult.OK
            Close()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Bağlama başlatılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Function SelectedActiveBindingId() As String
        If gridActiveBindings.CurrentRow Is Nothing OrElse Not gridActiveBindings.Columns.Contains("BindingId") Then Return ""
        Return Convert.ToString(gridActiveBindings.CurrentRow.Cells("BindingId").Value)
    End Function

    Private Function FindSelectedBindingRow() As Dictionary(Of String, String)
        Dim bindingId = SelectedActiveBindingId()
        If bindingId.Trim() = "" Then Return Nothing

        Return DataService.GetMoldBindingRecords().
            FirstOrDefault(Function(r) String.Equals(DataService.GetValue(r, "BindingId"), bindingId, StringComparison.OrdinalIgnoreCase))
    End Function

    Private Function DurationMinutes(startText As String, endTime As DateTime) As String
        Dim startDate As DateTime
        If DateTime.TryParseExact(startText,
                                  "yyyy-MM-dd HH:mm:ss",
                                  CultureInfo.InvariantCulture,
                                  DateTimeStyles.None,
                                  startDate) OrElse DateTime.TryParse(startText, startDate) Then
            Return Math.Max(0, CInt(Math.Round((endTime - startDate).TotalMinutes))).ToString()
        End If

        Return ""
    End Function

    Private Function BuildTicketNote(bindingRow As Dictionary(Of String, String), endTime As DateTime, durationMin As String) As String
        Dim startText = DataService.GetValue(bindingRow, "StartedAt")
        Dim baseNote = DataService.GetValue(bindingRow, "Note")
        Dim finishNote = txtFinishNote.Text.Trim()

        Dim trackingNote = "Kalıp bağlama tamamlandı." &
                           " Başlangıç: " & startText &
                           " | Bitiş: " & endTime.ToString("yyyy-MM-dd HH:mm:ss")

        If durationMin.Trim() <> "" Then trackingNote &= " | Süre: " & durationMin & " dk"

        Dim previousMachine = DataService.GetValue(bindingRow, "PreviousMachineNo")
        If previousMachine.Trim() <> "" AndAlso Not String.Equals(previousMachine, DataService.GetValue(bindingRow, "MachineNo"), StringComparison.OrdinalIgnoreCase) Then
            trackingNote &= " | Makine değişimi: " & previousMachine & " -> " & DataService.GetValue(bindingRow, "MachineNo")
        End If

        If DataService.GetValue(bindingRow, "BindingReason").Trim() <> "" Then
            trackingNote &= " | Bağlama Nedeni: " & DataService.GetValue(bindingRow, "BindingReason")
        End If

        If DataService.GetValue(bindingRow, "MachineChangeReason").Trim() <> "" Then
            trackingNote &= " | Makine Değişim Nedeni: " & DataService.GetValue(bindingRow, "MachineChangeReason")
        End If

        If baseNote.Trim() <> "" Then trackingNote &= " | Başlangıç Notu: " & baseNote
        If finishNote.Trim() <> "" Then trackingNote &= " | Bitiş Notu: " & finishNote

        Return trackingNote
    End Function

    Private Sub FinishBinding_Click(sender As Object, e As EventArgs)
        Try
            Dim bindingRow = FindSelectedBindingRow()
            If bindingRow Is Nothing Then
                MessageBox.Show("Lütfen bitirmek istediğiniz devam eden kalıp bağlama kaydını seçiniz.",
                                "Bağlama seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            If Not String.Equals(DataService.GetValue(bindingRow, "Status"), "STARTED", StringComparison.OrdinalIgnoreCase) Then
                MessageBox.Show("Seçili bağlama kaydı zaten bitirilmiş.", "Kayıt tamamlanmış", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim endTime = DateTime.Now
            Dim durationMin = DurationMinutes(DataService.GetValue(bindingRow, "StartedAt"), endTime)
            Dim ticketId = BuildQualityTicketId()
            Dim ticketNote = BuildTicketNote(bindingRow, endTime, durationMin)

            Dim row As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                {"TicketId", ticketId},
                {"Status", "OPEN"},
                {"CreatedAt", endTime.ToString("yyyy-MM-dd HH:mm:ss")},
                {"CreatedBy", AppState.CurrentUserName},
                {"ComputerName", Environment.MachineName},
                {"MachineNo", DataService.GetValue(bindingRow, "MachineNo")},
                {"PreviousMachineNo", DataService.GetValue(bindingRow, "PreviousMachineNo")},
                {"MoldCode", DataService.GetValue(bindingRow, "MoldCode")},
                {"TrCode", DataService.GetValue(bindingRow, "TrCode")},
                {"DrawingRev", DataService.GetValue(bindingRow, "DrawingRev")},
                {"ProductName", DataService.GetValue(bindingRow, "ProductName")},
                {"Material", DataService.GetValue(bindingRow, "Material")},
                {"ColorName", DataService.GetValue(bindingRow, "ColorName")},
                {"PlasticCode", DataService.GetValue(bindingRow, "PlasticCode")},
                {"RawMaterial", DataService.GetValue(bindingRow, "RawMaterial")},
                {"WorkOrderNo", DataService.GetValue(bindingRow, "WorkOrderNo")},
                {"Note", ticketNote},
                {"SeenByQuality", ""},
                {"SeenAt", ""},
                {"ClosedBy", ""},
                {"ClosedAt", ""},
                {"CloseNote", ""},
                {"BindingId", DataService.GetValue(bindingRow, "BindingId")},
                {"BindingStartAt", DataService.GetValue(bindingRow, "StartedAt")},
                {"BindingEndAt", endTime.ToString("yyyy-MM-dd HH:mm:ss")},
                {"BindingDurationMin", durationMin},
                {"BindingReason", DataService.GetValue(bindingRow, "BindingReason")},
                {"MachineChangeReason", DataService.GetValue(bindingRow, "MachineChangeReason")}
            }

            ticketId = DataService.CompleteMoldBindingAndCreateProductionTicket(
                DataService.GetValue(bindingRow, "BindingId"),
                row,
                AppState.CurrentUserName,
                txtFinishNote.Text.Trim(),
                durationMin)

            AuditService.Log("MOLD_BINDING_COMPLETE_AND_QUALITY_TICKET_CREATE",
                             DataService.GetValue(bindingRow, "TrCode"),
                             DataService.GetValue(bindingRow, "DrawingRev"),
                             $"BindingId={DataService.GetValue(bindingRow, "BindingId")}; TicketId={ticketId}; DurationMin={durationMin}")

            LoadActiveBindings()
            MessageBox.Show("Kalıp bağlama bitirildi ve Kalite Kontrol için ticket oluşturuldu." & Environment.NewLine &
                            "Ticket No: " & ticketId & Environment.NewLine &
                            "Bitiş: " & endTime.ToString("yyyy-MM-dd HH:mm:ss") &
                            If(durationMin.Trim() <> "", Environment.NewLine & "Süre: " & durationMin & " dk", ""),
                            "Bağlama tamamlandı", MessageBoxButtons.OK, MessageBoxIcon.Information)

            DialogResult = DialogResult.OK
            Close()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Bağlama bitirilemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub LoadActiveBindings()
        Dim activeRows = DataService.GetMoldBindingRecords().
            Where(Function(r) String.Equals(DataService.GetValue(r, "Status"), "STARTED", StringComparison.OrdinalIgnoreCase)).
            ToList()

        If Not (AppState.IsProductionManager OrElse AppState.IsManager OrElse AppState.IsAdmin) Then
            activeRows = activeRows.
                Where(Function(r) String.Equals(DataService.GetValue(r, "StartedBy"), AppState.CurrentUserName, StringComparison.OrdinalIgnoreCase)).
                ToList()
        End If

        Dim dt As New DataTable()
        For Each col In {"BindingId", "StartedAt", "StartedBy", "MachineNo", "PreviousMachineNo", "MoldCode", "BindingReason", "TrCode", "ProductName", "WorkOrderNo"}
            dt.Columns.Add(col)
        Next

        For Each r In activeRows.OrderByDescending(Function(x) DataService.GetValue(x, "StartedAt"))
            Dim dr = dt.NewRow()
            For Each dc As DataColumn In dt.Columns
                dr(dc.ColumnName) = DataService.GetValue(r, dc.ColumnName)
            Next
            dt.Rows.Add(dr)
        Next

        gridActiveBindings.DataSource = dt
    End Sub

    Private Sub ActiveGrid_DoubleClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 Then Return

        Dim bindingRow = FindSelectedBindingRow()
        If bindingRow Is Nothing Then Return

        txtMachineNo.Text = DataService.GetValue(bindingRow, "MachineNo")
        cboMoldCode.Text = DataService.GetValue(bindingRow, "MoldCode")
        txtRawMaterial.Text = DataService.GetValue(bindingRow, "RawMaterial")
        txtWorkOrderNo.Text = DataService.GetValue(bindingRow, "WorkOrderNo")
        If DataService.GetValue(bindingRow, "BindingReason").Trim() <> "" Then cboBindingReason.Text = DataService.GetValue(bindingRow, "BindingReason")
        txtMachineChangeReason.Text = DataService.GetValue(bindingRow, "MachineChangeReason")
        txtNote.Text = DataService.GetValue(bindingRow, "StartNote")
        If txtNote.Text.Trim() = "" Then txtNote.Text = DataService.GetValue(bindingRow, "Note")
        txtFinishNote.Clear()
        RefreshLastMoldInfo()

        Dim tr = DataService.GetValue(bindingRow, "TrCode")
        Dim rev = DataService.GetValue(bindingRow, "DrawingRev")
        For i As Integer = 0 To cboProduct.Items.Count - 1
            Dim p = TryCast(cboProduct.Items(i), ProductInfo)
            If p IsNot Nothing AndAlso
               String.Equals(p.TrCode, tr, StringComparison.OrdinalIgnoreCase) AndAlso
               String.Equals(p.DrawingRev, rev, StringComparison.OrdinalIgnoreCase) Then
                cboProduct.SelectedIndex = i
                Exit For
            End If
        Next
    End Sub

    Private Sub Clear_Click(sender As Object, e As EventArgs)
        ClearInputs(True)
    End Sub

    Private Sub ClearInputs(clearProductInfo As Boolean)
        txtMachineNo.Clear()
        cboMoldCode.Text = ""
        txtRawMaterial.Clear()
        txtWorkOrderNo.Clear()
        If cboBindingReason.Items.Count > 0 Then cboBindingReason.SelectedIndex = 0
        txtMachineChangeReason.Clear()
        lblLastMoldInfo.Text = ""
        txtNote.Clear()
        txtFinishNote.Clear()

        If clearProductInfo Then
            If txtProductFilter.Text <> "" Then txtProductFilter.Clear()
            cboProduct.SelectedIndex = -1
            lblProductInfo.Text = ""
        Else
            ProductChanged(Me, EventArgs.Empty)
        End If

        LoadOpenMoldWarnings()
    End Sub
End Class
