Imports System
Imports System.Collections.Generic
Imports System.Data
Imports System.Diagnostics
Imports System.Drawing
Imports System.Globalization
Imports System.Drawing.Drawing2D
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports Microsoft.VisualBasic.FileIO

Public Class MainForm
    Inherits Form

    Private Const InternalIdColumn As String = "__APP_ROW_ID"
    Private Const InoTalepTarihiColumn As String = "INO TALEP TARİHİ"
    Private Const DefaultCsvFileName As String = "INO_Database.csv"
    Private Const MaxCsvBackupCount As Integer = 30

    Private ReadOnly dataDirectory As String
    Private ReadOnly integratedMode As Boolean
    Private ReadOnly integratedRoleName As String
    Private ReadOnly integratedAuditWriter As Action(Of String, String, String, String)
    Private ReadOnly forcedReadOnlyMode As Boolean

    Private csvPath As String = ""
    Private delimiter As String = ";"

    Private columns As New List(Of String)()
    Private columnMap As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
    Private aliases As Dictionary(Of String, String())

    Private table As DataTable
    Private view As DataView

    Private nextInternalId As Integer = 1
    Private activeFilter As String = "ALL"
    Private isUpdating As Boolean = False
    Private isSaving As Boolean = False
    Private isClosingAfterSave As Boolean = False
    Private isCloseSavePending As Boolean = False
    Private hasUnsavedChanges As Boolean = False
    Private databaseReadOnlyMode As Boolean = False
    Private ownsDatabaseLock As Boolean = False
    Private instanceLockStream As FileStream
    Private instanceLockPath As String = ""
    Private instanceLockInfoPath As String = ""
    Private idleTimer As Timer
    Private activityFilter As AppActivityMessageFilter

    Private dashboardTotal As Integer = 0
    Private dashboardApproved As Integer = 0
    Private dashboardPending As Integer = 0
    Private dashboardRejected As Integer = 0
    Private dashboardCheck As Integer = 0
    Private sessionAddedRows As New List(Of SessionAddedRowInfo)()
    Private ReadOnly pendingDataAuditEntries As New List(Of PendingAuditEntry)()
    Private searchTimer As Timer
    Private lastGridColumnCount As Integer = -1

    Private dgv As DataGridView
    Private txtSearch As TextBox
    Private cboColumnFilter As ComboBox
    Private txtColumnFilter As TextBox
    Private lblColumnFilterCount As Label
    Private lblStatus As Label
    Private chkAutoSave As CheckBox

    Private userStore As UserStore
    Private currentUser As String = ""
    Private btnNewRow As Button
    Private btnDeleteRow As Button
    Private btnLogin As Button
    Private btnLogout As Button
    Private btnChangePassword As Button
    Private btnEmailDraft As Button
    Private btnColumnSelect As Button
    Private lblCurrentUser As Label
    Private lblFilterResult As Label
    Private ReadOnly filterButtons As New Dictionary(Of String, Button)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly filterAccentColors As New Dictionary(Of String, Color)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly columnFilters As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly toolTips As New ToolTip()
    Private isLoadingColumnFilter As Boolean = False
    Private responsiveBandLayoutUpdater As Action
    Private isApplyingResponsiveBandLayout As Boolean = False

    Private lblTotal As Label
    Private lblApproved As Label
    Private lblPending As Label
    Private lblRejected As Label
    Private lblCheck As Label

    Private ReadOnly statusColumns As String() = {"GENEL DURUM", "İNO-1", "İNO-2"}

    Public Sub New()
        Me.New(AppDomain.CurrentDomain.BaseDirectory, "", "", Nothing)
    End Sub

    Public Sub New(moduleDataDirectory As String,
                   initialUser As String,
                   initialRoleName As String,
                   Optional centralAuditWriter As Action(Of String, String, String, String) = Nothing,
                   Optional forceReadOnly As Boolean = False)
        dataDirectory = If(moduleDataDirectory, "").Trim()
        If dataDirectory = "" Then dataDirectory = AppDomain.CurrentDomain.BaseDirectory
        Directory.CreateDirectory(dataDirectory)

        integratedMode = forceReadOnly OrElse Not String.IsNullOrWhiteSpace(initialUser)
        integratedRoleName = If(initialRoleName, "").Trim()
        integratedAuditWriter = centralAuditWriter
        currentUser = If(initialUser, "").Trim()
        forcedReadOnlyMode = forceReadOnly
        databaseReadOnlyMode = forceReadOnly

        Me.Text = "İNO-1 / İNO-2 TAKİP FORMU"
        Me.WindowState = FormWindowState.Maximized
        Me.MinimumSize = New Size(1200, 720)
        Me.Font = New Font("Segoe UI", 9.0F)

        InitAliases()
        userStore = New UserStore(dataDirectory, Not integratedMode)

        AppIconHelper.ApplyIcon(Me)
        InitUi()
        CreateEmptyTable()

        AddHandler Me.Shown, AddressOf MainForm_Shown
        AddHandler Me.FormClosing, AddressOf MainForm_FormClosing
        InitIdleTimer()
    End Sub

    Private Function CurrentAppUserForLog() As String
        If String.IsNullOrWhiteSpace(currentUser) Then Return "GİRİŞ YOK"
        Return currentUser
    End Function

    Private NotInheritable Class PendingAuditEntry
        Public Property ActionName As String
        Public Property Sira As String
        Public Property SayacAdi As String
        Public Property Details As String
    End Class

    Private Sub WriteLog(actionName As String, Optional row As DataRow = Nothing, Optional details As String = "")
        Dim sira As String = ""
        Dim sayac As String = ""

        Try
            If row IsNot Nothing AndAlso row.RowState <> DataRowState.Deleted Then
                sira = GetField(row, "SIRA")
                sayac = GetField(row, "SAYAÇ ADI")
            End If
        Catch
        End Try

        WriteLogValues(actionName, sira, sayac, details)
    End Sub

    Private Sub WriteLogValues(actionName As String, sira As String, sayac As String, details As String)
        If integratedAuditWriter Is Nothing Then Return

        Try
            integratedAuditWriter.Invoke(actionName, If(sira, ""), If(sayac, ""), If(details, ""))
        Catch
            ' Merkezi log hedefindeki bir sorun da ana İNO işlemini geri almamalıdır.
        End Try
    End Sub

    Private Sub QueueDataChangeLog(actionName As String, row As DataRow, details As String)
        Dim sira As String = ""
        Dim sayac As String = ""

        Try
            If row IsNot Nothing AndAlso row.RowState <> DataRowState.Deleted Then
                sira = GetField(row, "SIRA")
                sayac = GetField(row, "SAYAÇ ADI")
            End If
        Catch
        End Try

        pendingDataAuditEntries.Add(New PendingAuditEntry With {
            .ActionName = actionName,
            .Sira = sira,
            .SayacAdi = sayac,
            .Details = details
        })
        hasUnsavedChanges = True
    End Sub

    Private Sub FlushPendingDataChangeLogs()
        If pendingDataAuditEntries.Count = 0 Then Return

        Dim entries = pendingDataAuditEntries.ToList()
        pendingDataAuditEntries.Clear()

        For Each entry In entries
            WriteLogValues(entry.ActionName, entry.Sira, entry.SayacAdi, entry.Details)
        Next
    End Sub

    Private Function BuildChangeDetail(beforeValues As Dictionary(Of String, String), afterValues As Dictionary(Of String, String)) As String
        If beforeValues Is Nothing OrElse afterValues Is Nothing Then Return ""

        Dim changes As New List(Of String)()

        For Each col In columns
            Dim beforeText As String = If(beforeValues.ContainsKey(col), beforeValues(col), "")
            Dim afterText As String = If(afterValues.ContainsKey(col), afterValues(col), "")

            If Not String.Equals(beforeText, afterText, StringComparison.Ordinal) Then
                changes.Add(col & ": [" & beforeText & "] -> [" & afterText & "]")
            End If
        Next

        If changes.Count = 0 Then Return "Değişiklik yok"

        Return String.Join(" ; ", changes)
    End Function

    Private Sub InitIdleTimer()
        ' Entegre kullanımda kapanma süresini ana TeknikResimOlcum penceresi yönetir.
        ' İkinci bir sayaç İNO penceresini kullanıcıya sormadan kapatmamalıdır.
        If integratedMode Then Return

        idleTimer = New Timer()
        idleTimer.Interval = 15 * 60 * 1000
        AddHandler idleTimer.Tick, AddressOf IdleTimer_Tick
        idleTimer.Start()

        activityFilter = New AppActivityMessageFilter(Sub() ResetIdleTimer())
        Application.AddMessageFilter(activityFilter)
    End Sub

    Private Sub ResetIdleTimer()
        If idleTimer Is Nothing Then Return

        idleTimer.Stop()
        idleTimer.Start()
    End Sub

    Private Sub IdleTimer_Tick(sender As Object, e As EventArgs)
        If idleTimer IsNot Nothing Then idleTimer.Stop()

        isClosingAfterSave = False
        Me.Close()
    End Sub

    Private Sub CleanupClosingResources()
        If idleTimer IsNot Nothing Then idleTimer.Stop()
        If searchTimer IsNot Nothing Then searchTimer.Stop()

        If activityFilter IsNot Nothing Then
            Application.RemoveMessageFilter(activityFilter)
            activityFilter = Nothing
        End If
    End Sub

    Private Async Sub MainForm_FormClosing(sender As Object, e As FormClosingEventArgs)
        If databaseReadOnlyMode Then
            CleanupClosingResources()
            Return
        End If

        If isClosingAfterSave Then
            CleanupClosingResources()
            ReleaseDatabaseLock()
            Return
        End If

        If table Is Nothing OrElse String.IsNullOrWhiteSpace(csvPath) Then
            CleanupClosingResources()
            ReleaseDatabaseLock()
            Return
        End If

        ' Yeni/düzenlenmiş/silinmiş satırlar zaten işlem anında kaydedilir.
        ' Değişiklik yokken tüm ağ CSV'sini tekrar yazmak pencerenin kapanmasını gereksiz yere geciktiriyordu.
        If Not hasUnsavedChanges AndAlso pendingDataAuditEntries.Count = 0 Then
            CleanupClosingResources()
            ReleaseDatabaseLock()
            Return
        End If

        e.Cancel = True
        If isCloseSavePending Then Return

        isCloseSavePending = True
        If idleTimer IsNot Nothing Then idleTimer.Stop()
        If searchTimer IsNot Nothing Then searchTimer.Stop()

        Dim saved = Await SaveCurrentCsvSilentlyAsync()

        If saved Then
            Try
                WriteLog("PROGRAM KAPANIŞ", Nothing, "Program kapanırken bekleyen değişiklikler kaydedildi.")
            Catch
            End Try

            isClosingAfterSave = True
            Me.Close()
        Else
            isCloseSavePending = False
            lblStatus.Text = "Kapanış iptal edildi; CSV kaydedilemedi."
            If idleTimer IsNot Nothing Then idleTimer.Start()
        End If
    End Sub

    Private Async Sub MainForm_Shown(sender As Object, e As EventArgs)
        RemoveHandler Me.Shown, AddressOf MainForm_Shown

        Dim defaultCsvPath = GetDefaultCsvPath()

        If databaseReadOnlyMode Then
            Me.Text &= " - SALT OKUNUR"
            UpdatePermissionUi()
            Dim readOnlyDetail = If(forcedReadOnlyMode,
                                    "Planlama rolü için salt okunur oturum başlatıldı.",
                                    "Salt okunur oturum başlatıldı.")
            WriteLog("PROGRAM AÇILIŞ SALT OKUNUR", Nothing, readOnlyDetail)
        ElseIf Not AcquireDatabaseLock(defaultCsvPath) Then
            databaseReadOnlyMode = True
            Me.Text &= " - SALT OKUNUR"
            UpdatePermissionUi()
            WriteLog("PROGRAM AÇILIŞ SALT OKUNUR", Nothing, "Veritabanı başka kullanıcıda açık; salt okunur oturum başlatıldı.")
        End If

        If File.Exists(defaultCsvPath) Then
            Await LoadCsvAsync(defaultCsvPath)
            WriteLog("PROGRAM AÇILIŞ", Nothing, "Veritabanı yüklendi.")
        Else
            lblStatus.Text = "Varsayılan CSV bulunamadı: " & defaultCsvPath
            MessageBox.Show("Program açılırken otomatik yüklenecek CSV dosyası bulunamadı:" &
                            Environment.NewLine & defaultCsvPath &
                            Environment.NewLine & Environment.NewLine &
                            "Lütfen INO_Database.csv dosyasını EXE ile aynı klasöre koyun.",
                            "CSV Bulunamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End If
    End Sub

    Private Function AcquireDatabaseLock(databasePath As String) As Boolean
        Try
            Dim folder = System.IO.Path.GetDirectoryName(databasePath)
            If String.IsNullOrWhiteSpace(folder) Then folder = AppDomain.CurrentDomain.BaseDirectory

            instanceLockPath = System.IO.Path.Combine(folder, "INO_Database.lock")
            instanceLockInfoPath = System.IO.Path.Combine(folder, "INO_Database.lockinfo")

            Dim info = $"Bilgisayar: {Environment.MachineName}{Environment.NewLine}Windows Kullanıcısı: {Environment.UserName}{Environment.NewLine}Açılış Zamanı: {DateTime.Now:yyyy-MM-dd HH:mm:ss}"

            ' Önce gerçek kilit alınır. Kilit alınamazsa mevcut lockinfo dosyası bozulmaz.
            instanceLockStream = New FileStream(instanceLockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)

            ' lockinfo dosyası paylaşılabilir şekilde tutulur; kilit alınamazsa kullanıcıya kimin açık tuttuğu gösterilir.
            File.WriteAllText(instanceLockInfoPath, info, Encoding.UTF8)

            Dim bytes = Encoding.UTF8.GetBytes(info)
            instanceLockStream.SetLength(0)
            instanceLockStream.Write(bytes, 0, bytes.Length)
            instanceLockStream.Flush()
            ownsDatabaseLock = True

            Return True
        Catch ex As IOException
            Dim lockOwner = ReadLockOwnerInfo()

            MessageBox.Show("INO_Database.csv şu anda başka bir bilgisayarda veya başka bir program oturumunda açık görünüyor." &
                            Environment.NewLine & Environment.NewLine &
                            lockOwner &
                            Environment.NewLine & Environment.NewLine &
                            "Ekran salt okunur olarak açılacaktır.",
                            "Veritabanı Salt Okunur Açılıyor", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return False
        Catch ex As Exception
            MessageBox.Show("Veritabanı kilidi oluşturulamadı:" & Environment.NewLine & ex.Message,
                            "Kilit Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return False
        End Try
    End Function

    Private Function ReadLockOwnerInfo() As String
        Try
            If Not String.IsNullOrWhiteSpace(instanceLockInfoPath) AndAlso File.Exists(instanceLockInfoPath) Then
                Return File.ReadAllText(instanceLockInfoPath, Encoding.UTF8)
            End If
        Catch
        End Try

        Return "Açık tutan bilgisayar/kullanıcı bilgisi okunamadı."
    End Function

    Private Sub ReleaseDatabaseLock()
        If Not ownsDatabaseLock Then Return

        Try
            If instanceLockStream IsNot Nothing Then
                instanceLockStream.Dispose()
                instanceLockStream = Nothing
            End If

            ownsDatabaseLock = False

            If Not String.IsNullOrWhiteSpace(instanceLockPath) AndAlso File.Exists(instanceLockPath) Then
                Try
                    File.Delete(instanceLockPath)
                Catch
                End Try
            End If

            If Not String.IsNullOrWhiteSpace(instanceLockInfoPath) AndAlso File.Exists(instanceLockInfoPath) Then
                Try
                    File.Delete(instanceLockInfoPath)
                Catch
                End Try
            End If
        Catch
        End Try
    End Sub

    Private Function GetDefaultCsvPath() As String
        Return System.IO.Path.Combine(dataDirectory, DefaultCsvFileName)
    End Function

    Private Sub InitAliases()
        aliases = New Dictionary(Of String, String())(StringComparer.OrdinalIgnoreCase) From {
            {"INO TALEP TARİHİ", New String() {"INO TALEP TARİHİ", "INO TALEP TARIHI", "INO TALEP TARİH", "INO TALEP TARIH", "INO TALEBI TARIHI", "INO TALEBİ TARİHİ"}},
            {"SIRA", New String() {"SIRA", "SIRANO", "SIRA NO", "NO"}},
            {"SAYAÇ ADI", New String() {"SAYAÇADI", "SAYACADI", "SAYAÇ ADI", "SAYAC ADI"}},
            {"SİPARİŞ YERİ", New String() {"SİPARİŞYERİ", "SIPARISYERI", "SİPARİŞ YERİ", "SIPARIS YERI"}},
            {"İŞ EMRİ NO", New String() {"İŞEMRİNO", "ISEMRINO", "İŞ EMRİ NO", "IS EMRI NO", "İŞ EMRI NO"}},
            {"INO-1 VERİLEN BÖLÜM", New String() {"INO1VERİLENBÖLÜM", "INO1VERILENBOLUM", "INO-1 VERİLEN BÖLÜM", "INO 1 VERILEN BOLUM"}},
            {"INO-1 ONAY TARİHİ", New String() {"INO1ONAYTARİHİ", "INO1ONAYTARIHI", "INO-1 ONAY TARİHİ", "INO 1 ONAY TARIHI", "INO1ONAYTARIH"}},
            {"INO-1 ONAY VEREN", New String() {"INO1ONAYVEREN", "INO1ONAYIVEREN", "INO-1 ONAY VEREN", "INO-1 ONAYI VEREN"}},
            {"INO-1 RAPOR NO", New String() {"INO1RAPORNO", "INO-1 RAPOR NO", "INO 1 RAPOR NO"}},
            {"INO-1 DURUM", New String() {"INO1DURUM", "INO-1 DURUM", "INO 1 DURUM"}},
            {"INO-2 ONAY TARİHİ", New String() {"INO2ONAYTARİHİ", "INO2ONAYTARIHI", "INO-2 ONAY TARİHİ", "INO 2 ONAY TARIHI", "INO2ONAYTARIH"}},
            {"INO-2 ONAY VEREN", New String() {"INO2ONAYVEREN", "INO2ONAYIVEREN", "INO-2 ONAY VEREN", "INO-2 ONAYI VEREN"}},
            {"INO-2 RAPOR NO", New String() {"INO2RAPORNO", "INO-2 RAPOR NO", "INO 2 RAPOR NO"}},
            {"INO-2 DURUM", New String() {"INO2DURUM", "INO-2 DURUM", "INO 2 DURUM"}},
            {"Q4", New String() {"Q4"}},
            {"Q3", New String() {"Q3"}},
            {"ARA DEBİ", New String() {"ARADEBİ", "ARADEBI", "ARA DEBİ", "ARA DEBI"}},
            {"Q2", New String() {"Q2"}},
            {"Q1", New String() {"Q1"}},
            {"TAM (+)", New String() {"TAM(+)", "TAM +", "TAMPLUS", "TAMARTI"}},
            {"TAM (-)", New String() {"TAM(-)", "TAM -", "TAMMINUS", "TAMEKSI", "TAMEKSİ"}},
            {"AÇIKLAMA", New String() {"AÇIKLAMA", "ACIKLAMA"}}
        }
    End Sub

    Private Sub InitUi()
        Dim root As New TableLayoutPanel()
        root.Dock = DockStyle.Fill
        root.RowCount = 4
        root.ColumnCount = 1
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 72))
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 164))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        Me.Controls.Add(root)

        Dim docHeader = CreateDocumentHeader()
        root.Controls.Add(docHeader, 0, 0)

        Dim commandBar As New TableLayoutPanel()
        commandBar.Dock = DockStyle.Fill
        commandBar.ColumnCount = 2
        commandBar.RowCount = 1
        commandBar.Padding = New Padding(8, 8, 8, 6)
        commandBar.BackColor = Color.White
        commandBar.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        commandBar.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 420))
        root.Controls.Add(commandBar, 0, 1)

        Dim actionPanel As New FlowLayoutPanel()
        actionPanel.Dock = DockStyle.Fill
        actionPanel.WrapContents = True
        actionPanel.AutoScroll = False
        actionPanel.Margin = New Padding(0)
        commandBar.Controls.Add(actionPanel, 0, 0)

        btnNewRow = MakeButton("Yeni Kayıt", Color.FromArgb(15, 123, 63))
        AddHandler btnNewRow.Click, AddressOf BtnNew_Click
        toolTips.SetToolTip(btnNewRow, "Yeni bir İNO takip kaydı oluşturur.")
        actionPanel.Controls.Add(btnNewRow)

        btnLogin = MakeButton("Kullanıcı Girişi", Color.FromArgb(31, 78, 121))
        AddHandler btnLogin.Click, AddressOf BtnLogin_Click
        actionPanel.Controls.Add(btnLogin)

        btnLogout = MakeButton("Kullanıcı Çıkışı", Color.FromArgb(180, 35, 24))
        AddHandler btnLogout.Click, AddressOf BtnLogout_Click
        actionPanel.Controls.Add(btnLogout)

        btnChangePassword = MakeButton("Şifre Değiştir", Color.FromArgb(71, 84, 103))
        AddHandler btnChangePassword.Click, AddressOf BtnChangePassword_Click
        actionPanel.Controls.Add(btnChangePassword)

        Dim btnDashboard = MakeButton("Genel Bakış", Color.FromArgb(31, 78, 121))
        AddHandler btnDashboard.Click, AddressOf BtnDashboard_Click
        toolTips.SetToolTip(btnDashboard, "Toplam, onaylı, bekleyen ve olumsuz kayıtların özetini açar.")
        actionPanel.Controls.Add(btnDashboard)

        btnColumnSelect = MakeButton("Görünen Sütunlar", Color.FromArgb(71, 84, 103))
        AddHandler btnColumnSelect.Click, AddressOf BtnColumnSelect_Click
        toolTips.SetToolTip(btnColumnSelect, "Tabloda görmek istediğiniz sütunları seçmenizi sağlar.")
        actionPanel.Controls.Add(btnColumnSelect)

        btnEmailDraft = MakeButton("E-posta Oluştur", Color.FromArgb(15, 123, 63))
        AddHandler btnEmailDraft.Click, AddressOf BtnEmailDraft_Click
        toolTips.SetToolTip(btnEmailDraft, "Seçilen kayıtlar için e-posta taslağı oluşturur.")
        actionPanel.Controls.Add(btnEmailDraft)

        btnDeleteRow = MakeButton("Seçili Kaydı Sil", Color.FromArgb(180, 35, 24))
        btnDeleteRow.Margin = New Padding(18, 4, 4, 4)
        AddHandler btnDeleteRow.Click, AddressOf BtnDelete_Click
        toolTips.SetToolTip(btnDeleteRow, "Tabloda seçili kaydı siler. Bu işlem için onay istenir.")
        actionPanel.Controls.Add(btnDeleteRow)

        Dim currentUserHost As New Panel()
        currentUserHost.Dock = DockStyle.Fill
        currentUserHost.Margin = New Padding(0)
        currentUserHost.Padding = New Padding(8, 4, 0, 0)
        currentUserHost.BackColor = Color.White
        commandBar.Controls.Add(currentUserHost, 1, 0)

        lblCurrentUser = New Label()
        lblCurrentUser.Text = "Kullanıcı: Giriş yapılmadı"
        lblCurrentUser.Dock = DockStyle.Top
        lblCurrentUser.Height = 40
        lblCurrentUser.Margin = New Padding(0)
        lblCurrentUser.Padding = New Padding(12, 0, 12, 0)
        lblCurrentUser.TextAlign = ContentAlignment.MiddleLeft
        lblCurrentUser.BackColor = Color.FromArgb(238, 244, 252)
        lblCurrentUser.ForeColor = Color.FromArgb(31, 78, 121)
        lblCurrentUser.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        currentUserHost.Controls.Add(lblCurrentUser)

        ' Durum etiketi iç işlemler için korunur ancak ekranda gösterilmez.
        lblStatus = New Label()
        lblStatus.Text = ""
        lblStatus.AutoSize = True
        lblStatus.Visible = False

        Dim filterFrame As New Panel()
        filterFrame.Dock = DockStyle.Fill
        filterFrame.Padding = New Padding(12, 8, 12, 8)
        filterFrame.Margin = New Padding(10, 4, 10, 6)
        filterFrame.BackColor = Color.FromArgb(248, 250, 252)
        filterFrame.BorderStyle = BorderStyle.FixedSingle
        root.Controls.Add(filterFrame, 0, 2)

        Dim filterLayout As New TableLayoutPanel()
        filterLayout.Dock = DockStyle.Fill
        filterLayout.ColumnCount = 1
        filterLayout.RowCount = 3
        filterLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 44))
        filterLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 44))
        filterLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        filterFrame.Controls.Add(filterLayout)

        Dim searchRow As New TableLayoutPanel()
        searchRow.Dock = DockStyle.Fill
        searchRow.ColumnCount = 3
        searchRow.RowCount = 1
        searchRow.Margin = New Padding(0)
        searchRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 100))
        searchRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        searchRow.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 240))
        filterLayout.Controls.Add(searchRow, 0, 0)

        Dim lblSearch As New Label()
        lblSearch.Text = "Listede ara"
        lblSearch.Dock = DockStyle.Fill
        lblSearch.TextAlign = ContentAlignment.MiddleLeft
        lblSearch.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        lblSearch.ForeColor = Color.FromArgb(52, 64, 84)
        searchRow.Controls.Add(lblSearch, 0, 0)

        txtSearch = New TextBox()
        txtSearch.Dock = DockStyle.Fill
        txtSearch.AutoSize = False
        txtSearch.Height = 34
        txtSearch.Margin = New Padding(0, 4, 12, 4)
        txtSearch.Font = New Font("Segoe UI", 10.0F)
        txtSearch.PlaceholderText = "Sayaç adı, iş emri, sipariş yeri veya açıklama yazın..."
        AddHandler txtSearch.TextChanged, AddressOf SearchTextChangedDebounced
        searchRow.Controls.Add(txtSearch, 1, 0)

        lblFilterResult = New Label()
        lblFilterResult.Text = "Tüm kayıtlar gösteriliyor"
        lblFilterResult.Dock = DockStyle.Fill
        lblFilterResult.TextAlign = ContentAlignment.MiddleRight
        lblFilterResult.Font = New Font("Segoe UI", 8.5F, FontStyle.Bold)
        lblFilterResult.ForeColor = Color.FromArgb(71, 84, 103)
        searchRow.Controls.Add(lblFilterResult, 2, 0)

        searchTimer = New Timer()
        searchTimer.Interval = 350
        AddHandler searchTimer.Tick, AddressOf SearchTimer_Tick

        Dim columnFilterPanel As New FlowLayoutPanel()
        columnFilterPanel.Dock = DockStyle.Fill
        columnFilterPanel.WrapContents = True
        columnFilterPanel.AutoScroll = False
        columnFilterPanel.Margin = New Padding(0)
        filterLayout.Controls.Add(columnFilterPanel, 0, 1)

        Dim filterPanel As New FlowLayoutPanel()
        filterPanel.Dock = DockStyle.Fill
        filterPanel.WrapContents = True
        filterPanel.AutoScroll = False
        filterPanel.Margin = New Padding(0)
        filterLayout.Controls.Add(filterPanel, 0, 2)

        Dim lblStatusFilter As New Label()
        lblStatusFilter.Text = "Durum"
        lblStatusFilter.AutoSize = False
        lblStatusFilter.Width = 96
        lblStatusFilter.Height = 40
        lblStatusFilter.TextAlign = ContentAlignment.MiddleLeft
        lblStatusFilter.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        lblStatusFilter.ForeColor = Color.FromArgb(52, 64, 84)
        filterPanel.Controls.Add(lblStatusFilter)

        AddFilterButton(filterPanel, "Tümü", "ALL", Color.FromArgb(31, 78, 121), Color.White)
        AddFilterButton(filterPanel, "Tam Onaylı", "TAM ONAYLI", Color.FromArgb(15, 123, 63), Color.White)
        AddFilterButton(filterPanel, "Bekleyen", "BEKLİYOR", Color.FromArgb(183, 121, 31), Color.White)
        AddFilterButton(filterPanel, "Uygun Değil", "RED / UYGUN DEĞİL", Color.FromArgb(180, 35, 24), Color.White)
        AddFilterButton(filterPanel, "Kontrol Gerekir", "KONTROL GEREKİR", Color.FromArgb(105, 65, 198), Color.White)
        AddFilterButton(filterPanel, "İNO-1 Bekleyen", "INO1_BEKLEYEN", Color.FromArgb(34, 113, 160), Color.White)
        AddFilterButton(filterPanel, "İNO-2 Bekleyen", "INO2_BEKLEYEN", Color.FromArgb(42, 111, 110), Color.White)

        Dim columnFilterHost As New Panel() With {
            .Width = 485,
            .Height = 40,
            .Margin = New Padding(16, 4, 4, 4),
            .BackColor = Color.Transparent
        }

        Dim lblColumnFilter As New Label() With {
            .Text = "Sütun",
            .Left = 0,
            .Top = 0,
            .Width = 52,
            .Height = 40,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .ForeColor = Color.FromArgb(52, 64, 84)
        }
        columnFilterHost.Controls.Add(lblColumnFilter)

        cboColumnFilter = New ComboBox() With {
            .Left = 56,
            .Top = 7,
            .Width = 170,
            .Height = 27,
            .DropDownWidth = 300,
            .DropDownStyle = ComboBoxStyle.DropDownList
        }
        AddHandler cboColumnFilter.SelectedIndexChanged, AddressOf ColumnFilterColumnChanged
        toolTips.SetToolTip(cboColumnFilter, "Filtrelemek istediğiniz sütunu seçin.")
        columnFilterHost.Controls.Add(cboColumnFilter)

        txtColumnFilter = New TextBox() With {
            .Left = 234,
            .Top = 7,
            .Width = 175,
            .Height = 27,
            .PlaceholderText = "Bu sütunda ara..."
        }
        AddHandler txtColumnFilter.TextChanged, AddressOf ColumnFilterTextChanged
        toolTips.SetToolTip(txtColumnFilter, "Yazdığınız değer yalnızca seçili sütunda aranır. Birden fazla sütun filtresi birlikte çalışır.")
        columnFilterHost.Controls.Add(txtColumnFilter)

        lblColumnFilterCount = New Label() With {
            .Text = "Aktif: 0",
            .Left = 415,
            .Top = 0,
            .Width = 70,
            .Height = 40,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Font = New Font("Segoe UI", 8.5F, FontStyle.Bold),
            .ForeColor = Color.FromArgb(71, 84, 103)
        }
        columnFilterHost.Controls.Add(lblColumnFilterCount)
        columnFilterPanel.Controls.Add(columnFilterHost)

        Dim btnClearColumnFilters = MakeButton("Sütun Filtrelerini Temizle", Color.FromArgb(71, 84, 103))
        btnClearColumnFilters.Margin = New Padding(4)
        toolTips.SetToolTip(btnClearColumnFilters, "Yalnızca sütunlara özel filtreleri temizler.")
        AddHandler btnClearColumnFilters.Click, AddressOf ClearColumnFilters_Click
        columnFilterPanel.Controls.Add(btnClearColumnFilters)

        Dim btnClearFilter = MakeButton("Filtreleri Temizle", Color.FromArgb(71, 84, 103))
        btnClearFilter.Margin = New Padding(14, 4, 4, 4)
        toolTips.SetToolTip(btnClearFilter, "Genel aramayı, durum filtresini ve sütun filtrelerini temizler.")
        AddHandler btnClearFilter.Click, Sub()
                                             activeFilter = "ALL"
                                             txtSearch.Text = ""
                                             columnFilters.Clear()
                                             LoadSelectedColumnFilterText()
                                             ApplyFilter()
                                         End Sub
        filterPanel.Controls.Add(btnClearFilter)
        UpdateFilterButtonStyles()

        dgv = New DataGridView()
        dgv.Dock = DockStyle.Fill
        dgv.AllowUserToAddRows = False
        dgv.AllowUserToDeleteRows = False
        dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        dgv.MultiSelect = False
        dgv.AutoGenerateColumns = True
        dgv.BackgroundColor = Color.White
        dgv.BorderStyle = BorderStyle.None
        dgv.EnableHeadersVisualStyles = False
        dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(233, 238, 245)
        dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(23, 32, 51)
        dgv.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 8.5F, FontStyle.Bold)
        dgv.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False
        dgv.DefaultCellStyle.WrapMode = DataGridViewTriState.False
        dgv.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None
        dgv.RowTemplate.Height = 26
        dgv.RowHeadersVisible = False
        dgv.EditMode = DataGridViewEditMode.EditProgrammatically
        dgv.ReadOnly = True
        dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None

        AddHandler dgv.DataBindingComplete, AddressOf Dgv_DataBindingComplete
        AddHandler dgv.CellEndEdit, AddressOf Dgv_CellEndEdit
        AddHandler dgv.CurrentCellDirtyStateChanged, AddressOf Dgv_CurrentCellDirtyStateChanged
        AddHandler dgv.CellDoubleClick, AddressOf Dgv_CellDoubleClick
        AddHandler dgv.CellFormatting, AddressOf Dgv_CellFormatting

        root.Controls.Add(dgv, 0, 3)

        responsiveBandLayoutUpdater =
            Sub()
                If isApplyingResponsiveBandLayout OrElse root.IsDisposed OrElse commandBar.IsDisposed OrElse
                   filterFrame.IsDisposed OrElse filterLayout.IsDisposed Then Return

                isApplyingResponsiveBandLayout = True
                Try
                    Dim dpiScale = Math.Max(1.0R, DeviceDpi / 96.0R)
                    Dim rootWidth = Math.Max(520, root.ClientSize.Width)
                    Dim rootHeight = Math.Max(420, root.ClientSize.Height)

                    Dim userColumnWidth = Math.Max(270, Math.Min(420, CInt(Math.Round(rootWidth * 0.34R))))
                    commandBar.ColumnStyles(1).Width = userColumnWidth

                    Dim actionWidth = Math.Max(260, rootWidth - userColumnWidth - commandBar.Padding.Horizontal)
                    actionPanel.AutoScroll = False
                    Dim actionPreferredHeight = Math.Max(
                        CInt(Math.Round(48 * dpiScale)),
                        actionPanel.GetPreferredSize(New Size(actionWidth, 0)).Height)
                    Dim commandHeight = Math.Max(
                        CInt(Math.Round(72 * dpiScale)),
                        actionPreferredHeight + commandBar.Padding.Vertical)

                    Dim filterWidth = Math.Max(300, rootWidth - filterFrame.Margin.Horizontal - filterFrame.Padding.Horizontal - 4)
                    columnFilterPanel.AutoScroll = False
                    filterPanel.AutoScroll = False

                    Dim columnPreferredHeight = Math.Max(
                        CInt(Math.Round(44 * dpiScale)),
                        columnFilterPanel.GetPreferredSize(New Size(filterWidth, 0)).Height)
                    Dim statusPreferredHeight = Math.Max(
                        CInt(Math.Round(48 * dpiScale)),
                        filterPanel.GetPreferredSize(New Size(filterWidth, 0)).Height)

                    Dim fixedFilterHeight = CInt(Math.Round(44 * dpiScale))
                    Dim filterChromeHeight = filterFrame.Padding.Vertical + filterFrame.Margin.Vertical + 4
                    Dim desiredFilterHeight = fixedFilterHeight + columnPreferredHeight + statusPreferredHeight + filterChromeHeight

                    Dim minimumGridHeight = Math.Max(110, CInt(Math.Round(120 * dpiScale)))
                    Dim maximumFilterHeight = Math.Max(
                        CInt(Math.Round(140 * dpiScale)),
                        rootHeight - CInt(Math.Round(58 * dpiScale)) - commandHeight - minimumGridHeight)
                    Dim filterHeight = Math.Min(desiredFilterHeight, maximumFilterHeight)

                    Dim availableDynamicFilterHeight = Math.Max(
                        CInt(Math.Round(88 * dpiScale)),
                        filterHeight - fixedFilterHeight - filterChromeHeight)
                    Dim columnHeight = columnPreferredHeight
                    Dim statusHeight = statusPreferredHeight

                    If columnHeight + statusHeight > availableDynamicFilterHeight Then
                        Dim columnShare = Math.Max(
                            CInt(Math.Round(44 * dpiScale)),
                            CInt(Math.Round(availableDynamicFilterHeight * 0.42R)))
                        columnHeight = Math.Min(columnPreferredHeight, columnShare)
                        statusHeight = Math.Max(
                            CInt(Math.Round(44 * dpiScale)),
                            availableDynamicFilterHeight - columnHeight)
                    End If

                    filterLayout.RowStyles(0).SizeType = SizeType.Absolute
                    filterLayout.RowStyles(0).Height = fixedFilterHeight
                    filterLayout.RowStyles(1).SizeType = SizeType.Absolute
                    filterLayout.RowStyles(1).Height = columnHeight
                    filterLayout.RowStyles(2).SizeType = SizeType.Absolute
                    filterLayout.RowStyles(2).Height = statusHeight

                    actionPanel.AutoScroll = actionPreferredHeight + commandBar.Padding.Vertical > commandHeight
                    columnFilterPanel.AutoScroll = columnPreferredHeight > columnHeight
                    filterPanel.AutoScroll = statusPreferredHeight > statusHeight

                    root.RowStyles(1).Height = commandHeight
                    root.RowStyles(2).Height = filterHeight
                    root.PerformLayout()
                Catch ex As Exception
                    ' Yerleşim hatası formun çalışmasını engellememelidir.
                Finally
                    isApplyingResponsiveBandLayout = False
                End Try
            End Sub

        AddHandler ClientSizeChanged, Sub() responsiveBandLayoutUpdater.Invoke()
        AddHandler Shown, Sub() responsiveBandLayoutUpdater.Invoke()
        AddHandler DpiChanged,
            Sub()
                If IsHandleCreated AndAlso Not IsDisposed Then
                    BeginInvoke(New System.Windows.Forms.MethodInvoker(Sub() responsiveBandLayoutUpdater.Invoke()))
                End If
            End Sub

        UpdatePermissionUi()
        responsiveBandLayoutUpdater.Invoke()
    End Sub

    Private Function CreateDocumentHeader() As Control
        ' Tek başlık alanı: form adı ve doküman bilgileri aynı satırda gösterilir.
        Dim outer As New Panel()
        outer.Dock = DockStyle.Fill
        outer.BackColor = Color.White
        outer.Padding = New Padding(8, 4, 8, 4)

        Dim headerBox As New Panel()
        headerBox.Dock = DockStyle.Fill
        headerBox.BackColor = Color.White
        headerBox.BorderStyle = BorderStyle.FixedSingle
        headerBox.Padding = New Padding(10, 4, 10, 4)
        outer.Controls.Add(headerBox)

        Dim content As New TableLayoutPanel()
        content.Dock = DockStyle.Fill
        content.ColumnCount = 2
        content.RowCount = 1
        content.BackColor = Color.White
        content.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 58))
        content.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 42))
        content.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        headerBox.Controls.Add(content)

        Dim titleLabel As New Label()
        titleLabel.Text = "İNO-1 / İNO-2 TAKİP FORMU"
        titleLabel.Dock = DockStyle.Fill
        titleLabel.TextAlign = ContentAlignment.MiddleCenter
        titleLabel.Font = New Font("Segoe UI", 14.0F, FontStyle.Bold)
        titleLabel.ForeColor = Color.FromArgb(23, 32, 51)
        titleLabel.AutoEllipsis = True
        content.Controls.Add(titleLabel, 0, 0)

        Dim metaLabel As New Label()
        metaLabel.Text = "Yayım Tarihi: 21.11.2019   |   Rev. No/Tarihi: 03/23.11.2020   |   Doküman No: F.442"
        metaLabel.Dock = DockStyle.Fill
        metaLabel.TextAlign = ContentAlignment.MiddleRight
        metaLabel.Font = New Font("Segoe UI", 7.6F, FontStyle.Bold)
        metaLabel.ForeColor = Color.FromArgb(23, 32, 51)
        metaLabel.AutoEllipsis = True
        content.Controls.Add(metaLabel, 1, 0)

        Return outer
    End Function

    Private Function GetCurrentUserRole() As String
        If integratedMode Then Return integratedRoleName
        If String.IsNullOrWhiteSpace(currentUser) OrElse userStore Is Nothing Then Return ""
        Return userStore.GetRole(currentUser)
    End Function

    Private Function HasFullEditPrivilege() As Boolean
        If databaseReadOnlyMode Then Return False

        Dim roleName = GetCurrentUserRole()

        Return String.Equals(roleName, UserStore.RoleAdmin, StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(roleName, UserStore.RoleMechanism, StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(currentUser, "ADMİN", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(currentUser, "MEKANİZMA", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Function HasApprovalEditPrivilege() As Boolean
        If databaseReadOnlyMode Then Return False
        If HasFullEditPrivilege() Then Return True

        Dim roleName = GetCurrentUserRole()

        Return String.Equals(roleName, UserStore.RoleApproval, StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(currentUser, "OZAN ÇAĞLAYAN", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(currentUser, "GÜLİZ KARTAL", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(currentUser, "NESLİHAN ŞENOL", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(currentUser, "AYAR", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Function HasEditPrivilege() As Boolean
        Return HasApprovalEditPrivilege()
    End Function

    Private Function IsAdminUser() As Boolean
        Dim roleName = GetCurrentUserRole()

        Return String.Equals(roleName, UserStore.RoleAdmin, StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(currentUser, "ADMİN", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Sub UpdatePermissionUi()
        Dim fullEditAllowed = HasFullEditPrivilege()
        Dim loggedIn = Not String.IsNullOrWhiteSpace(currentUser)

        ' Aktif olmayan butonlar görünmez.
        If btnNewRow IsNot Nothing Then btnNewRow.Visible = fullEditAllowed
        If btnDeleteRow IsNot Nothing Then btnDeleteRow.Visible = fullEditAllowed

        If btnLogin IsNot Nothing Then btnLogin.Visible = Not loggedIn AndAlso Not databaseReadOnlyMode
        If btnLogout IsNot Nothing Then btnLogout.Visible = loggedIn AndAlso Not databaseReadOnlyMode

        If btnChangePassword IsNot Nothing Then
            btnChangePassword.Visible = loggedIn AndAlso Not databaseReadOnlyMode
            btnChangePassword.Enabled = loggedIn AndAlso Not databaseReadOnlyMode
            btnChangePassword.Text = If(IsAdminUser(), "Kullanıcı Yönetimi", "Şifre Değiştir")
        End If

        If integratedMode Then
            If btnLogin IsNot Nothing Then btnLogin.Visible = False
            If btnLogout IsNot Nothing Then btnLogout.Visible = False
            If btnChangePassword IsNot Nothing Then btnChangePassword.Visible = False
        End If

        If btnColumnSelect IsNot Nothing Then
            btnColumnSelect.Visible = loggedIn AndAlso Not databaseReadOnlyMode
            btnColumnSelect.Enabled = loggedIn AndAlso Not databaseReadOnlyMode
        End If

        If btnEmailDraft IsNot Nothing Then
            btnEmailDraft.Visible = fullEditAllowed
            btnEmailDraft.Enabled = fullEditAllowed
        End If

        If lblCurrentUser IsNot Nothing Then
            lblCurrentUser.Text = BuildCurrentUserStatusText()

            If databaseReadOnlyMode Then
                lblCurrentUser.BackColor = Color.FromArgb(255, 243, 205)
                lblCurrentUser.ForeColor = Color.FromArgb(133, 77, 14)
            Else
                lblCurrentUser.BackColor = Color.FromArgb(238, 244, 252)
                lblCurrentUser.ForeColor = Color.FromArgb(31, 78, 121)
            End If
        End If

        If lblStatus IsNot Nothing Then
            lblStatus.Text = ""
        End If

        If responsiveBandLayoutUpdater IsNot Nothing Then responsiveBandLayoutUpdater.Invoke()
    End Sub

    Private Function BuildCurrentUserStatusText() As String
        Dim userText = If(String.IsNullOrWhiteSpace(currentUser), "Giriş yapılmadı", currentUser.Trim())
        Dim roleText = If(GetCurrentUserRole(), "").Trim()

        If forcedReadOnlyMode Then
            If String.IsNullOrWhiteSpace(roleText) Then roleText = "Salt okunur"
            Return $"Rol: {roleText}  |  Kullanıcı: {userText}  |  Salt okunur"
        End If

        If integratedMode AndAlso Not String.IsNullOrWhiteSpace(roleText) Then
            Dim integratedText = $"Kullanıcı: {userText}  |  Rol: {roleText}"
            If databaseReadOnlyMode Then integratedText &= "  |  Salt okunur"
            Return integratedText
        End If

        Dim text = "Kullanıcı: " & userText
        If databaseReadOnlyMode Then text &= "  |  Salt okunur"
        Return text
    End Function

    Private Function GetReadOnlyMessage() As String
        If forcedReadOnlyMode Then
            Return "Bu kullanıcı rolü İNO-1 / İNO-2 Takip ekranını yalnızca okuyucu olarak görebilir. Kayıt ekleme, düzenleme, silme veya onay işlemi yapılamaz."
        End If

        Return "Veritabanı başka bir kullanıcı tarafından düzenleniyor. Bu oturum salt okunurdur."
    End Function

    Private Function RequireEditPrivilege() As Boolean
        If databaseReadOnlyMode Then
            MessageBox.Show(GetReadOnlyMessage(),
                            "Salt Okunur Oturum", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return False
        End If

        If HasApprovalEditPrivilege() Then Return True

        MessageBox.Show("Bu işlem için yetkili kullanıcı girişi yapılmalıdır.",
                        "Yetki Gerekli", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Return False
    End Function

    Private Function RequireFullEditPrivilege() As Boolean
        If databaseReadOnlyMode Then
            MessageBox.Show(GetReadOnlyMessage(),
                            "Salt Okunur Oturum", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return False
        End If

        If HasFullEditPrivilege() Then Return True

        MessageBox.Show("Bu işlem için MEKANİZMA veya ADMİN kullanıcısı ile giriş yapılmalıdır.",
                        "Yetki Gerekli", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Return False
    End Function

    Private Sub BtnLogin_Click(sender As Object, e As EventArgs)
        Using frm As New LoginForm(userStore)
            If frm.ShowDialog(Me) = DialogResult.OK Then
                currentUser = frm.LoggedInUser
                UpdatePermissionUi()
                WriteLog("KULLANICI GİRİŞ", Nothing, "Giriş başarılı.")
            End If
        End Using
    End Sub

    Private Sub BtnLogout_Click(sender As Object, e As EventArgs)
        WriteLog("KULLANICI ÇIKIŞ", Nothing, "Çıkış yapıldı.")
        currentUser = ""
        UpdatePermissionUi()
    End Sub

    Private Sub BtnChangePassword_Click(sender As Object, e As EventArgs)
        If String.IsNullOrWhiteSpace(currentUser) Then
            MessageBox.Show("Şifre değiştirmek için önce kullanıcı girişi yapın.",
                            "Kullanıcı Girişi Gerekli", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        If IsAdminUser() Then
            Using frm As New UserManagementForm(userStore, currentUser)
                If frm.ShowDialog(Me) = DialogResult.OK Then
                    WriteLog("KULLANICI YÖNETİMİ", Nothing, "Kullanıcı yönetimi işlemi yapıldı.")
                End If
            End Using
        Else
            Using frm As New PasswordChangeForm(userStore, currentUser, False)
                If frm.ShowDialog(Me) = DialogResult.OK Then
                    WriteLog("ŞİFRE DEĞİŞİKLİĞİ", Nothing, "Şifre değişikliği yapıldı.")
                End If
            End Using
        End If
    End Sub

    Private Sub BtnDashboard_Click(sender As Object, e As EventArgs)
        UpdateDashboard()

        Dim periodStats = BuildDashboardPeriodStats()

        Using frm As New DashboardForm(dashboardTotal, dashboardApproved, dashboardPending, dashboardRejected, dashboardCheck, periodStats)
            frm.ShowDialog(Me)
        End Using
    End Sub

    Private Function BuildDashboardPeriodStats() As List(Of DashboardPeriodStat)
        Dim today = DateTime.Today
        Dim weekStartOffset = (CInt(today.DayOfWeek) + 6) Mod 7
        Dim weekStart = today.AddDays(-weekStartOffset)
        Dim monthStart = New DateTime(today.Year, today.Month, 1)

        Dim result As New List(Of DashboardPeriodStat) From {
            New DashboardPeriodStat("Bugün"),
            New DashboardPeriodStat("Bu Hafta"),
            New DashboardPeriodStat("Bu Ay")
        }

        If table Is Nothing Then Return result

        For Each dr As DataRow In table.Rows
            If dr.RowState = DataRowState.Deleted Then Continue For

            Dim rowDate As DateTime
            If Not TryGetRowRequestDate(dr, rowDate) Then Continue For

            If rowDate.Date = today Then AddRowToPeriodStat(result(0), dr)
            If rowDate.Date >= weekStart AndAlso rowDate.Date <= today Then AddRowToPeriodStat(result(1), dr)
            If rowDate.Date >= monthStart AndAlso rowDate.Date <= today Then AddRowToPeriodStat(result(2), dr)
        Next

        Return result
    End Function

    Private Function TryGetRowRequestDate(row As DataRow, ByRef parsedDate As DateTime) As Boolean
        parsedDate = DateTime.MinValue

        Dim raw = GetField(row, InoTalepTarihiColumn)

        If String.IsNullOrWhiteSpace(raw) Then Return False

        Dim formats = New String() {"dd.MM.yyyy", "d.M.yyyy", "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd"}

        If DateTime.TryParseExact(raw.Trim(), formats, CultureInfo.GetCultureInfo("tr-TR"), DateTimeStyles.None, parsedDate) Then
            Return True
        End If

        Return DateTime.TryParse(raw, CultureInfo.GetCultureInfo("tr-TR"), DateTimeStyles.None, parsedDate)
    End Function

    Private Sub AddRowToPeriodStat(stat As DashboardPeriodStat, row As DataRow)
        stat.Total += 1

        ' Bunlar CSV'den gelen alanlar değil, uygulamanın hesapladığı DataTable sütunlarıdır.
        ' GetField yalnız CSV alias haritasını okuduğu için burada her zaman boş dönüyordu.
        Dim genel = Clean(row("GENEL DURUM"))
        Dim ino1 = Clean(row("İNO-1"))
        Dim ino2 = Clean(row("İNO-2"))

        If genel = "TAM ONAYLI" Then stat.Approved += 1
        If genel = "BEKLİYOR" Then stat.Pending += 1
        If genel = "RED / UYGUN DEĞİL" Then stat.Rejected += 1
        If genel = "KONTROL GEREKİR" Then stat.CheckRequired += 1

        If ino1 = "BEKLİYOR" OrElse ino1 = "" Then stat.Ino1Pending += 1
        If ino2 = "BEKLİYOR" OrElse ino2 = "" Then stat.Ino2Pending += 1
    End Sub

    Private Function ColumnVisibilityPath() As String
        Return System.IO.Path.Combine(dataDirectory, "INO_ColumnVisibility.txt")
    End Function

    Private Function DefaultVisibleColumns() As List(Of String)
        Return New List(Of String) From {
            "GENEL DURUM",
            "İNO-1",
            "İNO-2",
            "SIRA",
            "SIRA NO",
            "SAYAÇ ADI",
            "SİPARİŞ YERİ",
            "İŞ EMRİ NO",
            "INO TALEP TARİHİ",
            "AÇIKLAMA"
        }
    End Function

    Private Function LoadVisibleColumnPreference() As List(Of String)
        Dim path = ColumnVisibilityPath()

        If Not File.Exists(path) Then Return Nothing

        Dim result As New List(Of String)()
        Dim seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        For Each line In File.ReadAllLines(path, Encoding.UTF8)
            Dim t = Clean(line)
            If t.Length > 0 AndAlso Not seen.Contains(t) Then
                result.Add(t)
                seen.Add(t)
            End If
        Next

        If result.Count = 0 Then Return Nothing

        Return result
    End Function

    Private Sub SaveVisibleColumnPreference(selected As IEnumerable(Of String))
        File.WriteAllLines(ColumnVisibilityPath(), selected.ToArray(), Encoding.UTF8)
    End Sub

    Private Sub ApplyColumnVisibilityPreference()
        If dgv Is Nothing OrElse dgv.Columns.Count = 0 Then Return

        Dim selected = LoadVisibleColumnPreference()

        If selected Is Nothing Then selected = DefaultVisibleColumns()

        Dim selectedSet As New HashSet(Of String)(selected, StringComparer.OrdinalIgnoreCase)

        For Each col As DataGridViewColumn In dgv.Columns
            If col.Name = InternalIdColumn Then
                col.Visible = False
            Else
                col.Visible = selectedSet.Contains(col.Name)
            End If
        Next

        Dim displayIndex As Integer = 0

        For Each colName In selected
            If dgv.Columns.Contains(colName) AndAlso dgv.Columns(colName).Visible Then
                dgv.Columns(colName).DisplayIndex = displayIndex
                displayIndex += 1
            End If
        Next
    End Sub

    Private Sub BtnColumnSelect_Click(sender As Object, e As EventArgs)
        If String.IsNullOrWhiteSpace(currentUser) Then
            MessageBox.Show("Sütun seçimi için önce kullanıcı girişi yapın.",
                            "Kullanıcı Girişi Gerekli", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        If dgv Is Nothing OrElse dgv.Columns.Count = 0 Then Return

        Dim allColumns As New List(Of String)()
        Dim visibleColumns As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        For Each col As DataGridViewColumn In dgv.Columns.Cast(Of DataGridViewColumn)().OrderBy(Function(c) c.DisplayIndex)
            If col.Name = InternalIdColumn Then Continue For

            allColumns.Add(col.Name)

            If col.Visible Then visibleColumns.Add(col.Name)
        Next

        Using frm As New ColumnSelectorForm(allColumns, visibleColumns)
            If frm.ShowDialog(Me) = DialogResult.OK Then
                SaveVisibleColumnPreference(frm.SelectedColumns)
                ApplyColumnVisibilityPreference()
                AutoFitColumns()
                WriteLog("SÜTUN GÖRÜNÜMÜ", Nothing, "Görüntülenecek sütunlar değiştirildi.")
            End If
        End Using
    End Sub

    Private Sub BtnEmailDraft_Click(sender As Object, e As EventArgs)
        If Not HasFullEditPrivilege() Then
            MessageBox.Show("E-posta hazırlama yetkisi sadece ADMİN ve MEKANİZMA kullanıcılarında vardır.",
                            "Yetki Yok", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        PrepareSessionAddedRowsEmail()
    End Sub

    Private Function GetRowsForEmailDraft() As List(Of DataRow)
        Dim result As New List(Of DataRow)()

        If table Is Nothing Then Return result

        For Each dr As DataRow In table.Rows
            If dr.RowState = DataRowState.Deleted Then Continue For
            result.Add(dr)
        Next

        Return result
    End Function

    Private Sub PrepareSessionAddedRowsEmail()
        Dim rows = GetRowsForEmailDraft()

        If rows.Count = 0 Then
            MessageBox.Show("E-posta taslağı için seçilebilecek kayıt bulunamadı.", "E-posta Hazırlanmadı", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim items As New List(Of MailDraftRowItem)()

        For Each dr In rows
            Dim internalId As Integer = 0
            Integer.TryParse(Clean(dr(InternalIdColumn)), internalId)

            items.Add(New MailDraftRowItem With {
                .InternalId = internalId,
                .Sira = GetField(dr, "SIRA"),
                .SayacAdi = GetField(dr, "SAYAÇ ADI"),
                .SiparisYeri = GetField(dr, "SİPARİŞ YERİ"),
                .IsEmriNo = GetField(dr, "İŞ EMRİ NO")
            })
        Next

        Using frm As New EmailDraftSelectionForm(items)
            If frm.ShowDialog(Me) <> DialogResult.OK Then Return

            Dim selectedIds = frm.SelectedInternalIds

            If selectedIds Is Nothing OrElse selectedIds.Count = 0 Then
                MessageBox.Show("En az bir satır seçmelisiniz.", "Satır Seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim selectedRows = rows.Where(Function(dr)
                                              Dim internalId As Integer = 0
                                              Integer.TryParse(Clean(dr(InternalIdColumn)), internalId)
                                              Return selectedIds.Contains(internalId)
                                          End Function).ToList()

            If selectedRows.Count = 0 Then
                MessageBox.Show("Seçilen satırlar bulunamadı.", "E-posta Hazırlanmadı", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim subject = "İNO Takip - Seçili Kayıtlar - " & DateTime.Now.ToString("dd.MM.yyyy")
            Dim htmlBody = BuildEmailHtmlBody(selectedRows)

            Dim openedEditableDraft = TryOpenOutlookEditableDraft("", "", subject, htmlBody)

            If Not openedEditableDraft Then
                MessageBox.Show("Outlook düzenlenebilir e-posta penceresi açılamadı." & Environment.NewLine &
                                "Lütfen Outlook kurulu olduğundan ve bu bilgisayarda açılabildiğinden emin olun.",
                                "Outlook Açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            WriteLog("E-POSTA TASLAĞI", Nothing, selectedRows.Count & " adet seçili kayıt için Outlook içinde düzenlenebilir e-posta taslağı açıldı.")
        End Using
    End Sub

    Private Function BuildEditableEmailText(selectedRows As List(Of DataRow)) As String
        Dim body As New StringBuilder()

        body.AppendLine("Merhaba,")
        body.AppendLine()
        body.AppendLine("Aşağıdaki İNO kayıtları için e-posta taslağı hazırlanmıştır:")
        body.AppendLine()
        body.AppendLine("SAYAÇ ADI | SİPARİŞ YERİ | İŞ EMRİ NO")
        body.AppendLine("----------------------------------------")

        For Each dr In selectedRows
            body.AppendLine($"{GetField(dr, "SAYAÇ ADI")} | {GetField(dr, "SİPARİŞ YERİ")} | {GetField(dr, "İŞ EMRİ NO")}")
        Next

        body.AppendLine()
        body.AppendLine("Bilginize.")
        body.AppendLine()
        body.AppendLine("Hazırlayan: " & CurrentAppUserForLog())

        Return body.ToString()
    End Function

    Private Function ConvertEditableTextToHtml(bodyText As String) As String
        Dim html As New StringBuilder()

        html.AppendLine("<!DOCTYPE html>")
        html.AppendLine("<html>")
        html.AppendLine("<head>")
        html.AppendLine("<meta charset=""utf-8"">")
        html.AppendLine("</head>")
        html.AppendLine("<body style=""font-family:Segoe UI, Arial, sans-serif; font-size:14px; color:#1f2937; white-space:normal;"">")
        html.AppendLine("<div style=""line-height:1.5;"">")
        html.AppendLine(HtmlEncode(bodyText).Replace(vbCrLf, "<br>").Replace(vbLf, "<br>").Replace(vbCr, "<br>"))
        html.AppendLine("</div>")
        html.AppendLine("</body>")
        html.AppendLine("</html>")

        Return html.ToString()
    End Function

    Private Function BuildEmailHtmlBody(selectedRows As List(Of DataRow)) As String
        Dim html As New StringBuilder()

        html.AppendLine("<!DOCTYPE html>")
        html.AppendLine("<html>")
        html.AppendLine("<head>")
        html.AppendLine("<meta charset=""utf-8"">")
        html.AppendLine("</head>")
        html.AppendLine("<body style=""font-family:Segoe UI, Arial, sans-serif; font-size:14px; color:#1f2937;"">")
        html.AppendLine("<p>Merhaba,</p>")
        html.AppendLine("<p>Aşağıdaki İNO kayıtları için e-posta taslağı hazırlanmıştır:</p>")
        html.AppendLine("<table style=""border-collapse:collapse; width:100%; max-width:900px;"">")
        html.AppendLine("<thead>")
        html.AppendLine("<tr style=""background-color:#edf2f7;"">")
        html.AppendLine("<th style=""border:1px solid #cbd5e1; padding:8px; text-align:left;"">SAYAÇ ADI</th>")
        html.AppendLine("<th style=""border:1px solid #cbd5e1; padding:8px; text-align:left;"">SİPARİŞ YERİ</th>")
        html.AppendLine("<th style=""border:1px solid #cbd5e1; padding:8px; text-align:left;"">İŞ EMRİ NO</th>")
        html.AppendLine("</tr>")
        html.AppendLine("</thead>")
        html.AppendLine("<tbody>")

        Dim rowIndex As Integer = 0

        For Each dr In selectedRows
            Dim back = If(rowIndex Mod 2 = 0, "#ffffff", "#f8fafc")
            Dim textColor = If(IsDemonteDataRow(dr), "#b42318", "#1f2937")

            html.AppendLine("<tr style=""background-color:" & back & "; color:" & textColor & ";"">")
            html.AppendLine("<td style=""border:1px solid #cbd5e1; padding:8px;"">" & HtmlEncode(GetField(dr, "SAYAÇ ADI")) & "</td>")
            html.AppendLine("<td style=""border:1px solid #cbd5e1; padding:8px;"">" & HtmlEncode(GetField(dr, "SİPARİŞ YERİ")) & "</td>")
            html.AppendLine("<td style=""border:1px solid #cbd5e1; padding:8px;"">" & HtmlEncode(GetField(dr, "İŞ EMRİ NO")) & "</td>")
            html.AppendLine("</tr>")

            rowIndex += 1
        Next

        html.AppendLine("</tbody>")
        html.AppendLine("</table>")
        html.AppendLine("<p>Bilginize.</p>")
        html.AppendLine("<p><b>Hazırlayan:</b> " & HtmlEncode(CurrentAppUserForLog()) & "</p>")
        html.AppendLine("</body>")
        html.AppendLine("</html>")

        Return html.ToString()
    End Function

    Private Function HtmlEncode(value As String) As String
        Dim s = If(value, "")

        Return s.Replace("&", "&amp;").
                 Replace("<", "&lt;").
                 Replace(">", "&gt;").
                 Replace("""", "&quot;").
                 Replace("'", "&#39;")
    End Function

    Private Function TryOpenOutlookEditableDraft(toText As String, ccText As String, subject As String, htmlBody As String) As Boolean
        Try
            Dim outlookType = Type.GetTypeFromProgID("Outlook.Application")

            If outlookType Is Nothing Then Return False

            Dim outlookApp = Activator.CreateInstance(outlookType)
            Dim mailItem = outlookType.InvokeMember("CreateItem",
                                                     BindingFlags.InvokeMethod,
                                                     Nothing,
                                                     outlookApp,
                                                     New Object() {0})

            Dim mailType = mailItem.GetType()

            mailType.InvokeMember("To", BindingFlags.SetProperty, Nothing, mailItem, New Object() {If(toText, "")})
            mailType.InvokeMember("CC", BindingFlags.SetProperty, Nothing, mailItem, New Object() {If(ccText, "")})
            mailType.InvokeMember("Subject", BindingFlags.SetProperty, Nothing, mailItem, New Object() {subject})
            mailType.InvokeMember("HTMLBody", BindingFlags.SetProperty, Nothing, mailItem, New Object() {htmlBody})

            ' Display ile gönderilmez; Outlook içinde düzenlenebilir yeni mail penceresi açılır.
            mailType.InvokeMember("Display", BindingFlags.InvokeMethod, Nothing, mailItem, New Object() {False})

            Try
                Dim inspector = mailType.InvokeMember("GetInspector", BindingFlags.GetProperty, Nothing, mailItem, Nothing)
                If inspector IsNot Nothing Then
                    inspector.GetType().InvokeMember("Activate", BindingFlags.InvokeMethod, Nothing, inspector, Nothing)
                End If
            Catch
            End Try

            Return True
        Catch
            Return False
        End Try
    End Function

    Private Function CreateEmailDraftFile(toText As String, ccText As String, subject As String, htmlBody As String) As String
        Dim draftFolder = System.IO.Path.Combine(dataDirectory, "MailDrafts")
        Directory.CreateDirectory(draftFolder)

        Dim fileName = "INO_MailDraft_" & DateTime.Now.ToString("yyyyMMdd_HHmmss") & ".eml"
        Dim draftPath = System.IO.Path.Combine(draftFolder, fileName)

        Dim eml As New StringBuilder()
        eml.AppendLine("To: " & If(toText, ""))
        If Not String.IsNullOrWhiteSpace(ccText) Then eml.AppendLine("Cc: " & ccText)
        eml.AppendLine("Subject: " & EncodeMailHeader(subject))
        eml.AppendLine("MIME-Version: 1.0")
        eml.AppendLine("Content-Type: text/html; charset=utf-8")
        eml.AppendLine("Content-Transfer-Encoding: 8bit")
        eml.AppendLine()
        eml.AppendLine(htmlBody)

        File.WriteAllText(draftPath, eml.ToString(), New UTF8Encoding(False))

        Return draftPath
    End Function

    Private Function EncodeMailHeader(text As String) As String
        If String.IsNullOrEmpty(text) Then Return ""

        Dim bytes = Encoding.UTF8.GetBytes(text)
        Return "=?UTF-8?B?" & Convert.ToBase64String(bytes) & "?="
    End Function

    Private Function MakeButton(text As String, backColor As Color) As Button
        Dim b As New Button()
        b.Text = text
        b.AutoSize = True
        b.AutoSizeMode = AutoSizeMode.GrowAndShrink
        b.MinimumSize = New Size(0, 40)
        b.Padding = New Padding(12, 0, 12, 0)
        b.Margin = New Padding(4)
        b.BackColor = backColor
        b.ForeColor = Color.White
        b.FlatStyle = FlatStyle.Flat
        b.FlatAppearance.BorderSize = 0
        b.FlatAppearance.MouseOverBackColor = BlendWithWhite(backColor, 0.14F)
        b.FlatAppearance.MouseDownBackColor = BlendWithWhite(backColor, 0.24F)
        b.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        b.Cursor = Cursors.Hand
        b.UseVisualStyleBackColor = False
        Return b
    End Function

    Private Function AddKpiCard(parent As TableLayoutPanel, index As Integer, title As String, color As Color) As Label
        Dim card As New KpiCard(title, color)
        card.Dock = DockStyle.Fill
        card.Margin = New Padding(10, 8, 10, 8)

        parent.Controls.Add(card, index, 0)
        Return card.ValueLabel
    End Function

    Private Sub AddFilterButton(parent As FlowLayoutPanel, text As String, filterValue As String, back As Color, fore As Color)
        Dim b = MakeButton(text, back)
        b.AccessibleName = text
        b.MinimumSize = New Size(92, 40)
        b.Tag = filterValue
        filterButtons(filterValue) = b
        filterAccentColors(filterValue) = back
        AddHandler b.Click, Sub(sender, e)
                                activeFilter = CStr(DirectCast(sender, Button).Tag)
                                ApplyFilter()
                            End Sub
        parent.Controls.Add(b)
    End Sub

    Private Sub UpdateFilterButtonStyles()
        For Each pair In filterButtons
            Dim filterValue = pair.Key
            Dim button = pair.Value
            Dim accent = If(filterAccentColors.ContainsKey(filterValue),
                            filterAccentColors(filterValue),
                            Color.FromArgb(31, 78, 121))
            Dim selected = String.Equals(activeFilter, filterValue, StringComparison.OrdinalIgnoreCase)
            Dim originalText = If(String.IsNullOrWhiteSpace(button.AccessibleName), button.Text, button.AccessibleName)

            button.Text = If(selected, "✓ " & originalText, originalText)
            button.BackColor = If(selected, accent, BlendWithWhite(accent, 0.86F))
            button.ForeColor = If(selected, Color.White, Color.FromArgb(35, 48, 66))
            button.FlatAppearance.BorderSize = If(selected, 2, 1)
            button.FlatAppearance.BorderColor = accent
            button.FlatAppearance.MouseOverBackColor = BlendWithWhite(accent, If(selected, 0.12F, 0.75F))
            button.FlatAppearance.MouseDownBackColor = BlendWithWhite(accent, If(selected, 0.22F, 0.64F))
        Next
    End Sub

    Private Function BlendWithWhite(color As Color, whiteRatio As Single) As Color
        Dim ratio = Math.Max(0.0F, Math.Min(1.0F, whiteRatio))
        Dim red = CInt(color.R + ((255 - color.R) * ratio))
        Dim green = CInt(color.G + ((255 - color.G) * ratio))
        Dim blue = CInt(color.B + ((255 - color.B) * ratio))
        Return Color.FromArgb(red, green, blue)
    End Function

    Private Sub CreateEmptyTable()
        columns = New List(Of String) From {
            "SIRA", "SAYAÇ ADI", "SİPARİŞ YERİ", "İŞ EMRİ NO",
            "INO-1 VERİLEN BÖLÜM", "INO-1 ONAY TARİHİ", "INO-1 ONAY VEREN", "INO-1 DURUM",
            "INO-2 ONAY TARİHİ", "INO-2 ONAY VEREN", "INO-2 DURUM",
            "Q4", "Q3", "ARA DEBİ", "Q2", "Q1", "TAM (+)", "TAM (-)", "AÇIKLAMA", InoTalepTarihiColumn
        }
        EnsureInoTalepTarihiColumn()
        RebuildColumnMap()
        BuildTableSchema()
        BindTable()
        RecomputeAllStatuses()
        UpdateDashboard()
    End Sub

    Private Sub EnsureInoTalepTarihiColumn()
        If columns Is Nothing Then columns = New List(Of String)()

        ' INO Talep Tarihi sütunu kullanıcı isteğine göre en sağda tutulur.
        ' Mevcut CSV içinde başka bir yerde varsa kaldırılıp en sona taşınır.
        Dim existingIndex = columns.FindIndex(Function(c) String.Equals(Normalize(c), Normalize(InoTalepTarihiColumn), StringComparison.OrdinalIgnoreCase))

        If existingIndex >= 0 Then
            Dim existingName = columns(existingIndex)
            columns.RemoveAt(existingIndex)

            ' Gerçek başlık küçük yazım farkıyla geldiyse standart başlığa çevrilir.
            If table IsNot Nothing AndAlso table.Columns.Contains(existingName) AndAlso Not String.Equals(existingName, InoTalepTarihiColumn, StringComparison.OrdinalIgnoreCase) Then
                table.Columns(existingName).ColumnName = InoTalepTarihiColumn
            End If
        End If

        columns.Add(InoTalepTarihiColumn)
    End Sub

    Private Sub BuildTableSchema()
        table = New DataTable("INO_TAKIP_CSV")
        table.Columns.Add(InternalIdColumn, GetType(Integer))

        For Each c In statusColumns
            table.Columns.Add(c, GetType(String))
        Next

        For Each c In columns
            If Not table.Columns.Contains(c) Then
                table.Columns.Add(c, GetType(String))
            End If
        Next
    End Sub

    Private Sub BindTable()
        view = New DataView(table)
        view.Sort = Bracket(InternalIdColumn) & " DESC"
        dgv.DataSource = view
        ConfigureGrid()
        RefreshColumnFilterChoices()
    End Sub

    Private Sub ConfigureGrid()
        If dgv.Columns.Contains(InternalIdColumn) Then dgv.Columns(InternalIdColumn).Visible = False

        For Each c In statusColumns
            If dgv.Columns.Contains(c) Then
                dgv.Columns(c).ReadOnly = True
                dgv.Columns(c).DisplayIndex = Array.IndexOf(statusColumns, c)
                dgv.Columns(c).Frozen = False
                dgv.Columns(c).DefaultCellStyle.Font = New Font("Segoe UI", 8.2F, FontStyle.Bold)
            End If
        Next

        ' SIRA / SIRA NO alanı kayıt kimliği gibi kullanılır; ana tabloda değiştirilmesi engellenir.
        If columnMap IsNot Nothing AndAlso columnMap.ContainsKey("SIRA") Then
            Dim siraCol = columnMap("SIRA")
            If Not String.IsNullOrWhiteSpace(siraCol) AndAlso dgv.Columns.Contains(siraCol) Then
                dgv.Columns(siraCol).ReadOnly = True
                dgv.Columns(siraCol).DefaultCellStyle.BackColor = Color.FromArgb(245, 247, 250)
                dgv.Columns(siraCol).DefaultCellStyle.ForeColor = Color.FromArgb(102, 112, 133)
            End If
        End If

        ' INO TALEP TARİHİ yalnızca yeni satır oluşturulurken otomatik atanır.
        ' Sonradan ana tabloda değiştirilemez.
        If dgv.Columns.Contains(InoTalepTarihiColumn) Then
            dgv.Columns(InoTalepTarihiColumn).ReadOnly = True
            dgv.Columns(InoTalepTarihiColumn).DefaultCellStyle.BackColor = Color.FromArgb(245, 247, 250)
            dgv.Columns(InoTalepTarihiColumn).DefaultCellStyle.ForeColor = Color.FromArgb(102, 112, 133)
        End If

        ' Rapor numaraları sistem tarafından benzersiz üretilir; sonradan manuel değiştirilmez.
        SetGridColumnReadOnly("INO-1 RAPOR NO")
        SetGridColumnReadOnly("INO-2 RAPOR NO")

        ApplyColumnVisibilityPreference()
        AutoFitColumns()
    End Sub

    Private Sub SetGridColumnReadOnly(canonical As String)
        If columnMap Is Nothing OrElse Not columnMap.ContainsKey(canonical) Then Return

        Dim realCol = columnMap(canonical)
        If String.IsNullOrWhiteSpace(realCol) Then Return

        If dgv.Columns.Contains(realCol) Then
            dgv.Columns(realCol).ReadOnly = True
            dgv.Columns(realCol).DefaultCellStyle.BackColor = Color.FromArgb(245, 247, 250)
            dgv.Columns(realCol).DefaultCellStyle.ForeColor = Color.FromArgb(102, 112, 133)
        End If
    End Sub

    Private Sub AutoFitColumns()
        If dgv Is Nothing OrElse dgv.Columns.Count = 0 Then Return

        Const sampleRows As Integer = 40

        For Each col As DataGridViewColumn In dgv.Columns
            If Not col.Visible Then Continue For

            Dim minW As Integer = 60
            Dim maxW As Integer = 170

            If statusColumns.Contains(col.Name) Then
                minW = 105
                maxW = 130
            End If

            Dim widthEstimate As Integer = 18 + (If(col.HeaderText, "").Length * 7)

            Dim checked As Integer = 0
            For Each gridRow As DataGridViewRow In dgv.Rows
                If gridRow.IsNewRow Then Continue For

                Dim v As Object = gridRow.Cells(col.Index).Value
                Dim s As String = If(v Is Nothing, "", Convert.ToString(v))
                If s.Length > 40 Then s = s.Substring(0, 40)

                widthEstimate = Math.Max(widthEstimate, 18 + (s.Length * 7))

                checked += 1
                If checked >= sampleRows Then Exit For
            Next

            col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            col.Width = Math.Min(Math.Max(widthEstimate, minW), maxW)
        Next
    End Sub

    Private Async Sub BtnOpen_Click(sender As Object, e As EventArgs)
        Using ofd As New OpenFileDialog()
            ofd.Title = "CSV dosyası seç"
            ofd.Filter = "CSV Dosyası (*.csv)|*.csv|Tüm Dosyalar (*.*)|*.*"

            If ofd.ShowDialog() = DialogResult.OK Then
                Await LoadCsvAsync(ofd.FileName)
            End If
        End Using
    End Sub

    Private Async Function LoadCsvAsync(path As String) As Task
        Try
            SetUiBusy(True, "CSV yükleniyor, lütfen bekleyin...")

            Dim loaded = Await Task.Run(Function() ReadCsvFile(path))

            csvPath = path
            delimiter = loaded.Delimiter
            columns = loaded.Headers
            EnsureInoTalepTarihiColumn()
            RebuildColumnMap()
            BuildTableSchema()

            nextInternalId = 1

            For Each rowValues In loaded.Rows
                Dim dr = table.NewRow()
                dr(InternalIdColumn) = nextInternalId
                nextInternalId += 1

                For i As Integer = 0 To columns.Count - 1
                    dr(columns(i)) = If(i < rowValues.Count, Clean(rowValues(i)), "")
                Next

                If HasMeaningfulData(dr) AndAlso Not IsRepeatedHeaderRow(dr) Then
                    table.Rows.Add(dr)
                End If
            Next

            RecomputeAllStatuses()
            BindTable()
            ApplyFilter()

            hasUnsavedChanges = False
            lblStatus.Text = ""

        Catch ex As Exception
            MessageBox.Show("CSV dosyası okunamadı:" & Environment.NewLine & ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            SetUiBusy(False, "")
        End Try
    End Function

    Private Class CsvLoadResult
        Public Property Headers As List(Of String)
        Public Property Rows As List(Of List(Of String))
        Public Property Delimiter As String
    End Class

    Private Function ReadCsvFile(path As String) As CsvLoadResult
        Dim detectedDelimiter = DetectDelimiter(path)
        Dim result As New CsvLoadResult With {
            .Headers = New List(Of String)(),
            .Rows = New List(Of List(Of String))(),
            .Delimiter = detectedDelimiter
        }

        Using parser As New TextFieldParser(path, Encoding.UTF8)
            parser.TextFieldType = FieldType.Delimited
            parser.SetDelimiters(detectedDelimiter)
            parser.HasFieldsEnclosedInQuotes = True
            parser.TrimWhiteSpace = False

            If parser.EndOfData Then Return result

            Dim rawHeaders = parser.ReadFields()
            result.Headers = MakeUniqueHeaders(rawHeaders)

            While Not parser.EndOfData
                Dim fields = parser.ReadFields()
                result.Rows.Add(fields.Select(Function(x) Clean(x)).ToList())
            End While
        End Using

        RemoveUnwantedGeneratedColumns(result)

        Return result
    End Function


    Private Sub RemoveUnwantedGeneratedColumns(result As CsvLoadResult)
        If result Is Nothing OrElse result.Headers Is Nothing OrElse result.Headers.Count = 0 Then Return

        Dim keepIndexes As New List(Of Integer)()

        For i As Integer = 0 To result.Headers.Count - 1
            Dim header = Clean(result.Headers(i))
            Dim isGeneratedHeader = Regex.IsMatch(header, "^SÜTUN\s+\d+$", RegexOptions.IgnoreCase)

            ' Mantık:
            ' - Gerçek başlığı olmayan sütunlar MakeUniqueHeaders içinde "SÜTUN n" olarak oluşuyor.
            ' - Eğer bu sütun tüm satırlarda boşsa kullanıcı açısından anlamsızdır; tabloya alınmaz.
            ' - Gerçek başlıklı sütunlar veya veri içeren generated sütunlar korunur.
            Dim hasAnyData As Boolean = False

            For Each row In result.Rows
                If i < row.Count AndAlso Clean(row(i)).Length > 0 Then
                    hasAnyData = True
                    Exit For
                End If
            Next

            If Not isGeneratedHeader OrElse hasAnyData Then
                keepIndexes.Add(i)
            End If
        Next

        Dim newHeaders As New List(Of String)()
        For Each idx In keepIndexes
            newHeaders.Add(result.Headers(idx))
        Next

        Dim newRows As New List(Of List(Of String))()
        For Each row In result.Rows
            Dim newRow As New List(Of String)()
            For Each idx In keepIndexes
                If idx < row.Count Then
                    newRow.Add(Clean(row(idx)))
                Else
                    newRow.Add("")
                End If
            Next
            newRows.Add(newRow)
        Next

        result.Headers = newHeaders
        result.Rows = newRows
    End Sub

    Private Function DetectDelimiter(path As String) As String
        Dim firstLine As String = ""

        Using sr As New StreamReader(path, Encoding.UTF8, True)
            firstLine = sr.ReadLine()
        End Using

        If firstLine Is Nothing Then Return ";"

        Dim semicolonCount = firstLine.Count(Function(ch) ch = ";"c)
        Dim commaCount = firstLine.Count(Function(ch) ch = ","c)

        If semicolonCount >= commaCount Then Return ";"
        Return ","
    End Function

    Private Function MakeUniqueHeaders(rawHeaders As IEnumerable(Of String)) As List(Of String)
        Dim result As New List(Of String)()
        Dim seen As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
        Dim i As Integer = 1

        For Each raw In rawHeaders
            Dim h = Clean(raw)
            If h.Length = 0 Then h = "SÜTUN " & i.ToString()

            Dim key = Normalize(h)

            If Not seen.ContainsKey(key) Then
                seen(key) = 1
                result.Add(h)
            Else
                seen(key) += 1
                result.Add(h & " (" & seen(key).ToString() & ")")
            End If

            i += 1
        Next

        Return result
    End Function

    Private Sub RebuildColumnMap()
        columnMap.Clear()

        For Each canonical In aliases.Keys
            columnMap(canonical) = FindColumnForCanonical(canonical)
        Next
    End Sub

    Private Function FindColumnForCanonical(canonical As String) As String
        Dim keys = aliases(canonical).Select(Function(x) Normalize(x)).ToList()

        For Each col In columns
            Dim h = Normalize(col)
            If h.Length = 0 Then Continue For

            For Each k In keys
                If h = k OrElse h.Contains(k) OrElse k.Contains(h) Then Return col
            Next
        Next

        Return ""
    End Function

    Private Function GetField(dr As DataRow, canonical As String) As String
        If Not columnMap.ContainsKey(canonical) Then Return ""

        Dim col = columnMap(canonical)
        If String.IsNullOrWhiteSpace(col) Then Return ""
        If Not dr.Table.Columns.Contains(col) Then Return ""

        Return Clean(dr(col))
    End Function

    Private Sub SetField(dr As DataRow, canonical As String, value As String)
        If Not columnMap.ContainsKey(canonical) OrElse String.IsNullOrWhiteSpace(columnMap(canonical)) Then
            Dim newCol = canonical
            If Not columns.Contains(newCol) Then
                columns.Add(newCol)
                table.Columns.Add(newCol, GetType(String))
            End If
            RebuildColumnMap()
        End If

        Dim col = columnMap(canonical)
        If Not String.IsNullOrWhiteSpace(col) AndAlso dr.Table.Columns.Contains(col) Then dr(col) = Clean(value)
    End Sub

    Private Function HasMeaningfulData(dr As DataRow) As Boolean
        Dim siraCol As String = If(columnMap.ContainsKey("SIRA"), columnMap("SIRA"), "")

        For Each col In columns
            If String.Equals(col, siraCol, StringComparison.OrdinalIgnoreCase) Then Continue For
            If Clean(dr(col)).Length > 0 Then Return True
        Next

        Return False
    End Function

    Private Function IsRepeatedHeaderRow(dr As DataRow) As Boolean
        Dim hits As Integer = 0

        For Each col In columns
            Dim v = Normalize(dr(col))
            Dim h = Normalize(col)
            If v.Length > 0 AndAlso v = h Then hits += 1
        Next

        Return hits >= 2
    End Function

    Private Function GetYearPrefix() As String
        Return DateTime.Today.ToString("yy")
    End Function

    Private Function GenerateNextReportNo(stage As Integer) As String
        Dim prefix = GetYearPrefix() & "INO" & stage.ToString() & "-"
        Dim maxNo As Integer = 0

        If table IsNot Nothing Then
            Dim canonical = If(stage = 1, "INO-1 RAPOR NO", "INO-2 RAPOR NO")
            Dim colName As String = ""

            If columnMap IsNot Nothing AndAlso columnMap.ContainsKey(canonical) Then
                colName = columnMap(canonical)
            End If

            If Not String.IsNullOrWhiteSpace(colName) AndAlso table.Columns.Contains(colName) Then
                For Each dr As DataRow In table.Rows
                    If dr.RowState = DataRowState.Deleted Then Continue For

                    Dim value = Clean(dr(colName))
                    Dim m = Regex.Match(value, "^" & Regex.Escape(prefix) & "(\d+)$", RegexOptions.IgnoreCase)

                    If m.Success Then
                        Dim n As Integer
                        If Integer.TryParse(m.Groups(1).Value, n) AndAlso n > maxNo Then
                            maxNo = n
                        End If
                    End If
                Next
            End If
        End If

        Return prefix & (maxNo + 1).ToString("0000")
    End Function

    Private Async Sub BtnNew_Click(sender As Object, e As EventArgs)
        If Not RequireFullEditPrivilege() Then Return

        If table Is Nothing Then CreateEmptyTable()

        Dim dr = table.NewRow()
        dr(InternalIdColumn) = nextInternalId
        nextInternalId += 1

        For Each col In columns
            dr(col) = ""
        Next

        SetField(dr, "SIRA", NextSira().ToString())

        ' INO TALEP TARİHİ sadece yeni satır oluşturulduğu anda atanır; sonradan kullanıcı tarafından değiştirilemez.
        If table.Columns.Contains(InoTalepTarihiColumn) Then dr(InoTalepTarihiColumn) = DateTime.Today.ToString("dd.MM.yyyy")

        ' Rapor numaraları mevcut listedeki en büyük numaraya göre benzersiz otomatik atanır.
        SetField(dr, "INO-1 RAPOR NO", GenerateNextReportNo(1))
        SetField(dr, "INO-2 RAPOR NO", GenerateNextReportNo(2))

        table.Rows.Add(dr)
        RecomputeStatusForRow(dr)
        ApplyFilter()

        Dim ok = Await EditDataRowWithDialogAsync(dr)

        If Not ok Then
            WriteLog("YENİ SATIR İPTAL", dr, "Yeni satır düzenleme penceresinde iptal edildi.")
            dr.Delete()
            ApplyFilter()
            UpdateDashboard()
            Return
        End If

        sessionAddedRows.Add(New SessionAddedRowInfo With {
            .UserName = currentUser,
            .InternalId = Convert.ToInt32(dr(InternalIdColumn))
        })

        QueueDataChangeLog("YENİ SATIR EKLENDİ", dr, "Yeni satır oluşturuldu.")
        Await SaveCurrentCsvSilentlyAsync()
    End Sub

    Private Function FirstEditableColumnIndex() As Integer
        For Each col As DataGridViewColumn In dgv.Columns
            If col.Visible AndAlso Not col.ReadOnly Then Return col.Index
        Next
        Return 0
    End Function

    Private Function NextSira() As Integer
        Dim maxNo As Integer = 0

        For Each dr As DataRow In table.Rows
            Dim s = Regex.Replace(GetField(dr, "SIRA"), "\D", "")
            Dim n As Integer
            If Integer.TryParse(s, n) Then
                If n > maxNo Then maxNo = n
            End If
        Next

        If maxNo > 0 Then Return maxNo + 1
        Return table.Rows.Count + 1
    End Function

    Private Async Sub Dgv_CellDoubleClick(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex >= 0 Then
            Await EditSelectedRowWithDialogAsync(True)
        End If
    End Sub

    Private Async Function EditSelectedRowWithDialogAsync(Optional allowReadOnlyWithoutLogin As Boolean = False) As Task
        Dim readOnlyView = databaseReadOnlyMode OrElse
                           (String.IsNullOrWhiteSpace(currentUser) AndAlso allowReadOnlyWithoutLogin)

        If Not readOnlyView AndAlso Not RequireEditPrivilege() Then Return

        If dgv.CurrentRow Is Nothing Then
            MessageBox.Show("Lütfen düzenlenecek satırı seçin.", "Satır Seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim drv = TryCast(dgv.CurrentRow.DataBoundItem, DataRowView)
        If drv Is Nothing Then Return

        Dim ok = Await EditDataRowWithDialogAsync(drv.Row, readOnlyView)

        If ok AndAlso Not readOnlyView Then
            Await SaveCurrentCsvSilentlyAsync()
        End If
    End Function

    Private Async Function EditDataRowWithDialogAsync(dataRow As DataRow, Optional readOnlyView As Boolean = False) As Task(Of Boolean)
        If dataRow Is Nothing OrElse dataRow.RowState = DataRowState.Deleted Then Return False

        Dim rowValues As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

        For Each col In columns
            rowValues(col) = Clean(dataRow(col))
        Next

        Dim titleText = If(readOnlyView, "Satır Görüntüle", "Satır Düzenle")
        Dim siraText = GetField(dataRow, "SIRA")
        Dim sayacText = GetField(dataRow, "SAYAÇ ADI")

        If siraText.Length > 0 OrElse sayacText.Length > 0 Then
            titleText &= " - " & siraText & " " & sayacText
        End If

        Using frm As New RowEditForm(columns, rowValues, titleText, If(readOnlyView, "", currentUser), If(readOnlyView, "", GetCurrentUserRole()), If(readOnlyView, False, HasFullEditPrivilege()), If(readOnlyView, False, HasApprovalEditPrivilege()))
            If frm.ShowDialog(Me) = DialogResult.OK Then
                If readOnlyView Then Return False

                Dim beforeValues = New Dictionary(Of String, String)(rowValues, StringComparer.OrdinalIgnoreCase)
                Dim afterValues = New Dictionary(Of String, String)(frm.Values, StringComparer.OrdinalIgnoreCase)
                Dim detailText = BuildChangeDetail(beforeValues, afterValues)

                isUpdating = True

                Try
                    For Each col In columns
                        dataRow(col) = frm.Values(col)
                    Next
                Finally
                    isUpdating = False
                End Try

                RecomputeStatusForRow(dataRow)
                ApplyFilter()
                UpdateDashboard()
                QueueDataChangeLog("SATIR DÜZENLENDİ", dataRow, detailText)
                Return True
            End If
        End Using

        Await Task.CompletedTask
        Return False
    End Function

    Private Async Sub BtnDelete_Click(sender As Object, e As EventArgs)
        Await DeleteSelectedRowAsync()
    End Sub

    Private Function IsMechanismUser() As Boolean
        Dim roleName = GetCurrentUserRole()

        Return String.Equals(roleName, UserStore.RoleMechanism, StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(currentUser, "MEKANİZMA", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Function IsInoSectionChanged(row As DataRow) As Boolean
        If row Is Nothing OrElse row.RowState = DataRowState.Deleted Then Return False

        For Each colName In columns
            Dim n = Normalize(colName)

            ' Otomatik sistem numaraları silme kontrolünde dikkate alınmaz.
            If n = "INO1RAPORNO" OrElse n = "INO2RAPORNO" Then Continue For

            Dim isInoField As Boolean =
                n.StartsWith("INO1") OrElse
                n.StartsWith("INO2") OrElse
                n = "Q4" OrElse
                n = "Q3" OrElse
                n = "Q2" OrElse
                n = "Q1" OrElse
                n.Contains("ARADEBI") OrElse
                n.StartsWith("TAM")

            If Not isInoField Then Continue For
            If Not row.Table.Columns.Contains(colName) Then Continue For

            If Clean(row(colName)).Length > 0 Then Return True
        Next

        Return False
    End Function

    Private Async Function DeleteSelectedRowAsync() As Task
        If Not RequireFullEditPrivilege() Then Return

        If dgv.CurrentRow Is Nothing Then Return

        Dim drv = TryCast(dgv.CurrentRow.DataBoundItem, DataRowView)
        If drv Is Nothing Then Return

        If IsMechanismUser() AndAlso IsInoSectionChanged(drv.Row) Then
            MessageBox.Show("Bu satırda İNO-1 veya İNO-2 bilgileri düzenlenmiş olduğu için MEKANİZMA kullanıcısı satırı silemez.",
                            "Silme Yetkisi Yok", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim sira = GetField(drv.Row, "SIRA")
        Dim sayac = GetField(drv.Row, "SAYAÇ ADI")

        If MessageBox.Show($"{sira} - {sayac} satırı silinsin mi?", "Satır Sil", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) = DialogResult.Yes Then
            QueueDataChangeLog("SATIR SİLİNDİ", drv.Row, "Satır silindi.")
            drv.Row.Delete()
            RecomputeAllStatuses()
            ApplyFilter()
            Await SaveCurrentCsvSilentlyAsync()
        End If
    End Function

    Private Sub BtnEdit_Click(sender As Object, e As EventArgs)
        If dgv.CurrentCell IsNot Nothing AndAlso Not dgv.CurrentCell.ReadOnly Then dgv.BeginEdit(True)
    End Sub

    Private Sub Dgv_CurrentCellDirtyStateChanged(sender As Object, e As EventArgs)
        If dgv.IsCurrentCellDirty Then dgv.CommitEdit(DataGridViewDataErrorContexts.Commit)
    End Sub

    Private Sub Dgv_CellEndEdit(sender As Object, e As DataGridViewCellEventArgs)
        If isUpdating Then Return
        If e.RowIndex < 0 Then Return

        Dim drv = TryCast(dgv.Rows(e.RowIndex).DataBoundItem, DataRowView)
        If drv Is Nothing Then Return

        ' Eski sürüm her hücre düzenlemesinden sonra tüm tabloyu yeniden hesaplıyor ve filtreliyordu.
        ' Büyük CSV dosyalarında kasmanın ana nedeni buydu.
        RecomputeStatusForRow(drv.Row)
        UpdateDashboard()
        hasUnsavedChanges = True

        ' Filtreyi her hücre çıkışında yeniden çalıştırma. Gerekirse Filtre Temizle / arama / filtre butonlarıyla yenilenir.
        ' Otomatik kaydet açıksa kayıt yine yapılır; büyük dosyalarda kapalı kullanılması önerilir.
        AutoSaveIfRequested()
    End Sub

    Private Sub Dgv_CellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If e.RowIndex < 0 OrElse e.ColumnIndex < 0 OrElse e.Value Is Nothing Then Return

        Dim colName = dgv.Columns(e.ColumnIndex).Name
        Dim valueText = Clean(e.Value).ToUpperInvariant()
        Dim demonteRow = IsDemonteGridRow(e.RowIndex)

        ' Hesaplanan durum kolonları özel arka planla renklendirilir.
        If statusColumns.Contains(colName) Then
            If valueText.Contains("RED") Then
                e.CellStyle.BackColor = Color.FromArgb(254, 226, 226)
                e.CellStyle.ForeColor = Color.FromArgb(153, 27, 27)
                e.CellStyle.Font = New Font("Segoe UI", 8.2F, FontStyle.Bold)
            ElseIf valueText.Contains("BEKL") Then
                e.CellStyle.BackColor = Color.FromArgb(254, 243, 199)
                e.CellStyle.ForeColor = Color.FromArgb(146, 64, 14)
                e.CellStyle.Font = New Font("Segoe UI", 8.2F, FontStyle.Bold)
            ElseIf valueText.Contains("ONAYLI") Then
                e.CellStyle.BackColor = Color.FromArgb(220, 252, 231)
                e.CellStyle.ForeColor = Color.FromArgb(22, 101, 52)
                e.CellStyle.Font = New Font("Segoe UI", 8.2F, FontStyle.Bold)
            ElseIf valueText.Contains("KONTROL") Then
                e.CellStyle.BackColor = Color.FromArgb(237, 233, 254)
                e.CellStyle.ForeColor = Color.FromArgb(91, 33, 182)
                e.CellStyle.Font = New Font("Segoe UI", 8.2F, FontStyle.Bold)
            End If
        End If

        ' AÇIKLAMA içinde DEMONTE geçiyorsa satırdaki tüm yazılar kırmızı gösterilir.
        If demonteRow Then
            e.CellStyle.ForeColor = Color.FromArgb(180, 35, 24)
        End If
    End Sub

    Private Function IsDemonteGridRow(rowIndex As Integer) As Boolean
        Try
            If dgv Is Nothing OrElse rowIndex < 0 OrElse rowIndex >= dgv.Rows.Count Then Return False

            Dim drv = TryCast(dgv.Rows(rowIndex).DataBoundItem, DataRowView)
            If drv Is Nothing Then Return False

            Return IsDemonteDataRow(drv.Row)
        Catch
            Return False
        End Try
    End Function

    Private Function IsDemonteDataRow(row As DataRow) As Boolean
        If row Is Nothing OrElse row.RowState = DataRowState.Deleted Then Return False

        Dim aciklama = GetField(row, "AÇIKLAMA")

        Return aciklama.IndexOf("DEMONTE", StringComparison.OrdinalIgnoreCase) >= 0
    End Function

    Private Sub Dgv_DataBindingComplete(sender As Object, e As DataGridViewBindingCompleteEventArgs)
        If lastGridColumnCount <> dgv.Columns.Count Then
            ConfigureGrid()
            lastGridColumnCount = dgv.Columns.Count
        End If
    End Sub

    Private Sub SearchTextChangedDebounced(sender As Object, e As EventArgs)
        If searchTimer Is Nothing Then
            ApplyFilter()
            Return
        End If

        searchTimer.Stop()
        searchTimer.Start()
    End Sub

    Private Sub RefreshColumnFilterChoices()
        If cboColumnFilter Is Nothing OrElse table Is Nothing Then Return

        Dim selectedColumn = Convert.ToString(cboColumnFilter.SelectedItem)

        For Each key In columnFilters.Keys.ToList()
            If Not table.Columns.Contains(key) Then columnFilters.Remove(key)
        Next

        isLoadingColumnFilter = True
        Try
            cboColumnFilter.Items.Clear()

            For Each column As DataColumn In table.Columns
                If Not String.Equals(column.ColumnName, InternalIdColumn, StringComparison.OrdinalIgnoreCase) Then
                    cboColumnFilter.Items.Add(column.ColumnName)
                End If
            Next

            If selectedColumn <> "" AndAlso cboColumnFilter.Items.Contains(selectedColumn) Then
                cboColumnFilter.SelectedItem = selectedColumn
            ElseIf cboColumnFilter.Items.Count > 0 Then
                cboColumnFilter.SelectedIndex = 0
            End If
        Finally
            isLoadingColumnFilter = False
        End Try

        LoadSelectedColumnFilterText()
        UpdateColumnFilterIndicators()
    End Sub

    Private Sub ColumnFilterColumnChanged(sender As Object, e As EventArgs)
        If isLoadingColumnFilter Then Return
        LoadSelectedColumnFilterText()
    End Sub

    Private Sub LoadSelectedColumnFilterText()
        If cboColumnFilter Is Nothing OrElse txtColumnFilter Is Nothing Then Return

        Dim columnName = Convert.ToString(cboColumnFilter.SelectedItem)
        Dim filterText As String = ""
        If columnName <> "" AndAlso columnFilters.ContainsKey(columnName) Then filterText = columnFilters(columnName)

        isLoadingColumnFilter = True
        Try
            txtColumnFilter.Text = filterText
            txtColumnFilter.Enabled = columnName <> ""
        Finally
            isLoadingColumnFilter = False
        End Try
    End Sub

    Private Sub ColumnFilterTextChanged(sender As Object, e As EventArgs)
        If isLoadingColumnFilter OrElse cboColumnFilter Is Nothing Then Return

        Dim columnName = Convert.ToString(cboColumnFilter.SelectedItem)
        If columnName = "" Then Return

        Dim filterText = If(txtColumnFilter.Text, "").Trim()
        If filterText = "" Then
            columnFilters.Remove(columnName)
        Else
            columnFilters(columnName) = filterText
        End If

        UpdateColumnFilterIndicators()
        SearchTextChangedDebounced(sender, e)
    End Sub

    Private Sub ClearColumnFilters_Click(sender As Object, e As EventArgs)
        columnFilters.Clear()
        LoadSelectedColumnFilterText()
        ApplyFilter()
    End Sub

    Private Sub UpdateColumnFilterIndicators()
        If lblColumnFilterCount IsNot Nothing Then
            lblColumnFilterCount.Text = "Aktif: " & columnFilters.Count.ToString()
            lblColumnFilterCount.ForeColor = If(columnFilters.Count > 0,
                                                 Color.FromArgb(180, 90, 20),
                                                 Color.FromArgb(71, 84, 103))
        End If

        If dgv Is Nothing Then Return

        For Each column As DataGridViewColumn In dgv.Columns
            Dim isFiltered = columnFilters.ContainsKey(column.Name)
            column.HeaderCell.Style.BackColor = If(isFiltered,
                                                   Color.FromArgb(255, 237, 190),
                                                   Color.FromArgb(233, 238, 245))
            column.HeaderCell.Style.ForeColor = If(isFiltered,
                                                   Color.FromArgb(125, 70, 10),
                                                   Color.FromArgb(23, 32, 51))
        Next

        dgv.Invalidate()
    End Sub

    Private Sub SearchTimer_Tick(sender As Object, e As EventArgs)
        searchTimer.Stop()
        ApplyFilter()
    End Sub

    Private Sub SearchOrFilterChanged(sender As Object, e As EventArgs)
        ApplyFilter()
    End Sub

    Private Sub ApplyFilter()
        If view Is Nothing Then Return

        Dim parts As New List(Of String)()

        If activeFilter = "TAM ONAYLI" Then
            parts.Add($"{Bracket("GENEL DURUM")} = 'TAM ONAYLI'")
        ElseIf activeFilter = "BEKLİYOR" Then
            parts.Add($"{Bracket("GENEL DURUM")} = 'BEKLİYOR'")
        ElseIf activeFilter = "RED / UYGUN DEĞİL" Then
            parts.Add($"{Bracket("GENEL DURUM")} = 'RED / UYGUN DEĞİL'")
        ElseIf activeFilter = "KONTROL GEREKİR" Then
            parts.Add($"{Bracket("GENEL DURUM")} = 'KONTROL GEREKİR'")
        ElseIf activeFilter = "INO1_BEKLEYEN" Then
            parts.Add($"{Bracket("İNO-1")} = 'BEKLİYOR'")
        ElseIf activeFilter = "INO2_BEKLEYEN" Then
            parts.Add($"{Bracket("İNO-2")} = 'BEKLİYOR'")
        End If

        Dim q = EscapeRowFilterLike(txtSearch.Text)
        If q.Length > 0 Then
            Dim searchParts As New List(Of String)()

            For Each col As DataColumn In table.Columns
                If col.ColumnName = InternalIdColumn Then Continue For
                searchParts.Add($"CONVERT({Bracket(col.ColumnName)}, 'System.String') LIKE '%{q}%'")
            Next

            parts.Add("(" & String.Join(" OR ", searchParts) & ")")
        End If

        For Each pair In columnFilters.OrderBy(Function(item) item.Key)
            If Not table.Columns.Contains(pair.Key) Then Continue For

            Dim columnQuery = EscapeRowFilterLike(pair.Value)
            If columnQuery.Length = 0 Then Continue For

            parts.Add($"CONVERT({Bracket(pair.Key)}, 'System.String') LIKE '%{columnQuery}%'")
        Next

        view.RowFilter = String.Join(" AND ", parts)
        view.Sort = Bracket(InternalIdColumn) & " DESC"

        UpdateFilterButtonStyles()
        UpdateColumnFilterIndicators()
        UpdateFilterResultLabel()
        UpdateDashboard()
    End Sub

    Private Sub UpdateFilterResultLabel()
        If lblFilterResult Is Nothing OrElse view Is Nothing OrElse table Is Nothing Then Return

        Dim totalCount = table.Rows.Cast(Of DataRow)().Count(Function(row) row.RowState <> DataRowState.Deleted)
        Dim filterName As String = "Tümü"

        If filterButtons.ContainsKey(activeFilter) Then
            filterName = filterButtons(activeFilter).AccessibleName
        End If

        If String.Equals(activeFilter, "ALL", StringComparison.OrdinalIgnoreCase) AndAlso
           String.IsNullOrWhiteSpace(txtSearch.Text) AndAlso
           columnFilters.Count = 0 Then
            lblFilterResult.Text = $"{totalCount:N0} kayıt gösteriliyor"
        Else
            Dim columnFilterSummary = If(columnFilters.Count > 0,
                                         $"  •  {columnFilters.Count:N0} sütun filtresi",
                                         "")
            lblFilterResult.Text = $"Gösterilen: {view.Count:N0} / {totalCount:N0}  •  {filterName}{columnFilterSummary}"
        End If
    End Sub

    Private Sub RecomputeStatusForRow(dr As DataRow)
        If dr Is Nothing OrElse dr.RowState = DataRowState.Deleted Then Return

        isUpdating = True

        Try
            dr("İNO-1") = ResolveStatus(dr, 1)
            dr("İNO-2") = ResolveStatus(dr, 2)
            dr("GENEL DURUM") = GeneralStatus(dr)
        Finally
            isUpdating = False
        End Try
    End Sub

    Private Sub RecomputeAllStatuses()
        If table Is Nothing Then Return

        isUpdating = True

        Try
            For Each dr As DataRow In table.Rows
                If dr.RowState = DataRowState.Deleted Then Continue For
                dr("İNO-1") = ResolveStatus(dr, 1)
                dr("İNO-2") = ResolveStatus(dr, 2)
                dr("GENEL DURUM") = GeneralStatus(dr)
            Next
        Finally
            isUpdating = False
        End Try
    End Sub

    Private Function ResolveStatus(dr As DataRow, stage As Integer) As String
        Dim durum As String = If(stage = 1, GetField(dr, "INO-1 DURUM"), GetField(dr, "INO-2 DURUM"))
        Dim tarih As String = If(stage = 1, GetField(dr, "INO-1 ONAY TARİHİ"), GetField(dr, "INO-2 ONAY TARİHİ"))
        Dim veren As String = If(stage = 1, GetField(dr, "INO-1 ONAY VEREN"), GetField(dr, "INO-2 ONAY VEREN"))

        If IsRejectedText(durum) Then Return "RED / UYGUN DEĞİL"
        If IsApprovedText(durum) Then Return "ONAYLI"

        If Clean(durum).Length = 0 AndAlso (IsDateLike(tarih) OrElse IsValidApprover(veren)) Then Return "ONAYLI"
        If Clean(durum).Length = 0 AndAlso Clean(tarih).Length = 0 AndAlso Clean(veren).Length = 0 Then Return "BEKLİYOR"

        Return "KONTROL GEREKİR"
    End Function

    Private Function GeneralStatus(dr As DataRow) As String
        Dim s1 = ResolveStatus(dr, 1)
        Dim s2 = ResolveStatus(dr, 2)

        If s1 = "ONAYLI" AndAlso s2 = "ONAYLI" Then Return "TAM ONAYLI"
        If s1 = "RED / UYGUN DEĞİL" OrElse s2 = "RED / UYGUN DEĞİL" Then Return "RED / UYGUN DEĞİL"
        If s1 = "KONTROL GEREKİR" OrElse s2 = "KONTROL GEREKİR" Then Return "KONTROL GEREKİR"

        Return "BEKLİYOR"
    End Function

    Private Function IsApprovedText(v As String) As Boolean
        Dim t = Normalize(v)
        Return New String() {"ONAYLI", "ONAYLANDI", "UYGUN", "OK", "EVET", "TAMAM", "GECTI", "PASS"}.Any(Function(x) t.Contains(x))
    End Function

    Private Function IsRejectedText(v As String) As Boolean
        Dim t = Normalize(v)
        Return New String() {"RED", "RET", "UYGUNDEGIL", "NOTOK", "HAYIR", "OLUMSUZ", "FAIL", "BASARISIZ", "ONAYLANMADI", "ONAYSIZ"}.Any(Function(x) t.Contains(x))
    End Function

    Private Function IsDateLike(v As String) As Boolean
        Dim s = Clean(v)
        If s.Length = 0 Then Return False

        Return Regex.IsMatch(s, "^\d{1,2}[./-]\d{1,2}[./-]\d{2,4}$") OrElse
               Regex.IsMatch(s, "^\d{4}[./-]\d{1,2}[./-]\d{1,2}$")
    End Function

    Private Function IsValidApprover(v As String) As Boolean
        Dim s = Clean(v)
        If s.Length < 2 Then Return False
        If Regex.IsMatch(s, "^\d+$") Then Return False
        If Normalize(s).Contains("ONAYVEREN") Then Return False
        If Normalize(s).Contains("ONAYIVEREN") Then Return False
        Return True
    End Function

    Private Sub UpdateDashboard()
        If table Is Nothing Then Return

        Dim total As Integer = 0
        Dim approved As Integer = 0
        Dim pending As Integer = 0
        Dim rejected As Integer = 0
        Dim checkCount As Integer = 0

        For Each dr As DataRow In table.Rows
            If dr.RowState = DataRowState.Deleted Then Continue For

            total += 1
            Dim g = Clean(dr("GENEL DURUM"))

            If g = "TAM ONAYLI" Then approved += 1
            If g = "BEKLİYOR" Then pending += 1
            If g = "RED / UYGUN DEĞİL" Then rejected += 1
            If g = "KONTROL GEREKİR" Then checkCount += 1
        Next

        dashboardTotal = total
        dashboardApproved = approved
        dashboardPending = pending
        dashboardRejected = rejected
        dashboardCheck = checkCount

        If lblTotal IsNot Nothing Then lblTotal.Text = total.ToString("N0")
        If lblApproved IsNot Nothing Then lblApproved.Text = approved.ToString("N0")
        If lblPending IsNot Nothing Then lblPending.Text = pending.ToString("N0")
        If lblRejected IsNot Nothing Then lblRejected.Text = rejected.ToString("N0")
        If lblCheck IsNot Nothing Then lblCheck.Text = checkCount.ToString("N0")
    End Sub

    Private Async Sub BtnSave_Click(sender As Object, e As EventArgs)
        If isSaving Then Return

        If String.IsNullOrWhiteSpace(csvPath) Then
            csvPath = GetDefaultCsvPath()
        End If

        Await SaveCsvAsync(csvPath, False)
    End Sub

    Private Async Sub BtnSaveAs_Click(sender As Object, e As EventArgs)
        If isSaving Then Return
        Await SaveAsAsync()
    End Sub

    Private Async Function SaveAsAsync() As Task
        Using sfd As New SaveFileDialog()
            sfd.Title = "CSV dosyasını kaydet"
            sfd.Filter = "CSV Dosyası (*.csv)|*.csv"
            sfd.FileName = If(String.IsNullOrWhiteSpace(csvPath), "INO_Takip.csv", System.IO.Path.GetFileName(csvPath))

            If sfd.ShowDialog() = DialogResult.OK Then Await SaveCsvAsync(sfd.FileName, True)
        End Using
    End Function

    Private Async Function SaveCurrentCsvSilentlyAsync() As Task(Of Boolean)
        If databaseReadOnlyMode Then Return False

        ' Otomatik kayıt sürerken pencere kapatılırsa ikinci bir yazma başlatma.
        ' Devam eden kaydın bitmesini bekleyip gerçek sonucu dirty durumundan al.
        If isSaving Then
            Do While isSaving
                Await Task.Delay(25)
            Loop
            Return Not hasUnsavedChanges
        End If

        If String.IsNullOrWhiteSpace(csvPath) Then
            csvPath = GetDefaultCsvPath()
        End If

        Return Await SaveCsvAsync(csvPath, False)
    End Function

    Private Async Sub AutoSaveIfRequested()
        Await SaveCurrentCsvSilentlyAsync()
    End Sub

    Private Async Function SaveCsvAsync(targetPath As String, showMessage As Boolean) As Task(Of Boolean)
        If databaseReadOnlyMode Then Return False
        If isSaving Then Return False

        Dim saved As Boolean = False

        Try
            isSaving = True
            SetUiBusy(True, "CSV kaydediliyor, lütfen bekleyin...")

            RecomputeAllStatuses()

            Dim pathToSave = targetPath
            Await Task.Run(Sub() SaveCsvCore(pathToSave))

            csvPath = pathToSave
            lblStatus.Text = ""

            ' Veri değişikliği logları yalnız CSV fiziksel olarak yazıldıktan sonra kalıcılaştırılır.
            FlushPendingDataChangeLogs()
            WriteLog("CSV KAYIT", Nothing, "CSV dosyası kaydedildi.")

            If showMessage Then
                MessageBox.Show("CSV dosyası kaydedildi:" & Environment.NewLine & csvPath,
                                "Kaydedildi", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End If

            hasUnsavedChanges = False
            saved = True
        Catch ex As Exception
            MessageBox.Show("CSV kaydedilemedi:" & Environment.NewLine & ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            SetUiBusy(False, "")
            isSaving = False
        End Try

        Return saved
    End Function

    Private Sub SaveCsvCore(targetPath As String)
        If File.Exists(targetPath) AndAlso IsFileLocked(targetPath) Then
            Throw New IOException("CSV dosyası açık veya başka bir program tarafından kullanılıyor. Lütfen dosyayı kapatıp tekrar deneyin.")
        End If

        If File.Exists(targetPath) Then CreateBackup(targetPath)

        Dim tempPath = targetPath & ".tmp"

        Using sw As New StreamWriter(tempPath, False, New UTF8Encoding(True))
            sw.WriteLine(String.Join(delimiter, columns.Select(Function(c) CsvEscape(c))))

            For Each dr As DataRow In table.Select("", Bracket(InternalIdColumn) & " ASC")
                If dr.RowState = DataRowState.Deleted Then Continue For
                If Not HasMeaningfulData(dr) Then Continue For

                Dim values = columns.Select(Function(c) CsvEscape(Clean(dr(c))))
                sw.WriteLine(String.Join(delimiter, values))
            Next
        End Using

        If File.Exists(targetPath) Then File.Delete(targetPath)
        File.Move(tempPath, targetPath)
    End Sub

    Private Function CsvEscape(v As String) As String
        Dim s = If(v, "")
        Dim mustQuote = s.Contains(delimiter) OrElse s.Contains("""") OrElse s.Contains(vbCr) OrElse s.Contains(vbLf)

        s = s.Replace("""", """""")

        If mustQuote Then Return """" & s & """"
        Return s
    End Function

    Private Function IsFileLocked(path As String) As Boolean
        Try
            Using fs As New FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
            End Using
            Return False
        Catch ex As IOException
            Return True
        Catch
            Return False
        End Try
    End Function

    Private Sub CreateBackup(path As String)
        Dim dir = System.IO.Path.GetDirectoryName(path)
        If String.IsNullOrWhiteSpace(dir) Then Return

        Dim backupDir = System.IO.Path.Combine(dir, "INO_CSV_Backup")
        Directory.CreateDirectory(backupDir)

        Dim fileName = System.IO.Path.GetFileNameWithoutExtension(path)
        Dim ext = System.IO.Path.GetExtension(path)
        Dim backupPath = System.IO.Path.Combine(backupDir, $"{fileName}_{DateTime.Now:yyyyMMdd_HHmmss_fff}_backup{ext}")

        CleanupOldBackups(backupDir, fileName, ext, MaxCsvBackupCount - 1)
        File.Copy(path, backupPath, True)
        CleanupOldBackups(backupDir, fileName, ext, MaxCsvBackupCount)
    End Sub

    Private Sub CleanupOldBackups(backupDir As String, fileName As String, ext As String, maxToKeep As Integer)
        If maxToKeep < 1 Then Return
        If String.IsNullOrWhiteSpace(backupDir) OrElse Not Directory.Exists(backupDir) Then Return

        Try
            Dim pattern = $"{fileName}_*_backup{ext}"
            Dim oldBackups = New DirectoryInfo(backupDir).
                GetFiles(pattern).
                OrderByDescending(Function(f) f.LastWriteTimeUtc).
                ThenByDescending(Function(f) f.Name).
                Skip(maxToKeep).
                ToList()

            For Each backup In oldBackups
                Try
                    backup.Delete()
                Catch
                End Try
            Next
        Catch
        End Try
    End Sub

    Private Sub SetUiBusy(busy As Boolean, message As String)
        Me.UseWaitCursor = busy
        If dgv IsNot Nothing Then dgv.Enabled = Not busy
        If lblStatus IsNot Nothing Then lblStatus.Text = message
        Application.DoEvents()
    End Sub

    Private Function Clean(v As Object) As String
        If v Is Nothing OrElse v Is DBNull.Value Then Return ""
        Return Regex.Replace(Convert.ToString(v), "\s+", " ").Trim()
    End Function

    Private Function Normalize(v As Object) As String
        Dim t = Clean(v)

        t = t.Replace("ı", "I").Replace("İ", "I")
        t = t.Replace("ğ", "G").Replace("Ğ", "G")
        t = t.Replace("ü", "U").Replace("Ü", "U")
        t = t.Replace("ş", "S").Replace("Ş", "S")
        t = t.Replace("ö", "O").Replace("Ö", "O")
        t = t.Replace("ç", "C").Replace("Ç", "C")

        t = t.ToUpperInvariant()
        t = Regex.Replace(t, "[\s\-_.\/\\\(\)\[\]\{\}:;,+]+", "")

        Return t
    End Function

    Private Function Bracket(colName As String) As String
        Return "[" & colName.Replace("]", "\]") & "]"
    End Function

    Private Function EscapeRowFilterLike(v As String) As String
        Dim s = Clean(v)
        s = s.Replace("'", "''")
        s = s.Replace("[", "[[]")
        s = s.Replace("%", "[%]")
        s = s.Replace("*", "[*]")
        Return s
    End Function
End Class
