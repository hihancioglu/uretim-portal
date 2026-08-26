Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmProductAdmin
    Inherits Form

    Private ReadOnly grid As New DataGridView()
    Private ReadOnly lblCount As New Label()
    Private ReadOnly txtTr As New TextBox()
    Private ReadOnly txtName As New TextBox()
    Private ReadOnly txtRev As New TextBox()
    Private ReadOnly txtFile As New TextBox()
    Private ReadOnly cboDrawingScope As New ComboBox()
    Private ReadOnly chkActive As New CheckBox()
    Private ReadOnly chkSetSamePassive As New CheckBox()
    Private ReadOnly txtGridFilter As New TextBox()
    Private ReadOnly cboGridStatus As New ComboBox()
    Private ReadOnly btnTodayMissingDrawings As New Button()
    Private allProductRows As New List(Of ProductInfo)()
    Private selectedDrawingPath As String = ""

    Public Sub New()
        AuthorizationService.Require(AppState.CanViewTechnicalDrawingAdmin, "Urun / Teknik Resim Yonetimi")
        AppIconService.Apply(Me)
        Text = "Ürün / Teknik Resim Yönetimi"
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        Size = New Size(1180, 720)
        MinimumSize = New Size(760, 520)

        Dim mainLayout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .BackColor = SystemColors.Control
        }
        mainLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 185.0F))
        mainLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 58.0F))
        mainLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Controls.Add(mainLayout)

        Dim top As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(12),
            .BackColor = SystemColors.Control,
            .Margin = New Padding(0),
            .ColumnCount = 5,
            .RowCount = 5,
            .AutoScroll = False
        }
        top.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 105.0F))
        top.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 30.0F))
        top.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 115.0F))
        top.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 70.0F))
        top.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 135.0F))
        top.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))
        top.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))
        top.RowStyles.Add(New RowStyle(SizeType.Absolute, 34.0F))
        top.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        top.RowStyles.Add(New RowStyle(SizeType.Absolute, 28.0F))
        mainLayout.Controls.Add(top, 0, 0)

        top.Controls.Add(CreateEditorLabel("TR Kodu"), 0, 0)
        txtTr.Dock = DockStyle.Fill
        txtTr.Margin = New Padding(6, 4, 8, 4)
        top.Controls.Add(txtTr, 1, 0)

        top.Controls.Add(CreateEditorLabel("Ürün Adı"), 2, 0)
        txtName.Dock = DockStyle.Fill
        txtName.Margin = New Padding(6, 4, 6, 4)
        top.Controls.Add(txtName, 3, 0)
        top.SetColumnSpan(txtName, 2)

        top.Controls.Add(CreateEditorLabel("Revizyon"), 0, 1)
        txtRev.Dock = DockStyle.Fill
        txtRev.Margin = New Padding(6, 4, 8, 4)
        top.Controls.Add(txtRev, 1, 1)

        top.Controls.Add(CreateEditorLabel("Şifreli Dosya"), 2, 1)
        txtFile.Dock = DockStyle.Fill
        txtFile.Margin = New Padding(6, 4, 6, 4)
        txtFile.ReadOnly = True
        top.Controls.Add(txtFile, 3, 1)

        Dim btnSelect As New Button() With {.Text = "PDF/DXF Seç", .Dock = DockStyle.Fill, .Margin = New Padding(6, 3, 6, 3)}
        AddHandler btnSelect.Click, AddressOf SelectDrawing_Click
        top.Controls.Add(btnSelect, 4, 1)

        top.Controls.Add(CreateEditorLabel("Resim Tipi"), 0, 2)
        cboDrawingScope.Dock = DockStyle.Fill
        cboDrawingScope.Margin = New Padding(6, 4, 8, 4)
        cboDrawingScope.DropDownStyle = ComboBoxStyle.DropDownList
        cboDrawingScope.Items.AddRange(ProductInfo.DrawingScopeLabels.Cast(Of Object)().ToArray())
        cboDrawingScope.SelectedItem = ProductInfo.DrawingScopePlastic
        AddHandler cboDrawingScope.SelectedIndexChanged,
            Sub()
                If selectedDrawingPath <> "" AndAlso txtTr.Text.Trim() <> "" AndAlso txtRev.Text.Trim() <> "" Then
                    txtFile.Text = BuildUniqueDrawingFileName(txtTr.Text.Trim(), txtRev.Text.Trim(), Path.GetExtension(selectedDrawingPath))
                End If
            End Sub
        top.Controls.Add(cboDrawingScope, 1, 2)

        chkActive.Text = "Aktif revizyon"
        chkActive.Dock = DockStyle.Fill
        chkActive.Margin = New Padding(6, 3, 8, 3)
        chkActive.Checked = True
        top.Controls.Add(chkActive, 2, 2)
        chkSetSamePassive.Text = "Aynı TR'nin eski kayıtlarını pasif yap"
        chkSetSamePassive.Dock = DockStyle.Fill
        chkSetSamePassive.Margin = New Padding(6, 3, 6, 3)
        chkSetSamePassive.Checked = True
        top.Controls.Add(chkSetSamePassive, 3, 2)
        top.SetColumnSpan(chkSetSamePassive, 2)

        Dim btnSave As New Button() With {.Text = "Kaydet / Güncelle", .Width = 150, .Height = 30, .Margin = New Padding(4)}
        AddHandler btnSave.Click, AddressOf Save_Click
        Dim btnOpen As New Button() With {.Text = "Seçili Teknik Resmi Aç", .Width = 170, .Height = 30, .Margin = New Padding(4)}
        AddHandler btnOpen.Click, AddressOf OpenPdf_Click
        Dim btnNew As New Button() With {.Text = "Yeni", .Width = 100, .Height = 30, .Margin = New Padding(4)}
        AddHandler btnNew.Click, AddressOf New_Click
        Dim btnRefresh As New Button() With {.Text = "Listeyi Yenile", .Width = 120, .Height = 30, .Margin = New Padding(4)}
        AddHandler btnRefresh.Click, Sub() LoadGrid()

        Dim btnDelete As New Button() With {.Text = "Seçili Kaydı Sil", .Width = 130, .Height = 30, .Margin = New Padding(4)}
        btnDelete.ForeColor = Color.DarkRed
        AddHandler btnDelete.Click, AddressOf DeleteSelected_Click

        btnTodayMissingDrawings.Width = 220
        btnTodayMissingDrawings.Height = 30
        btnTodayMissingDrawings.Margin = New Padding(4)
        btnTodayMissingDrawings.Text = "Bugünün Eksik Resimleri"
        btnTodayMissingDrawings.BackColor = Color.MistyRose
        btnTodayMissingDrawings.ForeColor = Color.DarkRed
        btnTodayMissingDrawings.FlatStyle = FlatStyle.Flat
        btnTodayMissingDrawings.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        btnTodayMissingDrawings.AutoEllipsis = False
        AddHandler btnTodayMissingDrawings.Click, AddressOf ShowTodayMissingDrawings_Click

        Dim buttonFlow As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .Margin = New Padding(0),
            .Padding = New Padding(0),
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = True,
            .AutoScroll = False
        }
        buttonFlow.Controls.AddRange({btnSave, btnOpen, btnNew, btnRefresh})
        If AppState.IsAdmin Then buttonFlow.Controls.Add(btnDelete)
        buttonFlow.Controls.Add(btnTodayMissingDrawings)
        top.Controls.Add(buttonFlow, 0, 3)
        top.SetColumnSpan(buttonFlow, 5)

        Dim info As New Label() With {
            .Text = "Kaydedilen teknik resimler aşağıdaki listede görünür. Satıra tek tıklayınca bilgiler forma alınır, çift tıklayınca teknik resim açılır.",
            .Dock = DockStyle.Fill,
            .Margin = New Padding(6, 2, 6, 0),
            .TextAlign = ContentAlignment.MiddleLeft,
            .AutoEllipsis = True
        }
        top.Controls.Add(info, 0, 4)
        top.SetColumnSpan(info, 5)

        If Not AppState.CanOpenTechnicalDrawingAdmin Then
            Text &= " - Salt Okunur"
            txtTr.ReadOnly = True
            txtName.ReadOnly = True
            txtRev.ReadOnly = True
            cboDrawingScope.Enabled = False
            chkActive.Enabled = False
            chkSetSamePassive.Enabled = False
            btnSelect.Visible = False
            btnSave.Visible = False
            btnNew.Visible = False
            info.Text = "SALT OKUNUR: Teknik resim kayıtları ve bugünün eksik resimleri görüntülenebilir; kayıt eklenemez veya değiştirilemez."
        End If

        Dim listHeader As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(12, 6, 12, 4),
            .BackColor = Color.WhiteSmoke,
            .Margin = New Padding(0),
            .ColumnCount = 6,
            .RowCount = 1,
            .AutoScroll = False
        }
        listHeader.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        listHeader.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 85.0F))
        listHeader.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        listHeader.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 55.0F))
        listHeader.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 105.0F))
        listHeader.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 130.0F))
        listHeader.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        lblCount.Text = "Kayıtlı teknik resimler"
        lblCount.Dock = DockStyle.Fill
        lblCount.Margin = New Padding(3, 2, 8, 2)
        lblCount.Font = New Font(Font.FontFamily, 9.0F, FontStyle.Bold)
        lblCount.TextAlign = ContentAlignment.MiddleLeft
        lblCount.AutoEllipsis = True
        listHeader.Controls.Add(lblCount, 0, 0)

        Dim lblFilter As New Label() With {.Text = "Liste Filtresi", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .BackColor = Color.Transparent}
        listHeader.Controls.Add(lblFilter, 1, 0)
        txtGridFilter.Dock = DockStyle.Fill
        txtGridFilter.Margin = New Padding(3, 2, 8, 2)
        txtGridFilter.PlaceholderText = "TR / revizyon / ürün / dosya"
        AddHandler txtGridFilter.TextChanged, Sub() ApplyGridFilter()
        listHeader.Controls.Add(txtGridFilter, 2, 0)

        Dim lblStatus As New Label() With {.Text = "Durum", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .BackColor = Color.Transparent}
        listHeader.Controls.Add(lblStatus, 3, 0)
        cboGridStatus.Dock = DockStyle.Fill
        cboGridStatus.Margin = New Padding(3, 2, 8, 2)
        cboGridStatus.DropDownStyle = ComboBoxStyle.DropDownList
        cboGridStatus.Items.AddRange({"TÜMÜ", "AKTİF", "PASİF"})
        cboGridStatus.SelectedIndex = 0
        AddHandler cboGridStatus.SelectedIndexChanged, Sub() ApplyGridFilter()
        listHeader.Controls.Add(cboGridStatus, 4, 0)

        Dim btnClearFilter As New Button() With {.Text = "Filtreyi Temizle", .Dock = DockStyle.Fill, .Margin = New Padding(6, 1, 3, 1)}
        AddHandler btnClearFilter.Click, Sub()
                                             txtGridFilter.Clear()
                                             cboGridStatus.SelectedIndex = 0
                                             ApplyGridFilter()
                                         End Sub
        listHeader.Controls.Add(btnClearFilter, 5, 0)
        mainLayout.Controls.Add(listHeader, 0, 1)

        ConfigureGrid()
        mainLayout.Controls.Add(grid, 0, 2)

        Dim isAdjustingLayout As Boolean = False
        Dim applyResponsiveLayout As Action =
            Sub()
                If isAdjustingLayout OrElse mainLayout.IsDisposed OrElse top.IsDisposed OrElse listHeader.IsDisposed Then Return

                isAdjustingLayout = True
                Try
                    Dim availableWidth = Math.Max(480, mainLayout.ClientSize.Width)
                    Dim dpiScale = Math.Max(1.0R, DeviceDpi / 96.0R)

                    buttonFlow.AutoScroll = False
                    Dim buttonWidth = Math.Max(300, availableWidth - top.Padding.Horizontal)
                    Dim buttonHeight = Math.Max(
                        CInt(Math.Round(38 * dpiScale)),
                        buttonFlow.GetPreferredSize(New Size(buttonWidth, 0)).Height)
                    top.RowStyles(3).Height = buttonHeight

                    Dim preferredTopHeight = top.GetPreferredSize(New Size(availableWidth, 0)).Height + top.Margin.Vertical
                    Dim preferredHeaderHeight = Math.Max(
                        CInt(Math.Round(48 * dpiScale)),
                        listHeader.GetPreferredSize(New Size(availableWidth, 0)).Height + listHeader.Margin.Vertical)

                    Dim minimumGridHeight = Math.Max(120, CInt(Math.Round(150 * dpiScale)))
                    Dim maximumTopHeight = Math.Max(
                        CInt(Math.Round(150 * dpiScale)),
                        mainLayout.ClientSize.Height - preferredHeaderHeight - minimumGridHeight)
                    Dim topHeight = Math.Min(preferredTopHeight, maximumTopHeight)

                    top.AutoScroll = preferredTopHeight > topHeight
                    buttonFlow.AutoScroll = buttonHeight > top.RowStyles(3).Height
                    mainLayout.RowStyles(0).Height = topHeight
                    mainLayout.RowStyles(1).Height = preferredHeaderHeight
                    mainLayout.PerformLayout()
                Finally
                    isAdjustingLayout = False
                End Try
            End Sub

        AddHandler ClientSizeChanged, Sub() applyResponsiveLayout.Invoke()
        AddHandler Shown, Sub() applyResponsiveLayout.Invoke()
        AddHandler DpiChanged,
            Sub()
                If IsHandleCreated AndAlso Not IsDisposed Then
                    BeginInvoke(New MethodInvoker(Sub() applyResponsiveLayout.Invoke()))
                End If
            End Sub
        applyResponsiveLayout.Invoke()

        LoadGrid()
    End Sub

    Private Shared Function CreateEditorLabel(text As String) As Label
        Return New Label() With {
            .Text = text,
            .Dock = DockStyle.Fill,
            .Margin = New Padding(6, 2, 3, 2),
            .TextAlign = ContentAlignment.MiddleLeft,
            .BackColor = Color.Transparent
        }
    End Function

    Private Sub ConfigureGrid()
        grid.Dock = DockStyle.Fill
        grid.ReadOnly = True
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.MultiSelect = False
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.AutoGenerateColumns = False
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        grid.RowHeadersVisible = False
        grid.ColumnHeadersVisible = True
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 235, 235)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        grid.BackgroundColor = Color.White
        grid.BorderStyle = BorderStyle.FixedSingle
        grid.GridColor = Color.Gainsboro
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None
        grid.RowTemplate.Height = 26

        grid.Columns.Clear()
        grid.Columns.Add(MakeTextColumn("TrCode", "TR Kodu", 120, 12))
        grid.Columns.Add(MakeTextColumn("DrawingRev", "Revizyon", 90, 8))
        grid.Columns.Add(MakeTextColumn("DrawingScope", "Resim Tipi", 150, 12))
        grid.Columns.Add(MakeTextColumn("ProductName", "Ürün Adı", 220, 18))
        grid.Columns.Add(MakeTextColumn("DrawingFile", "Şifreli Dosya", 260, 22))
        grid.Columns.Add(MakeTextColumn("IsActive", "Aktif", 70, 6))
        grid.Columns.Add(MakeTextColumn("CreatedBy", "Kaydeden", 110, 10))
        grid.Columns.Add(MakeTextColumn("CreatedAt", "Kayıt Tarihi", 150, 12))

        AddHandler grid.CellClick, AddressOf Grid_CellClick
        AddHandler grid.CellDoubleClick, AddressOf Grid_DoubleClick
    End Sub

    Private Function MakeTextColumn(name As String, header As String, width As Integer, fillWeight As Single) As DataGridViewTextBoxColumn
        Return New DataGridViewTextBoxColumn() With {
            .Name = name,
            .HeaderText = header,
            .Width = width,
            .MinimumWidth = 60,
            .FillWeight = fillWeight,
            .SortMode = DataGridViewColumnSortMode.Automatic
        }
    End Function

    Private Sub LoadGrid()
        allProductRows = DataService.GetProducts(False)
        ApplyGridFilter()
        UpdateTodayMissingDrawingButton()
    End Sub

    Private Sub UpdateTodayMissingDrawingButton()
        Try
            Dim missingCount = FrmTodayMoldDrawingStatus.CountMissingDrawings()
            btnTodayMissingDrawings.Text = "Bugünün Eksik Resimleri (" & missingCount.ToString() & ")"
            btnTodayMissingDrawings.BackColor = If(missingCount > 0, Color.MistyRose, Color.Honeydew)
            btnTodayMissingDrawings.ForeColor = If(missingCount > 0, Color.DarkRed, Color.DarkGreen)
        Catch ex As Exception
            btnTodayMissingDrawings.Text = "Eksik Resimleri Kontrol Et"
            btnTodayMissingDrawings.BackColor = Color.WhiteSmoke
            btnTodayMissingDrawings.ForeColor = Color.FromArgb(45, 65, 92)
            ErrorLogService.Log("FrmProductAdmin.UpdateTodayMissingDrawingButton", ex)
        End Try
    End Sub

    Private Sub ShowTodayMissingDrawings_Click(sender As Object, e As EventArgs)
        Try
            Using form As New FrmTodayMoldDrawingStatus()
                If form.ShowDialog(Me) <> DialogResult.OK Then
                    UpdateTodayMissingDrawingButton()
                    Return
                End If

                LoadGrid()
                txtGridFilter.Clear()
                cboGridStatus.SelectedIndex = 0
                ApplyGridFilter()

                Dim wantedTr = FrmTodayMoldDrawingStatus.NormalizeTrCode(form.SelectedTrCode)
                Dim product = allProductRows.
                    Where(Function(item) FrmTodayMoldDrawingStatus.NormalizeTrCode(item.TrCode) = wantedTr).
                    OrderByDescending(Function(item) String.Equals(item.DrawingRev, form.SelectedDrawingRev, StringComparison.OrdinalIgnoreCase)).
                    ThenByDescending(Function(item) String.Equals(item.IsActive, "YES", StringComparison.OrdinalIgnoreCase)).
                    FirstOrDefault()

                If product Is Nothing Then
                    If Not AppState.CanOpenTechnicalDrawingAdmin Then
                        MessageBox.Show("Bu TR için açılabilecek bir ürün / teknik resim kaydı bulunmuyor.", "Teknik resim kaydı yok", MessageBoxButtons.OK, MessageBoxIcon.Information)
                        Return
                    End If
                    New_Click(sender, EventArgs.Empty)
                    txtTr.Text = form.SelectedTrCode
                    txtTr.Focus()
                Else
                    SelectProductRow(product.TrCode, product.DrawingRev, product.DrawingFile)
                    txtFile.Focus()
                End If
            End Using
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Eksik teknik resimler açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ApplyGridFilter()
        If grid Is Nothing Then Return

        Dim filterText As String = txtGridFilter.Text.Trim()
        Dim statusText As String = If(cboGridStatus.SelectedItem Is Nothing, "TÜMÜ", cboGridStatus.SelectedItem.ToString())

        Dim filtered = allProductRows.AsEnumerable()

        If filterText <> "" Then
            Dim tokens = filterText.Split(New Char() {" "c, ";"c, ","c, "|"c}, StringSplitOptions.RemoveEmptyEntries)
            filtered = filtered.Where(Function(p)
                                          Dim haystack As String = (p.TrCode & " " & p.DrawingRev & " " & p.ProductName & " " & p.DrawingFile & " " & ProductInfo.NormalizeDrawingScope(p.DrawingScope) & " " & p.DisplayName).ToUpperInvariant()
                                          For Each token In tokens
                                              If Not haystack.Contains(token.ToUpperInvariant()) Then Return False
                                          Next
                                          Return True
                                      End Function)
        End If

        If statusText = "AKTİF" Then
            filtered = filtered.Where(Function(p) String.Equals(p.IsActive, "YES", StringComparison.OrdinalIgnoreCase))
        ElseIf statusText = "PASİF" Then
            filtered = filtered.Where(Function(p) Not String.Equals(p.IsActive, "YES", StringComparison.OrdinalIgnoreCase))
        End If

        Dim list = filtered.ToList()

        grid.Rows.Clear()
        For Each p In list
            Dim idx = grid.Rows.Add(p.TrCode, p.DrawingRev, ProductInfo.NormalizeDrawingScope(p.DrawingScope), p.ProductName, p.DrawingFile, p.IsActive, p.CreatedBy, p.CreatedAt)
            grid.Rows(idx).Tag = p
            If Not String.Equals(p.IsActive, "YES", StringComparison.OrdinalIgnoreCase) Then
                grid.Rows(idx).DefaultCellStyle.ForeColor = SystemColors.GrayText
            End If
        Next

        lblCount.Text = $"Kayıtlı teknik resimler: {list.Count} / {allProductRows.Count} adet"
        If allProductRows.Count = 0 Then
            lblCount.Text &= "   |   Henüz teknik resim kaydı yok. TR, revizyon ve PDF/DXF seçip Kaydet / Güncelle butonuna basın."
        ElseIf list.Count = 0 Then
            lblCount.Text &= "   |   Filtreye uygun kayıt bulunamadı."
        End If
    End Sub

    Private Sub SelectDrawing_Click(sender As Object, e As EventArgs)
        Using ofd As New OpenFileDialog()
            ofd.Filter = "Teknik Resim (*.pdf;*.dxf)|*.pdf;*.dxf|PDF Teknik Resim (*.pdf)|*.pdf|DXF Teknik Resim (*.dxf)|*.dxf"
            If ofd.ShowDialog(Me) = DialogResult.OK Then
                selectedDrawingPath = ofd.FileName
                Dim tr = FileNameUtil.SafeFileName(txtTr.Text)
                Dim rev = FileNameUtil.SafeFileName(txtRev.Text)
                If tr = "" OrElse rev = "" Then
                    MessageBox.Show("Teknik resim seçmeden önce TR Kodu ve Revizyon giriniz.", "Eksik bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    selectedDrawingPath = ""
                    Return
                End If
                txtFile.Text = BuildUniqueDrawingFileName(tr, rev, Path.GetExtension(ofd.FileName))
            End If
        End Using
    End Sub

    Private Function GetSelectedDrawingScope() As String
        Return ProductInfo.NormalizeDrawingScope(If(cboDrawingScope.SelectedItem Is Nothing, "", cboDrawingScope.SelectedItem.ToString()))
    End Function

    Private Function BuildUniqueDrawingFileName(tr As String, rev As String, sourceExtension As String) As String
        Dim safeTr = FileNameUtil.SafeFileName(tr)
        Dim safeRev = FileNameUtil.SafeFileName(rev)
        Dim scopeFolder = ProductInfo.DrawingScopeFolder(GetSelectedDrawingScope())
        Dim stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")
        Dim normalizedExtension = If(sourceExtension, "").Trim().ToLowerInvariant()
        If normalizedExtension <> ".pdf" AndAlso normalizedExtension <> ".dxf" Then normalizedExtension = ".pdf"

        Dim candidate = Path.Combine(scopeFolder, $"{safeTr}_{safeRev}_{stamp}{normalizedExtension}.enc")
        Dim counter As Integer = 1

        While File.Exists(AppPaths.ResolveDrawingFilePath(candidate))
            candidate = Path.Combine(scopeFolder, $"{safeTr}_{safeRev}_{stamp}_{counter}{normalizedExtension}.enc")
            counter += 1
        End While

        Return candidate
    End Function

    Private Sub Save_Click(sender As Object, e As EventArgs)
        Try
            If txtTr.Text.Trim() = "" OrElse txtRev.Text.Trim() = "" Then
                MessageBox.Show("TR Kodu ve Revizyon zorunludur.", "Eksik bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            If selectedDrawingPath <> "" Then
                If txtFile.Text.Trim() = "" OrElse File.Exists(AppPaths.ResolveDrawingFilePath(txtFile.Text.Trim())) Then
                    txtFile.Text = BuildUniqueDrawingFileName(txtTr.Text.Trim(), txtRev.Text.Trim(), Path.GetExtension(selectedDrawingPath))
                End If

                Dim encPath = AppPaths.ResolveDrawingFilePath(txtFile.Text.Trim())
                CryptoService.EncryptDrawing(selectedDrawingPath, encPath)
            ElseIf txtFile.Text.Trim() = "" OrElse Not File.Exists(AppPaths.ResolveDrawingFilePath(txtFile.Text.Trim())) Then
                MessageBox.Show("Yeni kayıt için PDF veya DXF seçiniz.", "Teknik resim gerekli", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim p As New ProductInfo With {
                .TrCode = txtTr.Text.Trim(),
                .ProductName = txtName.Text.Trim(),
                .DrawingRev = txtRev.Text.Trim(),
                .DrawingFile = txtFile.Text.Trim(),
                .DrawingScope = GetSelectedDrawingScope(),
                .IsActive = If(chkActive.Checked, "YES", "NO"),
                .CreatedBy = AppState.CurrentUserName,
                .CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            }

            DataService.SaveProduct(p, chkSetSamePassive.Checked)
            AuditService.Log("PRODUCT_SAVE", p.TrCode, p.DrawingRev, "Ürün / teknik resim kaydedildi. Dosya=" & p.DrawingFile)
            selectedDrawingPath = ""
            LoadGrid()
            SelectProductRow(p.TrCode, p.DrawingRev, p.DrawingFile)
            MessageBox.Show("Kayıt tamamlandı. Teknik resim aşağıdaki listede gösterildi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub DeleteSelected_Click(sender As Object, e As EventArgs)
        Try
            If Not AppState.IsAdmin Then
                MessageBox.Show("Bu işlem yalnızca ADMIN yetkisiyle yapılabilir.", "Yetki yok", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim p = GetSelectedProduct()
            If p Is Nothing Then
                MessageBox.Show("Silinecek teknik resmi listeden seçiniz.", "Seçim gerekli", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim msg As String =
                "Seçili teknik resim kaydı silinecek:" & Environment.NewLine & Environment.NewLine &
                "TR Kodu: " & p.TrCode & Environment.NewLine &
                "Revizyon: " & p.DrawingRev & Environment.NewLine &
                "Resim Tipi: " & ProductInfo.NormalizeDrawingScope(p.DrawingScope) & Environment.NewLine &
                "Şifreli Dosya: " & p.DrawingFile & Environment.NewLine & Environment.NewLine &
                "Bu işlem ürün/teknik resim kaydını ve bu TR-revizyona bağlı kontrol ölçüsü tanımlarını siler." & Environment.NewLine &
                "Ölçüm geçmişi kayıtları silinmez." & Environment.NewLine & Environment.NewLine &
                "Devam edilsin mi?"

            If MessageBox.Show(msg, "Silme Onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) <> DialogResult.Yes Then
                Return
            End If

            Dim deleteFile As Boolean =
                MessageBox.Show("Bu kayda ait şifreli teknik resim dosyası da silinsin mi?" & Environment.NewLine &
                                "Dosya başka bir teknik resim kaydı tarafından kullanılıyorsa silinmez.",
                                "Şifreli Teknik Resim Dosyası", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) = DialogResult.Yes

            Dim resultText = DataService.DeleteProduct(p.TrCode, p.DrawingRev, deleteFile, p.DrawingFile)
            AuditService.Log("PRODUCT_DELETE", p.TrCode, p.DrawingRev, resultText)

            LoadGrid()
            New_Click(sender, e)
            MessageBox.Show(resultText, "Silme tamamlandı", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Silme hatası", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub OpenPdf_Click(sender As Object, e As EventArgs)
        Try
            If txtFile.Text.Trim() = "" Then
                Dim p = GetSelectedProduct()
                If p IsNot Nothing Then txtFile.Text = p.DrawingFile
            End If

            If txtFile.Text.Trim() = "" Then
                MessageBox.Show("Açılacak teknik resmi listeden seçiniz.", "Seçim gerekli", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            TempFileService.OpenEncryptedDrawing(txtFile.Text.Trim())
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Teknik resim açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub New_Click(sender As Object, e As EventArgs)
        txtTr.Clear()
        txtName.Clear()
        txtRev.Clear()
        txtFile.Clear()
        cboDrawingScope.SelectedItem = ProductInfo.DrawingScopePlastic
        chkActive.Checked = True
        chkSetSamePassive.Checked = True
        selectedDrawingPath = ""
        grid.ClearSelection()
        txtTr.Focus()
    End Sub

    Private Sub Grid_CellClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 Then Return
        LoadProductFromRow(e.RowIndex)
    End Sub

    Private Sub Grid_DoubleClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 Then Return
        LoadProductFromRow(e.RowIndex)
        OpenPdf_Click(sender, EventArgs.Empty)
    End Sub

    Private Function GetSelectedProduct() As ProductInfo
        If grid.CurrentRow Is Nothing OrElse grid.CurrentRow.IsNewRow Then Return Nothing
        Return TryCast(grid.CurrentRow.Tag, ProductInfo)
    End Function

    Private Sub LoadProductFromRow(rowIndex As Integer)
        If rowIndex < 0 OrElse rowIndex >= grid.Rows.Count Then Return
        Dim p = TryCast(grid.Rows(rowIndex).Tag, ProductInfo)
        If p Is Nothing Then Return
        selectedDrawingPath = ""
        txtTr.Text = p.TrCode
        txtName.Text = p.ProductName
        txtRev.Text = p.DrawingRev
        txtFile.Text = p.DrawingFile
        cboDrawingScope.SelectedItem = ProductInfo.NormalizeDrawingScope(p.DrawingScope)
        chkActive.Checked = String.Equals(p.IsActive, "YES", StringComparison.OrdinalIgnoreCase)
    End Sub

    Private Sub SelectProductRow(trCode As String, drawingRev As String, Optional drawingFile As String = "")
        For Each row As DataGridViewRow In grid.Rows
            Dim p = TryCast(row.Tag, ProductInfo)
            If p Is Nothing Then Continue For
            If String.Equals(p.TrCode, trCode, StringComparison.OrdinalIgnoreCase) AndAlso
               String.Equals(p.DrawingRev, drawingRev, StringComparison.OrdinalIgnoreCase) AndAlso
               (String.IsNullOrWhiteSpace(drawingFile) OrElse String.Equals(p.DrawingFile, drawingFile, StringComparison.OrdinalIgnoreCase)) Then
                row.Selected = True
                grid.CurrentCell = row.Cells(0)
                LoadProductFromRow(row.Index)
                Exit For
            End If
        Next
    End Sub
End Class
