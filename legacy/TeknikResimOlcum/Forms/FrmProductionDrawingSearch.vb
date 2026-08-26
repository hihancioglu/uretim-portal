Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmProductionDrawingSearch
    Inherits Form

    Private ReadOnly txtTrCode As New TextBox()
    Private ReadOnly grid As New DataGridView()
    Private ReadOnly lblInfo As New Label()
    Private ReadOnly btnOpenDrawing As New Button()
    Private allProducts As New List(Of ProductInfo)()

    Public Sub New()
        AuthorizationService.Require(AppState.CanOpenProductionDrawingSearch, "Teknik Resim Arama")
        AppIconService.Apply(Me)
        Text = "TR Teknik Resim Arama"
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        Size = New Size(1180, 720)
        MinimumSize = New Size(760, 520)
        Font = New Font("Segoe UI", 9.0F)
        BackColor = Color.FromArgb(245, 247, 250)

        BuildScreen()
        LoadProducts()
        ApplyFilter()
    End Sub

    Private Sub BuildScreen()
        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .Padding = New Padding(10),
            .BackColor = BackColor
        }
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 72.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 54.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Controls.Add(root)

        Dim header As New Panel() With {
            .Dock = DockStyle.Fill,
            .BackColor = Color.FromArgb(31, 71, 126),
            .Padding = New Padding(22, 10, 22, 8),
            .Margin = New Padding(0, 0, 0, 8)
        }
        Dim title As New Label() With {
            .Text = "TR Teknik Resim Arama",
            .Dock = DockStyle.Top,
            .Height = 26,
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 13.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft
        }
        Dim subtitle As New Label() With {
            .Text = "TR kodu ile aktif plastik teknik resmini bulun ve salt okunur olarak görüntüleyin.",
            .Dock = DockStyle.Fill,
            .ForeColor = Color.FromArgb(230, 238, 250),
            .TextAlign = ContentAlignment.MiddleLeft,
            .AutoEllipsis = True
        }
        header.Controls.Add(subtitle)
        header.Controls.Add(title)
        root.Controls.Add(header, 0, 0)

        Dim toolbar As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 5,
            .RowCount = 1,
            .BackColor = Color.White,
            .Padding = New Padding(12, 8, 12, 8),
            .Margin = New Padding(0, 0, 0, 8)
        }
        toolbar.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 75.0F))
        toolbar.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        toolbar.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 120.0F))
        toolbar.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 150.0F))
        toolbar.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 110.0F))
        root.Controls.Add(toolbar, 0, 1)

        Dim lblTr As New Label() With {
            .Text = "TR Kodu",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        }
        toolbar.Controls.Add(lblTr, 0, 0)

        txtTrCode.Dock = DockStyle.Fill
        txtTrCode.Margin = New Padding(0, 2, 10, 2)
        txtTrCode.PlaceholderText = "TR kodu yazın..."
        AddHandler txtTrCode.TextChanged, Sub() ApplyFilter()
        AddHandler txtTrCode.KeyDown, AddressOf TrCode_KeyDown
        toolbar.Controls.Add(txtTrCode, 1, 0)

        Dim btnClear As New Button() With {
            .Text = "Temizle",
            .Dock = DockStyle.Fill,
            .Margin = New Padding(4, 0, 8, 0),
            .FlatStyle = FlatStyle.Flat,
            .BackColor = Color.White,
            .ForeColor = Color.FromArgb(31, 71, 126),
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        }
        btnClear.FlatAppearance.BorderColor = Color.FromArgb(190, 201, 215)
        AddHandler btnClear.Click,
            Sub()
                txtTrCode.Clear()
                txtTrCode.Focus()
            End Sub
        toolbar.Controls.Add(btnClear, 2, 0)

        btnOpenDrawing.Text = "Teknik Resmi Aç"
        btnOpenDrawing.Dock = DockStyle.Fill
        btnOpenDrawing.Margin = New Padding(4, 0, 8, 0)
        btnOpenDrawing.FlatStyle = FlatStyle.Flat
        btnOpenDrawing.BackColor = Color.FromArgb(31, 71, 126)
        btnOpenDrawing.ForeColor = Color.White
        btnOpenDrawing.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        btnOpenDrawing.Enabled = False
        AddHandler btnOpenDrawing.Click, AddressOf OpenSelectedDrawing_Click
        toolbar.Controls.Add(btnOpenDrawing, 3, 0)

        Dim btnClose As New Button() With {
            .Text = "Kapat",
            .Dock = DockStyle.Fill,
            .Margin = New Padding(4, 0, 0, 0),
            .FlatStyle = FlatStyle.Flat,
            .BackColor = Color.White,
            .ForeColor = Color.FromArgb(40, 40, 40)
        }
        btnClose.FlatAppearance.BorderColor = Color.FromArgb(190, 201, 215)
        AddHandler btnClose.Click, Sub() Close()
        toolbar.Controls.Add(btnClose, 4, 0)

        Dim gridHost As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .BackColor = Color.White,
            .Margin = New Padding(0)
        }
        gridHost.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
        gridHost.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        root.Controls.Add(gridHost, 0, 2)

        lblInfo.Dock = DockStyle.Fill
        lblInfo.Padding = New Padding(12, 0, 12, 0)
        lblInfo.TextAlign = ContentAlignment.MiddleLeft
        lblInfo.ForeColor = Color.FromArgb(31, 71, 126)
        lblInfo.BackColor = Color.FromArgb(231, 238, 248)
        lblInfo.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        lblInfo.AutoEllipsis = True
        gridHost.Controls.Add(lblInfo, 0, 0)

        ConfigureGrid()
        gridHost.Controls.Add(grid, 0, 1)
    End Sub

    Private Sub ConfigureGrid()
        grid.Dock = DockStyle.Fill
        grid.Margin = New Padding(0)
        grid.ReadOnly = True
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.AllowUserToResizeRows = False
        grid.MultiSelect = False
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.AutoGenerateColumns = False
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        grid.RowHeadersVisible = False
        grid.BackgroundColor = Color.White
        grid.BorderStyle = BorderStyle.FixedSingle
        grid.GridColor = Color.FromArgb(210, 218, 228)
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersHeight = 38
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(226, 234, 244)
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(24, 50, 82)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        grid.DefaultCellStyle.Font = New Font("Segoe UI", 9.0F)
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(196, 220, 248)
        grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(20, 40, 65)
        grid.RowTemplate.Height = 32

        grid.Columns.Add(MakeColumn("TrCode", "TR Kodu", 115, 13))
        grid.Columns.Add(MakeColumn("DrawingRev", "Revizyon", 90, 9))
        grid.Columns.Add(MakeColumn("ProductName", "Ürün Adı", 280, 28))
        grid.Columns.Add(MakeColumn("PlasticCode", "Plastik Kodu", 140, 14))
        grid.Columns.Add(MakeColumn("Material", "Malzeme", 150, 15))
        grid.Columns.Add(MakeColumn("ColorName", "Renk", 120, 12))
        grid.Columns.Add(MakeColumn("MoldCode", "Kalıp", 110, 11))
        grid.Columns.Add(MakeColumn("DrawingStatus", "Teknik Resim", 135, 13))

        AddHandler grid.SelectionChanged, AddressOf Grid_SelectionChanged
        AddHandler grid.CellDoubleClick, AddressOf Grid_CellDoubleClick
        AddHandler grid.KeyDown, AddressOf Grid_KeyDown
    End Sub

    Private Shared Function MakeColumn(name As String, headerText As String, minimumWidth As Integer, fillWeight As Single) As DataGridViewTextBoxColumn
        Return New DataGridViewTextBoxColumn() With {
            .Name = name,
            .HeaderText = headerText,
            .MinimumWidth = minimumWidth,
            .FillWeight = fillWeight,
            .SortMode = DataGridViewColumnSortMode.Automatic
        }
    End Function

    Private Sub LoadProducts()
        allProducts = DataService.GetProducts(True).
            Where(Function(product) CanShowProduct(product)).
            OrderBy(Function(product) DataService.TrCodeNumericSortValue(product.TrCode)).
            ThenBy(Function(product) product.TrCode, StringComparer.OrdinalIgnoreCase).
            ThenBy(Function(product) product.DrawingRev, StringComparer.OrdinalIgnoreCase).
            ToList()
    End Sub

    Private Shared Function CanShowProduct(product As ProductInfo) As Boolean
        If product Is Nothing Then Return False
        If String.IsNullOrWhiteSpace(product.TrCode) Then Return False
        If String.IsNullOrWhiteSpace(product.DrawingFile) Then Return False
        If Not String.Equals(ProductInfo.NormalizeDrawingScope(product.DrawingScope), ProductInfo.DrawingScopePlastic, StringComparison.OrdinalIgnoreCase) Then Return False
        Return AppState.CanAccessDrawingScope(product.DrawingScope)
    End Function

    Private Sub ApplyFilter()
        Dim query = txtTrCode.Text.Trim()
        Dim filtered As List(Of ProductInfo)

        If query = "" Then
            filtered = New List(Of ProductInfo)()
        Else
            filtered = allProducts.
                Where(Function(product) product.TrCode.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).
                OrderBy(Function(product) If(String.Equals(product.TrCode.Trim(), query, StringComparison.OrdinalIgnoreCase), 0, 1)).
                ThenBy(Function(product) DataService.TrCodeNumericSortValue(product.TrCode)).
                ThenBy(Function(product) product.TrCode, StringComparer.OrdinalIgnoreCase).
                ThenBy(Function(product) product.DrawingRev, StringComparer.OrdinalIgnoreCase).
                ToList()
        End If

        grid.Rows.Clear()
        For Each product In filtered
            Dim rowIndex = grid.Rows.Add(
                product.TrCode,
                product.DrawingRev,
                product.ProductName,
                product.PlasticCode,
                product.Material,
                product.ColorName,
                product.MoldCode,
                If(DrawingFileExists(product), "Hazır", "Dosya bulunamadı"))

            Dim row = grid.Rows(rowIndex)
            row.Tag = product
            If Not DrawingFileExists(product) Then
                row.DefaultCellStyle.BackColor = Color.MistyRose
                row.DefaultCellStyle.ForeColor = Color.DarkRed
            End If
        Next

        If query = "" Then
            lblInfo.Text = "TR kodu yazmaya başlayın. Bu pencere yalnızca aktif plastik teknik resimlerini gösterir."
        Else
            lblInfo.Text = "Eşleşen teknik resim: " & filtered.Count.ToString() & " adet"
        End If

        If grid.Rows.Count > 0 Then
            grid.ClearSelection()
            grid.Rows(0).Selected = True
            grid.CurrentCell = grid.Rows(0).Cells("TrCode")
        End If
        UpdateOpenButtonState()
    End Sub

    Private Shared Function DrawingFileExists(product As ProductInfo) As Boolean
        Try
            Return product IsNot Nothing AndAlso
                   Not String.IsNullOrWhiteSpace(product.DrawingFile) AndAlso
                   File.Exists(AppPaths.ResolveDrawingFilePath(product.DrawingFile))
        Catch
            Return False
        End Try
    End Function

    Private Sub TrCode_KeyDown(sender As Object, e As KeyEventArgs)
        If e.KeyCode <> Keys.Enter Then Return

        e.Handled = True
        e.SuppressKeyPress = True

        If grid.Rows.Count = 1 Then
            OpenSelectedDrawing()
        ElseIf grid.Rows.Count > 1 Then
            grid.Focus()
        End If
    End Sub

    Private Sub Grid_KeyDown(sender As Object, e As KeyEventArgs)
        If e.KeyCode <> Keys.Enter Then Return
        e.Handled = True
        e.SuppressKeyPress = True
        OpenSelectedDrawing()
    End Sub

    Private Sub Grid_CellDoubleClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 Then Return
        OpenSelectedDrawing()
    End Sub

    Private Sub Grid_SelectionChanged(sender As Object, e As EventArgs)
        UpdateOpenButtonState()
    End Sub

    Private Sub UpdateOpenButtonState()
        btnOpenDrawing.Enabled = GetSelectedProduct() IsNot Nothing
    End Sub

    Private Sub OpenSelectedDrawing_Click(sender As Object, e As EventArgs)
        OpenSelectedDrawing()
    End Sub

    Private Function GetSelectedProduct() As ProductInfo
        If grid.CurrentRow IsNot Nothing Then
            Return TryCast(grid.CurrentRow.Tag, ProductInfo)
        End If

        If grid.SelectedRows.Count > 0 Then
            Return TryCast(grid.SelectedRows(0).Tag, ProductInfo)
        End If

        Return Nothing
    End Function

    Private Sub OpenSelectedDrawing()
        Dim product = GetSelectedProduct()
        If product Is Nothing Then
            MessageBox.Show(Me, "Açılacak teknik resmi seçiniz.", "Teknik resim", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Try
            If String.IsNullOrWhiteSpace(product.DrawingFile) Then
                Throw New InvalidDataException("Bu TR için teknik resim dosyası tanımlı değil.")
            End If

            Dim drawingPath = AppPaths.ResolveDrawingFilePath(product.DrawingFile)
            If Not File.Exists(drawingPath) Then
                Throw New FileNotFoundException("Teknik resim dosyası bulunamadı. Teknik Resim birimi kaydı kontrol etmeli.")
            End If

            AuditService.Log(
                "PRODUCTION_LABEL_DRAWING_VIEW",
                product.TrCode,
                product.DrawingRev,
                "Üretim Etiket teknik resim arama ekranından teknik resim açıldı.")

            Using viewer As New FrmPdfViewer(
                product.DrawingFile,
                "Teknik Resim - " & product.TrCode & If(String.IsNullOrWhiteSpace(product.DrawingRev), "", " / " & product.DrawingRev),
                False)

                viewer.ShowDialog(Me)
            End Using
        Catch ex As Exception
            MessageBox.Show(Me, ex.Message, "Teknik resim açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub
End Class
