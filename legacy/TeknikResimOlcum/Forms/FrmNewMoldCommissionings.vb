Imports System.Drawing
Imports System.Globalization
Imports System.Linq
Imports System.Threading.Tasks
Imports System.Windows.Forms

Public Class FrmNewMoldCommissionings
    Inherits Form

    Private ReadOnly txtSearch As New TextBox()
    Private ReadOnly cboStatus As New ComboBox()
    Private ReadOnly cboStage As New ComboBox()
    Private ReadOnly grid As New DataGridView()
    Private ReadOnly lblShown As New Label()
    Private ReadOnly lblActive As New Label()
    Private ReadOnly lblTrial As New Label()
    Private ReadOnly lblWaitingApproval As New Label()
    Private ReadOnly lblCompleted As New Label()
    Private ReadOnly contentPanel As New Panel()
    Private ReadOnly emptyStatePanel As New TableLayoutPanel()
    Private ReadOnly lblEmptyTitle As New Label()
    Private ReadOnly lblEmptyHint As New Label()
    Private ReadOnly btnEmptyNew As New Button()
    Private ReadOnly lblLoadStatus As New Label()
    Private allRows As New List(Of Dictionary(Of String, String))()
    Private isLoading As Boolean

    Public Sub New()
        AuthorizationService.Require(AppState.CanOpenNewMoldCommissioning, "Yeni Kalıp Devreye Alma")
        AppIconService.Apply(Me)
        Text = "Yeni Kalıp Devreye Alma"
        StartPosition = FormStartPosition.CenterScreen
        WindowState = FormWindowState.Maximized
        MinimumSize = New Size(1100, 680)
        BackColor = Color.FromArgb(244, 247, 251)
        Font = New Font("Segoe UI", 9.0F)
        BuildScreen()
        AddHandler Shown, AddressOf FormShown
    End Sub

    Private Sub BuildScreen()
        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 5,
            .Padding = New Padding(12),
            .BackColor = BackColor
        }
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 88))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 28))
        Controls.Add(root)

        Dim header As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 1,
            .BackColor = Color.FromArgb(37, 82, 134),
            .Margin = New Padding(0, 0, 0, 6),
            .Padding = New Padding(18, 0, 18, 0)
        }
        header.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        header.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 250))
        header.Controls.Add(New Label() With {
            .Text = "Yeni Kalıp Devreye Alma",
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 14.0F, FontStyle.Bold)
        }, 0, 0)
        header.Controls.Add(New Label() With {
            .Text = If(AppState.CanModifyNewMoldCommissioning, "KAYIT VE ONAY", "SALT OKUNUR"),
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleRight,
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        }, 1, 0)
        root.Controls.Add(header, 0, 0)

        Dim toolbar As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 9,
            .RowCount = 1,
            .BackColor = Color.White,
            .Padding = New Padding(10, 6, 10, 6),
            .Margin = New Padding(0, 0, 0, 6)
        }
        toolbar.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 55))
        toolbar.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        toolbar.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 55))
        toolbar.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 140))
        toolbar.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 55))
        toolbar.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 165))
        toolbar.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 120))
        toolbar.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 110))
        toolbar.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 100))
        toolbar.RowStyles.Add(New RowStyle(SizeType.Percent, 100))

        toolbar.Controls.Add(MakeLabel("Arama"), 0, 0)
        txtSearch.Dock = DockStyle.Fill
        txtSearch.PlaceholderText = "kayıt no / ürün / TR / kalıp / makine"
        toolbar.Controls.Add(txtSearch, 1, 0)
        toolbar.Controls.Add(MakeLabel("Durum"), 2, 0)
        cboStatus.Dock = DockStyle.Fill
        cboStatus.DropDownStyle = ComboBoxStyle.DropDownList
        cboStatus.Items.AddRange(New Object() {"TÜMÜ", "AKTİF", "TAMAMLANDI"})
        cboStatus.SelectedIndex = 0
        toolbar.Controls.Add(cboStatus, 3, 0)
        toolbar.Controls.Add(MakeLabel("Aşama"), 4, 0)
        cboStage.Dock = DockStyle.Fill
        cboStage.DropDownStyle = ComboBoxStyle.DropDownList
        cboStage.Items.AddRange(New Object() {"TÜMÜ", "Talep", "Kalıphane Ön Kabul", "Denemeler", "Ölçüm / Doğrulama", "Düzeltmeler", "Nihai Onay"})
        cboStage.SelectedIndex = 0
        toolbar.Controls.Add(cboStage, 5, 0)

        Dim btnNew = MakeButton("Yeni Kayıt", Color.FromArgb(25, 128, 72), Color.White)
        Dim btnDetail = MakeButton("Detay Aç", Color.FromArgb(37, 82, 134), Color.White)
        Dim btnRefresh = MakeButton("Yenile", Color.White, Color.FromArgb(31, 71, 126))
        If AppState.CanModifyNewMoldCommissioning Then
            toolbar.Controls.Add(btnNew, 6, 0)
            AddHandler btnNew.Click, Sub() OpenDetail("")
        Else
            toolbar.Controls.Add(New Panel() With {.Dock = DockStyle.Fill}, 6, 0)
        End If
        toolbar.Controls.Add(btnDetail, 7, 0)
        toolbar.Controls.Add(btnRefresh, 8, 0)

        AddHandler btnDetail.Click, Sub() OpenSelected()
        AddHandler btnRefresh.Click, Async Sub() Await LoadRowsAsync()
        AddHandler txtSearch.TextChanged, Sub() ApplyFilter()
        AddHandler cboStatus.SelectedIndexChanged, Sub() ApplyFilter()
        AddHandler cboStage.SelectedIndexChanged, Sub() ApplyFilter()
        root.Controls.Add(toolbar, 0, 1)

        Dim kpis As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 5,
            .RowCount = 1,
            .BackColor = BackColor,
            .Margin = New Padding(0, 0, 0, 6)
        }
        For i = 0 To 4
            kpis.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 20))
        Next
        AddKpi(kpis, 0, "GÖSTERİLEN", lblShown, Color.FromArgb(37, 82, 134))
        AddKpi(kpis, 1, "AKTİF", lblActive, Color.FromArgb(214, 145, 24))
        AddKpi(kpis, 2, "DENEMEDE", lblTrial, Color.FromArgb(90, 82, 170))
        AddKpi(kpis, 3, "ONAY BEKLİYOR", lblWaitingApproval, Color.FromArgb(193, 47, 47))
        AddKpi(kpis, 4, "TAMAMLANDI", lblCompleted, Color.FromArgb(36, 145, 79))
        root.Controls.Add(kpis, 0, 2)

        ConfigureGrid()
        BuildContentArea()
        root.Controls.Add(contentPanel, 0, 3)

        lblLoadStatus.Dock = DockStyle.Fill
        lblLoadStatus.TextAlign = ContentAlignment.MiddleLeft
        lblLoadStatus.ForeColor = Color.FromArgb(87, 101, 119)
        lblLoadStatus.Padding = New Padding(8, 0, 0, 0)
        lblLoadStatus.Text = "Kayıtlar hazırlanıyor..."
        root.Controls.Add(lblLoadStatus, 0, 4)
    End Sub

    Private Sub BuildContentArea()
        contentPanel.Dock = DockStyle.Fill
        contentPanel.BackColor = Color.White
        contentPanel.Margin = New Padding(0)
        contentPanel.Controls.Add(grid)

        emptyStatePanel.ColumnCount = 1
        emptyStatePanel.RowCount = 4
        emptyStatePanel.Size = New Size(560, 225)
        emptyStatePanel.BackColor = Color.White
        emptyStatePanel.BorderStyle = BorderStyle.FixedSingle
        emptyStatePanel.Padding = New Padding(28, 22, 28, 22)
        emptyStatePanel.Anchor = AnchorStyles.None
        emptyStatePanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        emptyStatePanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 44))
        emptyStatePanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 52))
        emptyStatePanel.RowStyles.Add(New RowStyle(SizeType.Absolute, 46))
        emptyStatePanel.RowStyles.Add(New RowStyle(SizeType.Percent, 100))

        lblEmptyTitle.Dock = DockStyle.Fill
        lblEmptyTitle.TextAlign = ContentAlignment.MiddleCenter
        lblEmptyTitle.ForeColor = Color.FromArgb(20, 57, 101)
        lblEmptyTitle.Font = New Font("Segoe UI", 14.0F, FontStyle.Bold)
        emptyStatePanel.Controls.Add(lblEmptyTitle, 0, 0)

        lblEmptyHint.Dock = DockStyle.Fill
        lblEmptyHint.TextAlign = ContentAlignment.TopCenter
        lblEmptyHint.ForeColor = Color.FromArgb(87, 101, 119)
        lblEmptyHint.Font = New Font("Segoe UI", 9.5F)
        emptyStatePanel.Controls.Add(lblEmptyHint, 0, 1)

        btnEmptyNew.Text = "İlk Kaydı Oluştur"
        btnEmptyNew.Anchor = AnchorStyles.None
        btnEmptyNew.Size = New Size(170, 36)
        btnEmptyNew.BackColor = Color.FromArgb(25, 128, 72)
        btnEmptyNew.ForeColor = Color.White
        btnEmptyNew.FlatStyle = FlatStyle.Flat
        btnEmptyNew.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        AddHandler btnEmptyNew.Click, Sub() OpenDetail("")
        emptyStatePanel.Controls.Add(btnEmptyNew, 0, 2)

        contentPanel.Controls.Add(emptyStatePanel)
        AddHandler contentPanel.Resize, Sub()
                                            emptyStatePanel.Left = Math.Max(0, (contentPanel.ClientSize.Width - emptyStatePanel.Width) \ 2)
                                            emptyStatePanel.Top = Math.Max(0, (contentPanel.ClientSize.Height - emptyStatePanel.Height) \ 2)
                                        End Sub
        emptyStatePanel.BringToFront()
    End Sub

    Private Sub ConfigureGrid()
        grid.Dock = DockStyle.Fill
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.AllowUserToResizeRows = False
        grid.ReadOnly = True
        grid.MultiSelect = False
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.AutoGenerateColumns = False
        grid.BackgroundColor = Color.White
        grid.BorderStyle = BorderStyle.FixedSingle
        grid.RowHeadersVisible = False
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(218, 230, 244)
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(15, 50, 92)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 8.5F, FontStyle.Bold)
        grid.ColumnHeadersHeight = 42
        grid.RowTemplate.Height = 31
        AddTextColumn("CommissioningId", "KAYIT NO", 165)
        AddTextColumn("Status", "DURUM", 90)
        AddTextColumn("CurrentStage", "AŞAMA", 150)
        AddTextColumn("ProductCode", "ÜRÜN / TR", 120)
        AddTextColumn("ProductName", "ÜRÜN ADI", 230, DataGridViewAutoSizeColumnMode.Fill)
        AddTextColumn("MoldCode", "KALIP KODU", 130)
        AddTextColumn("PlannedMachine", "PLANLANAN MAKİNE", 135)
        AddTextColumn("CavityCount", "GÖZ", 60)
        AddTextColumn("RequestedProductionDate", "İLK ÜRETİM", 110)
        AddTextColumn("UpdatedAt", "SON GÜNCELLEME", 145)
        AddHandler grid.CellDoubleClick, Sub(s, e)
                                             If e.RowIndex >= 0 Then OpenSelected()
                                         End Sub
    End Sub

    Private Async Sub FormShown(sender As Object, e As EventArgs)
        Await LoadRowsAsync()
    End Sub

    Private Async Function LoadRowsAsync() As Task
        If isLoading Then Return
        SetLoadingState(True)
        Try
            allRows = Await Task.Run(
                Function()
                    CsvUtil.EnsureFile(AppPaths.NewMoldCommissioningsCsv, DataService.NewMoldCommissioningHeaders)
                    Return CsvUtil.ReadRows(AppPaths.NewMoldCommissioningsCsv)
                End Function)
            ApplyFilter()
        Catch ex As Exception
            ErrorLogService.Log("FrmNewMoldCommissionings.LoadRowsAsync", ex)
            MessageBox.Show("Kalıp devreye alma kayıtları okunamadı: " & ex.Message, "Kayıtlar", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            SetLoadingState(False)
        End Try
    End Function

    Private Sub SetLoadingState(value As Boolean)
        isLoading = value
        txtSearch.Enabled = Not value
        cboStatus.Enabled = Not value
        cboStage.Enabled = Not value
        If value Then
            grid.Visible = False
            emptyStatePanel.Visible = True
            lblEmptyTitle.Text = "Kayıtlar yükleniyor..."
            lblEmptyHint.Text = "Yalnızca kalıp devreye alma kayıtları okunuyor."
            btnEmptyNew.Visible = False
            lblLoadStatus.Text = "Kayıtlar yükleniyor..."
        Else
            UpdateContentState()
        End If
    End Sub

    Private Sub ApplyFilter()
        Dim query = txtSearch.Text.Trim()
        Dim status = If(cboStatus.SelectedItem, "TÜMÜ").ToString()
        Dim stage = If(cboStage.SelectedItem, "TÜMÜ").ToString()
        Dim filtered = allRows.Where(
            Function(row)
                If status <> "TÜMÜ" AndAlso Not String.Equals(DataService.GetValue(row, "Status"), status, StringComparison.OrdinalIgnoreCase) Then Return False
                If stage <> "TÜMÜ" AndAlso Not String.Equals(DataService.GetValue(row, "CurrentStage"), stage, StringComparison.OrdinalIgnoreCase) Then Return False
                If query.Length = 0 Then Return True
                Return DataService.NewMoldCommissioningHeaders.Any(
                    Function(key) DataService.GetValue(row, key).IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0)
            End Function).
            OrderByDescending(Function(row) ParseDate(DataService.GetValue(row, "UpdatedAt"))).
            ToList()

        grid.Rows.Clear()
        For Each item In filtered
            Dim index = grid.Rows.Add()
            Dim row = grid.Rows(index)
            For Each column As DataGridViewColumn In grid.Columns
                row.Cells(column.Name).Value = DataService.GetValue(item, column.Name)
            Next
            row.Tag = DataService.GetValue(item, "CommissioningId")
            If String.Equals(DataService.GetValue(item, "Status"), "TAMAMLANDI", StringComparison.OrdinalIgnoreCase) Then
                row.DefaultCellStyle.BackColor = Color.FromArgb(230, 247, 235)
            ElseIf String.Equals(DataService.GetValue(item, "CurrentStage"), "Nihai Onay", StringComparison.OrdinalIgnoreCase) Then
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 245, 215)
            End If
        Next

        lblShown.Text = filtered.Count.ToString()
        lblActive.Text = Enumerable.Count(allRows, Function(r) Not String.Equals(DataService.GetValue(r, "Status"), "TAMAMLANDI", StringComparison.OrdinalIgnoreCase)).ToString()
        lblTrial.Text = Enumerable.Count(allRows, Function(r) String.Equals(DataService.GetValue(r, "CurrentStage"), "Denemeler", StringComparison.OrdinalIgnoreCase)).ToString()
        lblWaitingApproval.Text = Enumerable.Count(allRows, Function(r) String.Equals(DataService.GetValue(r, "CurrentStage"), "Nihai Onay", StringComparison.OrdinalIgnoreCase) AndAlso Not String.Equals(DataService.GetValue(r, "Status"), "TAMAMLANDI", StringComparison.OrdinalIgnoreCase)).ToString()
        lblCompleted.Text = Enumerable.Count(allRows, Function(r) String.Equals(DataService.GetValue(r, "Status"), "TAMAMLANDI", StringComparison.OrdinalIgnoreCase)).ToString()
        If Not isLoading Then UpdateContentState()
    End Sub

    Private Sub UpdateContentState()
        Dim hasVisibleRows = grid.Rows.Count > 0
        grid.Visible = hasVisibleRows
        emptyStatePanel.Visible = Not hasVisibleRows
        If hasVisibleRows Then
            lblLoadStatus.Text = grid.Rows.Count.ToString() & " kayıt gösteriliyor. Detay için satıra çift tıklayın."
            Return
        End If

        Dim hasAnyRecords = allRows.Count > 0
        lblEmptyTitle.Text = If(hasAnyRecords, "Filtreye uygun kayıt bulunamadı", "Henüz kalıp devreye alma kaydı yok")
        lblEmptyHint.Text = If(hasAnyRecords,
                               "Arama veya filtreleri değiştirin; sonuçlar otomatik güncellenir.",
                               "İlk devreye alma sürecini başlatmak için yeni bir kayıt oluşturun.")
        btnEmptyNew.Visible = AppState.CanModifyNewMoldCommissioning AndAlso Not hasAnyRecords
        lblLoadStatus.Text = If(hasAnyRecords, "Filtre sonucu boş.", "Kayıt bulunmuyor.")
        emptyStatePanel.BringToFront()
    End Sub

    Private Sub OpenSelected()
        If grid.CurrentRow Is Nothing OrElse grid.CurrentRow.Tag Is Nothing Then
            MessageBox.Show("Önce bir kayıt seçin.", "Yeni Kalıp Devreye Alma", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        OpenDetail(grid.CurrentRow.Tag.ToString())
    End Sub

    Private Async Sub OpenDetail(id As String)
        Try
            Using detail As New FrmNewMoldCommissioningDetail(id)
                detail.ShowDialog(Me)
            End Using
            Await LoadRowsAsync()
        Catch ex As Exception
            ErrorLogService.Log("FrmNewMoldCommissionings.OpenDetail", ex)
            MessageBox.Show(
                "Kalıp devreye alma detay ekranı açılamadı." & Environment.NewLine &
                "Ayrıntı: " & ex.Message,
                "Detay Ekranı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub AddTextColumn(name As String, header As String, width As Integer, Optional mode As DataGridViewAutoSizeColumnMode = DataGridViewAutoSizeColumnMode.None)
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {
            .Name = name,
            .HeaderText = header,
            .Width = width,
            .AutoSizeMode = mode,
            .SortMode = DataGridViewColumnSortMode.Automatic
        })
    End Sub

    Private Shared Function MakeLabel(text As String) As Label
        Return New Label() With {.Text = text, .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft, .Font = New Font("Segoe UI", 8.5F, FontStyle.Bold)}
    End Function

    Private Shared Function MakeButton(text As String, backColor As Color, foreColor As Color) As Button
        Return New Button() With {.Text = text, .Dock = DockStyle.Fill, .Margin = New Padding(5, 2, 0, 2), .BackColor = backColor, .ForeColor = foreColor, .FlatStyle = FlatStyle.Flat, .Font = New Font("Segoe UI", 8.5F, FontStyle.Bold)}
    End Function

    Private Shared Sub AddKpi(host As TableLayoutPanel, column As Integer, title As String, valueLabel As Label, accent As Color)
        Dim panel As New Panel() With {.Dock = DockStyle.Fill, .BackColor = Color.White, .Margin = New Padding(0, 0, 8, 0), .Padding = New Padding(10, 6, 8, 6)}
        Dim accentPanel As New Panel() With {.Dock = DockStyle.Left, .Width = 4, .BackColor = accent}
        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .Padding = New Padding(10, 0, 0, 0),
            .Margin = New Padding(0)
        }
        layout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 28.0F))
        layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        layout.Controls.Add(New Label() With {
            .Text = title,
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .ForeColor = Color.FromArgb(70, 82, 98),
            .Font = New Font("Segoe UI", 8.5F, FontStyle.Bold),
            .Margin = New Padding(0),
            .Padding = New Padding(0)
        }, 0, 0)
        valueLabel.Dock = DockStyle.Fill
        valueLabel.TextAlign = ContentAlignment.MiddleLeft
        valueLabel.ForeColor = accent
        valueLabel.Font = New Font("Segoe UI", 15.0F, FontStyle.Bold)
        valueLabel.Margin = New Padding(0)
        valueLabel.Padding = New Padding(0)
        valueLabel.AutoSize = False
        valueLabel.UseCompatibleTextRendering = False
        layout.Controls.Add(valueLabel, 0, 1)
        panel.Controls.Add(layout)
        panel.Controls.Add(accentPanel)
        host.Controls.Add(panel, column, 0)
    End Sub

    Private Shared Function ParseDate(value As String) As DateTime
        Dim parsed As DateTime
        If DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, parsed) OrElse DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, parsed) Then Return parsed
        Return DateTime.MinValue
    End Function
End Class
