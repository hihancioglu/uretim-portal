Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms

Public Class FrmTestRequestEmailRecipients
    Inherits Form

    Private ReadOnly grid As New DataGridView()
    Private ReadOnly txtEmail As New TextBox()
    Private ReadOnly txtDisplayName As New TextBox()
    Private ReadOnly cboEventType As New ComboBox()
    Private ReadOnly cboDepartment As New ComboBox()
    Private ReadOnly cboRecipientType As New ComboBox()
    Private ReadOnly cboFilterEvent As New ComboBox()
    Private ReadOnly cboFilterDepartment As New ComboBox()
    Private ReadOnly cboFilterStatus As New ComboBox()
    Private ReadOnly txtFilterSearch As New TextBox()
    Private ReadOnly chkActive As New CheckBox()
    Private ReadOnly lblCount As New Label()
    Private selectedOriginalEmail As String = ""
    Private selectedOriginalEventType As String = ""
    Private selectedOriginalDepartment As String = ""
    Private isLoadingSelection As Boolean = False

    Public Sub New()
        AuthorizationService.Require(AppState.CanManageTestRequestEmailRecipients, "Test Talep Mail Alıcıları")
        AppIconService.Apply(Me)

        Text = "Test Talep Mail Alıcıları"
        StartPosition = FormStartPosition.CenterParent
        Size = New Size(1050, 620)
        MinimumSize = New Size(820, 500)
        BackColor = Color.FromArgb(243, 247, 252)
        Font = New Font("Segoe UI", 9.0F)

        BuildScreen()
        LoadGrid()
        New_Click(Me, EventArgs.Empty)
    End Sub

    Private Sub BuildScreen()
        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 5,
            .Padding = New Padding(10),
            .BackColor = BackColor
        }
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 48.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 172.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 70.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 54.0F))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Controls.Add(root)

        Dim header As New Label() With {
            .Dock = DockStyle.Fill,
            .Text = "Test / Talep Yönetimi Otomatik Mail Alıcıları",
            .BackColor = Color.FromArgb(31, 71, 126),
            .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 12.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(16, 0, 0, 0),
            .Margin = New Padding(0, 0, 0, 8)
        }
        root.Controls.Add(header, 0, 0)

        Dim editor As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 4,
            .RowCount = 4,
            .BackColor = Color.White,
            .Padding = New Padding(10),
            .Margin = New Padding(0, 0, 0, 8)
        }
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 95.0F))
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 52.0F))
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 125.0F))
        editor.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 48.0F))
        editor.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        editor.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        editor.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        editor.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        root.Controls.Add(editor, 0, 1)

        AddField(editor, "E-posta", txtEmail, 0, 0)
        AddField(editor, "Açıklama", txtDisplayName, 2, 0)
        txtEmail.PlaceholderText = "ornek@firma.com"
        txtDisplayName.PlaceholderText = "Örn. Laboratuvar / kalite ekip listesi"

        AddField(editor, "Olay", cboEventType, 0, 1)
        cboEventType.DropDownStyle = ComboBoxStyle.DropDownList
        cboEventType.Items.AddRange(TestRequestEmailNotificationService.SupportedEventTypes.
            Select(Function(eventType) TestRequestEmailNotificationService.EventDisplayName(eventType)).
            Cast(Of Object)().
            ToArray())
        cboEventType.SelectedIndex = 0

        AddField(editor, "Talep Eden Bölüm", cboDepartment, 2, 1)
        cboDepartment.DropDownStyle = ComboBoxStyle.DropDownList
        cboDepartment.Items.AddRange(TestRequestEmailNotificationService.SupportedDepartments.
            Select(Function(department) TestRequestEmailNotificationService.DepartmentDisplayName(department)).
            Cast(Of Object)().
            ToArray())
        cboDepartment.SelectedIndex = 0

        chkActive.Text = "Aktif"
        chkActive.Checked = True
        chkActive.Dock = DockStyle.Left
        chkActive.Margin = New Padding(5, 8, 5, 6)
        editor.Controls.Add(chkActive, 1, 2)

        cboRecipientType.DropDownStyle = ComboBoxStyle.DropDownList
        cboRecipientType.Items.AddRange(New Object() {"Kime", "CC"})
        cboRecipientType.SelectedItem = "Kime"
        AddField(editor, "Gönderim Türü", cboRecipientType, 2, 2)

        Dim hint As New Label() With {
            .Dock = DockStyle.Fill,
            .Text = "Olay ve talep eden bölüm birlikte filtrelenir. Aktif Kime alıcıları ana alıcıya, CC seçilen aktif alıcılar bilgi alanına eklenir. Test ataması mail göndermez.",
            .ForeColor = Color.FromArgb(70, 85, 105),
            .TextAlign = ContentAlignment.MiddleLeft,
            .AutoEllipsis = True,
            .Margin = New Padding(5, 5, 5, 5)
        }
        editor.SetColumnSpan(hint, 4)
        editor.Controls.Add(hint, 0, 3)

        root.Controls.Add(BuildFilterPanel(), 0, 2)

        Dim actions As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .BackColor = Color.White,
            .Padding = New Padding(8, 7, 8, 7),
            .Margin = New Padding(0, 0, 0, 6)
        }
        root.Controls.Add(actions, 0, 3)

        Dim btnSave As New Button()
        ConfigureButton(btnSave, "Kaydet / Güncelle", 145, Color.FromArgb(31, 71, 126), Color.White)
        AddHandler btnSave.Click, AddressOf Save_Click

        Dim btnNew As New Button()
        ConfigureButton(btnNew, "Yeni", 90, Color.White, Color.FromArgb(35, 50, 70))
        AddHandler btnNew.Click, AddressOf New_Click

        Dim btnDelete As New Button()
        ConfigureButton(btnDelete, "Seçili Alıcıyı Sil", 145, Color.MistyRose, Color.DarkRed)
        AddHandler btnDelete.Click, AddressOf Delete_Click

        Dim btnRefresh As New Button()
        ConfigureButton(btnRefresh, "Yenile", 90, Color.White, Color.FromArgb(35, 50, 70))
        AddHandler btnRefresh.Click, Sub() LoadGrid()

        Dim btnClose As New Button()
        ConfigureButton(btnClose, "Kapat", 90, Color.White, Color.FromArgb(35, 50, 70))
        AddHandler btnClose.Click, Sub() Close()

        lblCount.AutoSize = False
        lblCount.Width = 310
        lblCount.Height = 34
        lblCount.TextAlign = ContentAlignment.MiddleLeft
        lblCount.ForeColor = Color.FromArgb(31, 71, 126)
        lblCount.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        lblCount.Margin = New Padding(12, 0, 0, 0)

        actions.Controls.AddRange({btnSave, btnNew, btnDelete, btnRefresh, btnClose, lblCount})

        ConfigureGrid()
        root.Controls.Add(grid, 0, 4)
    End Sub

    Private Function BuildFilterPanel() As Control
        Dim panel As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .AutoScroll = True,
            .BackColor = Color.FromArgb(232, 239, 249),
            .Padding = New Padding(10, 7, 10, 5),
            .Margin = New Padding(0, 0, 0, 6)
        }

        cboFilterEvent.DropDownStyle = ComboBoxStyle.DropDownList
        cboFilterEvent.Items.Add("TÜMÜ")
        cboFilterEvent.Items.AddRange(TestRequestEmailNotificationService.SupportedEventTypes.
            Select(Function(eventType) TestRequestEmailNotificationService.EventDisplayName(eventType)).
            Cast(Of Object)().
            ToArray())
        cboFilterEvent.SelectedIndex = 0

        cboFilterDepartment.DropDownStyle = ComboBoxStyle.DropDownList
        cboFilterDepartment.Items.Add("TÜMÜ")
        cboFilterDepartment.Items.AddRange(TestRequestEmailNotificationService.SupportedDepartments.
            Select(Function(department) TestRequestEmailNotificationService.DepartmentDisplayName(department)).
            Cast(Of Object)().
            ToArray())
        cboFilterDepartment.SelectedIndex = 0

        cboFilterStatus.DropDownStyle = ComboBoxStyle.DropDownList
        cboFilterStatus.Items.AddRange({"TÜMÜ", "AKTİF", "PASİF"})
        cboFilterStatus.SelectedIndex = 0
        txtFilterSearch.PlaceholderText = "E-posta veya açıklama ara"

        panel.Controls.Add(BuildFilterBlock("Olay Filtresi", cboFilterEvent, 245))
        panel.Controls.Add(BuildFilterBlock("Talep Eden Bölüm", cboFilterDepartment, 185))
        panel.Controls.Add(BuildFilterBlock("Durum", cboFilterStatus, 105))
        panel.Controls.Add(BuildFilterBlock("Listede Ara", txtFilterSearch, 230))

        Dim btnClearFilters As New Button() With {
            .Text = "Filtreleri Temizle",
            .Width = 135,
            .Height = 30,
            .Margin = New Padding(4, 20, 4, 4),
            .BackColor = Color.White,
            .ForeColor = Color.FromArgb(31, 71, 126),
            .FlatStyle = FlatStyle.Flat,
            .Cursor = Cursors.Hand
        }
        AddHandler btnClearFilters.Click, AddressOf ClearFilters_Click
        panel.Controls.Add(btnClearFilters)

        AddHandler cboFilterEvent.SelectedIndexChanged, AddressOf FilterChanged
        AddHandler cboFilterDepartment.SelectedIndexChanged, AddressOf FilterChanged
        AddHandler cboFilterStatus.SelectedIndexChanged, AddressOf FilterChanged
        AddHandler txtFilterSearch.TextChanged, AddressOf FilterChanged
        Return panel
    End Function

    Private Shared Function BuildFilterBlock(caption As String, control As Control, width As Integer) As Control
        Dim block As New Panel() With {.Width = width, .Height = 48, .Margin = New Padding(0, 0, 10, 4)}
        block.Controls.Add(New Label() With {
            .Text = caption,
            .Left = 0,
            .Top = 0,
            .Width = width,
            .Height = 17,
            .Font = New Font("Segoe UI", 8.3F, FontStyle.Bold),
            .ForeColor = Color.FromArgb(31, 71, 126)
        })
        control.SetBounds(0, 20, width, 25)
        block.Controls.Add(control)
        Return block
    End Function

    Private Sub FilterChanged(sender As Object, e As EventArgs)
        If isLoadingSelection Then Return
        ResetEditorForCurrentFilter()
        LoadGrid()
    End Sub

    Private Sub ClearFilters_Click(sender As Object, e As EventArgs)
        isLoadingSelection = True
        Try
            cboFilterEvent.SelectedIndex = 0
            cboFilterDepartment.SelectedIndex = 0
            cboFilterStatus.SelectedIndex = 0
            txtFilterSearch.Clear()
        Finally
            isLoadingSelection = False
        End Try
        ResetEditorForCurrentFilter()
        LoadGrid()
    End Sub

    Private Sub ResetEditorForCurrentFilter()
        selectedOriginalEmail = ""
        selectedOriginalEventType = ""
        selectedOriginalDepartment = ""
        txtEmail.Clear()
        txtDisplayName.Clear()
        cboRecipientType.SelectedItem = "Kime"
        chkActive.Checked = True

        If cboFilterEvent.SelectedIndex > 0 Then
            SelectEventType(EventCodeFromDisplay(cboFilterEvent.Text))
        End If
        If cboFilterDepartment.SelectedIndex > 0 Then
            SelectDepartment(DepartmentCodeFromDisplay(cboFilterDepartment.Text))
        End If
    End Sub

    Private Sub ConfigureGrid()
        grid.Dock = DockStyle.Fill
        grid.ReadOnly = True
        grid.AllowUserToAddRows = False
        grid.AllowUserToDeleteRows = False
        grid.AllowUserToResizeRows = False
        grid.MultiSelect = False
        grid.RowHeadersVisible = False
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        grid.AutoGenerateColumns = False
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        grid.BackgroundColor = Color.White
        grid.BorderStyle = BorderStyle.FixedSingle
        grid.EnableHeadersVisualStyles = False
        grid.ColumnHeadersHeight = 36
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(220, 232, 247)
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(31, 50, 75)
        grid.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)

        grid.Columns.Clear()
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "EventType", .HeaderText = "BİLDİRİM OLAYI", .MinimumWidth = 210, .FillWeight = 24})
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "RequestingDepartment", .HeaderText = "TALEP EDEN BÖLÜM", .MinimumWidth = 145, .FillWeight = 17})
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "RecipientType", .HeaderText = "GÖNDERİM TÜRÜ", .MinimumWidth = 105, .FillWeight = 11})
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "Email", .HeaderText = "E-POSTA", .MinimumWidth = 220, .FillWeight = 27})
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "DisplayName", .HeaderText = "AÇIKLAMA", .MinimumWidth = 180, .FillWeight = 20})
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "IsActive", .HeaderText = "DURUM", .MinimumWidth = 90, .FillWeight = 10})
        grid.Columns.Add(New DataGridViewTextBoxColumn() With {.Name = "UpdatedAt", .HeaderText = "GÜNCELLEME", .MinimumWidth = 130, .FillWeight = 15})

        AddHandler grid.SelectionChanged, AddressOf Grid_SelectionChanged
        AddHandler grid.CellDoubleClick, Sub(sender, e) If e.RowIndex >= 0 Then LoadSelected()
    End Sub

    Private Sub LoadGrid()
        Try
            Dim allItems = DataService.GetTestRequestEmailRecipients(False)
            Dim query As IEnumerable(Of TestRequestEmailRecipient) = allItems

            If cboFilterEvent.SelectedIndex > 0 Then
                Dim selectedEventType = EventCodeFromDisplay(cboFilterEvent.Text)
                query = query.Where(Function(item) String.Equals(
                    TestRequestEmailNotificationService.NormalizeEventType(item.EventType),
                    selectedEventType,
                    StringComparison.OrdinalIgnoreCase))
            End If

            If cboFilterDepartment.SelectedIndex > 0 Then
                Dim selectedDepartment = DepartmentCodeFromDisplay(cboFilterDepartment.Text)
                query = query.Where(Function(item) String.Equals(
                    TestRequestEmailNotificationService.NormalizeDepartment(item.RequestingDepartment),
                    selectedDepartment,
                    StringComparison.OrdinalIgnoreCase))
            End If

            If String.Equals(cboFilterStatus.Text, "AKTİF", StringComparison.OrdinalIgnoreCase) Then
                query = query.Where(Function(item) String.Equals(item.IsActive, "YES", StringComparison.OrdinalIgnoreCase))
            ElseIf String.Equals(cboFilterStatus.Text, "PASİF", StringComparison.OrdinalIgnoreCase) Then
                query = query.Where(Function(item) Not String.Equals(item.IsActive, "YES", StringComparison.OrdinalIgnoreCase))
            End If

            Dim searchText = txtFilterSearch.Text.Trim().ToUpperInvariant()
            If searchText <> "" Then
                Dim tokens = searchText.Split(New Char() {" "c, ";"c, ","c}, StringSplitOptions.RemoveEmptyEntries)
                query = query.Where(
                    Function(item)
                        Dim haystack = (item.Email & " " & item.DisplayName & " " &
                                        RecipientTypeDisplay(item.RecipientType) & " " &
                                        TestRequestEmailNotificationService.EventDisplayName(item.EventType) & " " &
                                        TestRequestEmailNotificationService.DepartmentDisplayName(item.RequestingDepartment)).ToUpperInvariant()
                        Return tokens.All(Function(token) haystack.Contains(token))
                    End Function)
            End If

            Dim items = query.ToList()
            grid.Rows.Clear()
            For Each item In items
                Dim statusText = If(String.Equals(item.IsActive, "YES", StringComparison.OrdinalIgnoreCase), "AKTİF", "PASİF")
                Dim rowIndex = grid.Rows.Add(
                    TestRequestEmailNotificationService.EventDisplayName(item.EventType),
                    TestRequestEmailNotificationService.DepartmentDisplayName(item.RequestingDepartment),
                    RecipientTypeDisplay(item.RecipientType),
                    item.Email,
                    item.DisplayName,
                    statusText,
                    FormatDateTime(item.UpdatedAt))
                grid.Rows(rowIndex).Tag = item
            Next
            lblCount.Text = "Gösterilen: " & items.Count.ToString() & " / " & allItems.Count.ToString() & " alıcı"
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Alıcı listesi yüklenemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub Save_Click(sender As Object, e As EventArgs)
        Try
            Dim item As New TestRequestEmailRecipient With {
                .EventType = EventCodeFromDisplay(cboEventType.Text),
                .RequestingDepartment = DepartmentCodeFromDisplay(cboDepartment.Text),
                .Email = txtEmail.Text.Trim(),
                .DisplayName = txtDisplayName.Text.Trim(),
                .RecipientType = SelectedRecipientType(),
                .IsActive = If(chkActive.Checked, "YES", "NO")
            }
            DataService.SaveTestRequestEmailRecipient(selectedOriginalEmail, item, selectedOriginalEventType, selectedOriginalDepartment)
            LoadGrid()
            SelectByEmail(item.Email, item.EventType, item.RequestingDepartment)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Alıcı kaydedilemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Sub Delete_Click(sender As Object, e As EventArgs)
        If grid.CurrentRow Is Nothing Then Return
        Dim item = TryCast(grid.CurrentRow.Tag, TestRequestEmailRecipient)
        If item Is Nothing Then Return

        Dim answer = MessageBox.Show(
            item.Email & " alıcısı silinecek. Devam edilsin mi?",
            "Alıcıyı sil",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2)
        If answer <> DialogResult.Yes Then Return

        Try
            DataService.DeleteTestRequestEmailRecipient(item.Email, item.EventType, item.RequestingDepartment)
            LoadGrid()
            New_Click(Me, EventArgs.Empty)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Alıcı silinemedi", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub New_Click(sender As Object, e As EventArgs)
        ResetEditorForCurrentFilter()
        txtEmail.Focus()
    End Sub

    Private Sub Grid_SelectionChanged(sender As Object, e As EventArgs)
        LoadSelected()
    End Sub

    Private Sub LoadSelected()
        If grid.CurrentRow Is Nothing Then Return
        Dim item = TryCast(grid.CurrentRow.Tag, TestRequestEmailRecipient)
        If item Is Nothing Then Return

        isLoadingSelection = True
        Try
            selectedOriginalEmail = item.Email
            selectedOriginalEventType = item.EventType
            selectedOriginalDepartment = item.RequestingDepartment
            txtEmail.Text = item.Email
            txtDisplayName.Text = item.DisplayName
            cboRecipientType.SelectedItem = RecipientTypeDisplay(item.RecipientType)
            SelectEventType(item.EventType)
            SelectDepartment(item.RequestingDepartment)
            chkActive.Checked = String.Equals(item.IsActive, "YES", StringComparison.OrdinalIgnoreCase)
        Finally
            isLoadingSelection = False
        End Try
    End Sub

    Private Sub SelectByEmail(email As String, eventType As String, requestingDepartment As String)
        For Each row As DataGridViewRow In grid.Rows
            Dim item = TryCast(row.Tag, TestRequestEmailRecipient)
            If item IsNot Nothing AndAlso
               String.Equals(item.Email, email, StringComparison.OrdinalIgnoreCase) AndAlso
               String.Equals(TestRequestEmailNotificationService.NormalizeEventType(item.EventType), TestRequestEmailNotificationService.NormalizeEventType(eventType), StringComparison.OrdinalIgnoreCase) AndAlso
               String.Equals(TestRequestEmailNotificationService.NormalizeDepartment(item.RequestingDepartment), TestRequestEmailNotificationService.NormalizeDepartment(requestingDepartment), StringComparison.OrdinalIgnoreCase) Then
                row.Selected = True
                grid.CurrentCell = row.Cells("Email")
                LoadSelected()
                Exit For
            End If
        Next
    End Sub

    Private Shared Function EventCodeFromDisplay(displayText As String) As String
        For Each eventType In TestRequestEmailNotificationService.SupportedEventTypes
            If String.Equals(TestRequestEmailNotificationService.EventDisplayName(eventType), displayText, StringComparison.OrdinalIgnoreCase) Then
                Return eventType
            End If
        Next
        Return TestRequestEmailNotificationService.EventRequestCreated
    End Function

    Private Sub SelectEventType(eventType As String)
        Dim displayText = TestRequestEmailNotificationService.EventDisplayName(eventType)
        For index As Integer = 0 To cboEventType.Items.Count - 1
            If String.Equals(Convert.ToString(cboEventType.Items(index)), displayText, StringComparison.OrdinalIgnoreCase) Then
                cboEventType.SelectedIndex = index
                Return
            End If
        Next
        If cboEventType.Items.Count > 0 Then cboEventType.SelectedIndex = 0
    End Sub

    Private Shared Function DepartmentCodeFromDisplay(displayText As String) As String
        For Each department In TestRequestEmailNotificationService.SupportedDepartments
            If String.Equals(TestRequestEmailNotificationService.DepartmentDisplayName(department), displayText, StringComparison.OrdinalIgnoreCase) Then
                Return department
            End If
        Next
        Return TestRequestEmailNotificationService.AllDepartments
    End Function

    Private Sub SelectDepartment(department As String)
        Dim displayText = TestRequestEmailNotificationService.DepartmentDisplayName(department)
        For index As Integer = 0 To cboDepartment.Items.Count - 1
            If String.Equals(Convert.ToString(cboDepartment.Items(index)), displayText, StringComparison.OrdinalIgnoreCase) Then
                cboDepartment.SelectedIndex = index
                Return
            End If
        Next
        If cboDepartment.Items.Count > 0 Then cboDepartment.SelectedIndex = 0
    End Sub

    Private Function SelectedRecipientType() As String
        Return RecipientTypeDisplay(Convert.ToString(cboRecipientType.SelectedItem))
    End Function

    Private Shared Function RecipientTypeDisplay(value As String) As String
        If String.Equals(If(value, "").Trim(), "CC", StringComparison.OrdinalIgnoreCase) Then Return "CC"
        Return "Kime"
    End Function

    Private Shared Sub AddField(layout As TableLayoutPanel, caption As String, control As Control, column As Integer, row As Integer)
        layout.Controls.Add(New Label() With {
            .Dock = DockStyle.Fill,
            .Text = caption,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft,
            .Margin = New Padding(5)
        }, column, row)
        control.Dock = DockStyle.Fill
        control.Margin = New Padding(5, 6, 5, 6)
        layout.Controls.Add(control, column + 1, row)
    End Sub

    Private Shared Sub ConfigureButton(button As Button, text As String, width As Integer, backColor As Color, foreColor As Color)
        button.Text = text
        button.Width = width
        button.Height = 34
        button.BackColor = backColor
        button.ForeColor = foreColor
        button.FlatStyle = FlatStyle.Flat
        button.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        button.Margin = New Padding(4, 0, 4, 0)
        button.Cursor = Cursors.Hand
        button.UseVisualStyleBackColor = False
    End Sub

    Private Shared Function FormatDateTime(value As String) As String
        Dim parsed As DateTime
        If DateTime.TryParse(value, parsed) Then Return parsed.ToString("dd.MM.yyyy HH:mm")
        Return ""
    End Function
End Class
