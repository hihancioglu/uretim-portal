Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Net
Imports System.Text.Json
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports Microsoft.Web.WebView2.Core
Imports Microsoft.Web.WebView2.WinForms

Public Class FrmMain
    Inherits Form

    Private Const SessionValidationFailureThreshold As Integer = 12
    Private Shared ReadOnly SessionValidationFailureMinimumDuration As TimeSpan = TimeSpan.FromMinutes(1)
    Private Const SessionReplacementFailureThreshold As Integer = 3
    Private Shared ReadOnly SessionReplacementFailureMinimumDuration As TimeSpan = TimeSpan.FromSeconds(10)

    Private qualityTicketButton As Button = Nothing
    Private productionBindingButton As Button = Nothing
    Private productionIssueTicketButton As Button = Nothing
    Private moldTicketButton As Button = Nothing
    Private mechanismQualityButton As Button = Nothing
    Private updateWizardButton As Button = Nothing
    Private productAdminButton As Button = Nothing
    Private missingDrawingCount As Integer = 0
    Private testRequestButton As Button = Nothing
    Private openTestRequestCount As Integer = 0
    Private pendingMechanismQualityCount As Integer = 0

    Private lblQualityTicketCount As Label = Nothing
    Private lblProductionTicketCount As Label = Nothing
    Private lblMoldTicketCount As Label = Nothing
    Private lblActiveBindingCount As Label = Nothing
    Private mainLayoutHost As TableLayoutPanel = Nothing
    Private summaryGrid As TableLayoutPanel = Nothing
    Private categoryGrid As TableLayoutPanel = Nothing
    Private lastSummaryColumnCount As Integer = -1
    Private lastCategoryColumnCount As Integer = -1
    Private lastDynamicMenuLayoutSignature As String = ""
    Private isReflowingCategoryGrid As Boolean = False
    Private mainBrowser As WebView2 = Nothing
    Private mainBrowserReady As Boolean = False
    Private isMainBrowserInitializing As Boolean = False
    Private latestTicketSummary As TicketSummary = Nothing
    Private mainUpdateStatus As String = ""

    Private ReadOnly ticketRefreshTimer As New Timer()
    Private ReadOnly inactivityTimer As New Timer()
    Private activityFilter As ActivityMessageFilter = Nothing
    Private lastActivityUtc As DateTime = DateTime.UtcNow
    Private lastSessionTouchUtc As DateTime = DateTime.MinValue
    Private isAutoClosing As Boolean = False
    Private isClosing As Boolean = False
    Private isRefreshingTickets As Boolean = False
    Private isSessionTouchInProgress As Boolean = False
    Private isSessionValidationInProgress As Boolean = False
    Private consecutiveSessionValidationFailureCount As Integer = 0
    Private pendingSessionValidationFailure As String = ""
    Private firstSessionValidationFailureUtc As DateTime = DateTime.MinValue
    Private isInactivityPromptVisible As Boolean = False
    Private isUpdateCheckInProgress As Boolean = False
    Private lastUpdateCheckUtc As DateTime = DateTime.MinValue
    Private availableUpdatePackagePath As String = ""
    Private lastNotifiedUpdatePackagePath As String = ""
    Private ticketNotificationBaselineReady As Boolean = False
    Private lastNotifiedActiveQualityCount As Integer = 0
    Private lastNotifiedActiveBindingCount As Integer = 0
    Private lastNotifiedPendingMechanismQualityCount As Integer = 0
    Private closeCleanupStarted As Boolean = False
    Private closeCleanupCompleted As Boolean = False

    Public Sub New()
        AppIconService.Apply(Me)
        AppNotificationService.Initialize(Me)
        Text = "A Blok"
        StartPosition = FormStartPosition.CenterScreen
        WindowState = FormWindowState.Maximized
        MinimumSize = New Size(760, 560)
        BackColor = Color.FromArgb(245, 247, 250)
        AutoScroll = False
        DoubleBuffered = True
        ResizeRedraw = True

        AddHandler Activated, AddressOf FrmMain_Activated
        AddHandler Shown, AddressOf FrmMain_Shown
        AddHandler Resize, AddressOf FrmMain_Resize
        AddHandler FormClosing, AddressOf FrmMain_FormClosing
        AddHandler FormClosed, AddressOf FrmMain_FormClosed

        activityFilter = New ActivityMessageFilter(AddressOf RegisterUserActivity)
        Application.AddMessageFilter(activityFilter)

        ticketRefreshTimer.Interval = 30000
        AddHandler ticketRefreshTimer.Tick, AddressOf TicketRefreshTimer_Tick

        inactivityTimer.Interval = 5000
        AddHandler inactivityTimer.Tick, AddressOf InactivityTimer_Tick

        BuildMainScreen()
        ticketRefreshTimer.Start()
        inactivityTimer.Start()
        lastSessionTouchUtc = DateTime.MinValue
    End Sub

    Private Sub RegisterUserActivity()
        lastActivityUtc = DateTime.UtcNow
    End Sub

    Private Async Sub FrmMain_Shown(sender As Object, e As EventArgs)
        Await InitializeMainBrowserAsync()
        QueueSessionTouch()
        RefreshTicketButtons()
        QueueUpdateAvailabilityCheck(True)
    End Sub

    Private Async Sub QueueSessionTouch()
        Dim sessionId = AppState.CurrentSessionId
        Dim username = AppState.CurrentUserName
        Dim role = AppState.CurrentRole
        If isClosing OrElse isSessionTouchInProgress OrElse String.IsNullOrWhiteSpace(sessionId) Then Return

        isSessionTouchInProgress = True
        Try
            Dim validationResult = Await Task.Run(
                Function()
                    If Not UserService.IsUserAuthorizationValid(username, role) Then Return "AUTHORIZATION_CHANGED"
                    Return DataService.EnsureCurrentUserSession(sessionId, username, Environment.MachineName)
                End Function)
            HandleSessionValidationResult(validationResult, sessionId, username)
        Catch ex As Exception
            ErrorLogService.Log("FrmMain.QueueSessionTouch", ex)
        Finally
            isSessionTouchInProgress = False
        End Try
    End Sub

    Private Async Sub QueueSessionValidation()
        Dim sessionId = AppState.CurrentSessionId
        Dim username = AppState.CurrentUserName
        Dim role = AppState.CurrentRole
        If isClosing OrElse isSessionValidationInProgress OrElse String.IsNullOrWhiteSpace(sessionId) Then Return

        isSessionValidationInProgress = True
        Try
            Dim validationResult = Await Task.Run(
                Function()
                    If Not UserService.IsUserAuthorizationValid(username, role) Then Return "AUTHORIZATION_CHANGED"
                    Dim sessionState = DataService.GetCurrentUserSessionState(sessionId, username, Environment.MachineName)
                    If sessionState = "SESSION_MISSING" Then
                        Return DataService.EnsureCurrentUserSession(sessionId, username, Environment.MachineName)
                    End If
                    Return sessionState
                End Function)
            HandleSessionValidationResult(validationResult, sessionId, username)
        Catch ex As Exception
            ErrorLogService.Log("FrmMain.QueueSessionValidation", ex)
        Finally
            isSessionValidationInProgress = False
        End Try
    End Sub

    Private Sub HandleSessionValidationResult(validationResult As String,
                                              validatedSessionId As String,
                                              validatedUsername As String)
        If isClosing OrElse isAutoClosing OrElse IsDisposed Then Return

        ' Doğrulama ağ/dosya erişimi nedeniyle gecikebilir. Bu sırada kullanıcı
        ' değiştirilmişse eski oturum için dönen sonuç yeni oturumu kapatmamalıdır.
        If Not String.Equals(
                AppState.CurrentSessionId,
                validatedSessionId,
                StringComparison.OrdinalIgnoreCase) OrElse
           Not String.Equals(
                AppState.CurrentUserName,
                validatedUsername,
                StringComparison.OrdinalIgnoreCase) Then

            Return
        End If

        If validationResult = "OK" OrElse validationResult = "SESSION_RESTORED" Then
            consecutiveSessionValidationFailureCount = 0
            pendingSessionValidationFailure = ""
            firstSessionValidationFailureUtc = DateTime.MinValue
            Return
        End If

        If validationResult = "SESSION_TERMINATED" Then
            BeginAutomaticClose(
                "SESSION_TERMINATED_BY_REQUEST",
                "Oturum, yönetici işlemi veya yeni giriş nedeniyle kapatma isteği aldı.",
                "Bu oturum yönetici tarafından veya yeni giriş nedeniyle sonlandırıldı. Program kapatılacak; lütfen gerekirse yeniden giriş yapın.")
            Return
        End If

        If Not String.Equals(
            pendingSessionValidationFailure,
            validationResult,
            StringComparison.Ordinal) Then

            pendingSessionValidationFailure = validationResult
            consecutiveSessionValidationFailureCount = 1
            firstSessionValidationFailureUtc = DateTime.UtcNow
        Else
            consecutiveSessionValidationFailureCount += 1
        End If

        Dim requiredFailureCount = SessionValidationFailureThreshold
        Dim requiredFailureDuration = SessionValidationFailureMinimumDuration
        If validationResult = "SESSION_REPLACED" Then
            requiredFailureCount = SessionReplacementFailureThreshold
            requiredFailureDuration = SessionReplacementFailureMinimumDuration
        End If

        If consecutiveSessionValidationFailureCount < requiredFailureCount Then Return
        If firstSessionValidationFailureUtc <> DateTime.MinValue AndAlso
           DateTime.UtcNow - firstSessionValidationFailureUtc < requiredFailureDuration Then Return

        If validationResult = "SESSION_REPLACED" Then
            BeginAutomaticClose(
                "SESSION_REPLACED",
                "Aynı kullanıcı ve bilgisayar için farklı oturum kimliği art arda üç kontrolde doğrulandığı için eski oturum kapatıldı.",
                "Bu kullanıcı ile aynı bilgisayarda yeni bir oturum açıldığı doğrulandığı için bu pencere kapatılacak. Lütfen yeni açılan pencereden devam edin.")
        ElseIf validationResult = "AUTHORIZATION_CHANGED" Then
            BeginAutomaticClose(
                "SESSION_USER_AUTHORIZATION_CHANGED",
                "Kullanıcı pasif yapıldığı, silindiği veya rolü değiştirildiği için program otomatik kapatıldı.",
                "Kullanıcı hesabınız veya yetkiniz değiştirildiği için program kapatılacak. Lütfen yeniden giriş yapın.")
        ElseIf validationResult = "SESSION_MISSING" Then
            BeginAutomaticClose(
                "SESSION_RECORD_MISSING",
                "Aktif oturum kaydı bulunamadığı için program otomatik kapatıldı. Bu durum kullanıcı değişikliği, aynı bilgisayarda yeniden giriş veya yetkili oturum işlemi sonucunda oluşabilir.",
                "Aktif oturumunuz art arda yapılan kontrollerde bulunamadı. Başka bir pencerede aynı kullanıcıyla yeniden giriş yapıldıysa eski oturum sonlandırılmış olabilir. Program kapatılacak; lütfen yeniden giriş yapın.")
        End If
    End Sub

    Private Sub InactivityTimer_Tick(sender As Object, e As EventArgs)
        Try
            If isAutoClosing OrElse isClosing OrElse isInactivityPromptVisible OrElse IsDisposed Then Return
            If String.IsNullOrWhiteSpace(AppState.CurrentSessionId) Then Return
            QueueSessionValidation()

            If IsInactivityAutoCloseEnabledForCurrentRole() Then
                Dim idleTime = DateTime.UtcNow - lastActivityUtc
                If idleTime >= TimeSpan.FromMinutes(10) Then
                    ShowInactivityPrompt()
                    Return
                End If
            End If

            If DateTime.UtcNow - lastSessionTouchUtc >= TimeSpan.FromSeconds(30) Then
                QueueSessionTouch()
                lastSessionTouchUtc = DateTime.UtcNow
            End If
        Catch ex As Exception
            ErrorLogService.Log("FrmMain.InactivityTimer_Tick", ex)
        End Try
    End Sub

    Private Function IsInactivityAutoCloseEnabledForCurrentRole() As Boolean
        Return Not AppState.IsLaboratoryUser
    End Function

    Private Sub ShowInactivityPrompt()
        If isInactivityPromptVisible OrElse isAutoClosing OrElse isClosing OrElse IsDisposed Then Return

        isInactivityPromptVisible = True
        inactivityTimer.Stop()

        Try
            Using warning As New FrmInactivityWarning()
                Dim owner = GetInactivityDialogOwner()
                AuditService.Log(
                    "INACTIVITY_WARNING_SHOWN",
                    "",
                    "",
                    "Kullanım dışı süre uyarısı gösterildi. Aktif pencere=" & If(Form.ActiveForm?.Text, "(yok)"))

                If warning.ShowDialog(owner) = DialogResult.OK Then
                    lastActivityUtc = DateTime.UtcNow
                    lastSessionTouchUtc = DateTime.MinValue
                    QueueSessionTouch()
                    AuditService.Log("INACTIVITY_CONTINUE", "", "", "Kullanıcı Devam Et seçeneğini kullandı.")
                    Return
                End If
            End Using

            BeginAutomaticClose(
                "INACTIVITY_AUTO_CLOSE",
                "Program 10 dakikalık kullanım dışı süreden sonra verilen 60 saniyelik uyarıya cevap alınamadığı veya kapatma seçildiği için otomatik kapatıldı.")
        Catch ex As Exception
            ErrorLogService.Log("FrmMain.ShowInactivityPrompt", ex)
            ' Uyarı başka bir modal pencere nedeniyle gösterilemiyorsa kullanıcıyı
            ' habersizce kapatmak yerine süreyi yenileyip sonraki denemeyi bekle.
            lastActivityUtc = DateTime.UtcNow
            lastSessionTouchUtc = DateTime.MinValue
            QueueSessionTouch()
        Finally
            isInactivityPromptVisible = False
            If Not isAutoClosing AndAlso Not isClosing AndAlso Not IsDisposed Then
                inactivityTimer.Start()
            End If
        End Try
    End Sub

    Private Function GetInactivityDialogOwner() As IWin32Window
        Dim active = Form.ActiveForm
        If active IsNot Nothing AndAlso
           Not active.IsDisposed AndAlso
           active.Visible Then
            Return active
        End If

        Return Me
    End Function

    Private Sub BeginAutomaticClose(auditAction As String,
                                    auditDetail As String,
                                    Optional userMessage As String = "")
        If isAutoClosing OrElse isClosing OrElse IsDisposed Then Return

        isAutoClosing = True
        Try
            inactivityTimer.Stop()
            ticketRefreshTimer.Stop()
        Catch ex As Exception
            ErrorLogService.Log("FrmMain.BeginAutomaticClose.StopTimers", ex)
        End Try

        Dim ignoredAuditTask As Task = Task.Run(
            Sub()
                Try
                    AuditService.Log(auditAction, "", "", auditDetail)
                Catch ex As Exception
                    ErrorLogService.Log("FrmMain.BeginAutomaticClose.Audit", ex, auditDetail)
                End Try
            End Sub)

        If userMessage <> "" Then
            Try
                MessageBox.Show(
                    GetInactivityDialogOwner(),
                    userMessage,
                    "Oturum Sonlandırılıyor",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning)
            Catch ex As Exception
                ErrorLogService.Log("FrmMain.BeginAutomaticClose.Message", ex, userMessage)
            End Try
        End If

        Close()
    End Sub

    Private Sub BuildMainScreen()
        Text = If(AppState.CurrentUserIsPermissionTestAccount,
                  "A Blok — YETKİ TEST HESABI",
                  "A Blok")
        If mainBrowser IsNot Nothing Then
            Try
                mainBrowser.Dispose()
            Catch ex As Exception
                ErrorLogService.Log("FrmMain.BuildMainScreen.DisposeBrowser", ex)
            End Try
        End If
        Controls.Clear()
        qualityTicketButton = Nothing
        productionBindingButton = Nothing
        productionIssueTicketButton = Nothing
        moldTicketButton = Nothing
        updateWizardButton = Nothing
        productAdminButton = Nothing
        missingDrawingCount = 0
        testRequestButton = Nothing
        openTestRequestCount = 0
        lblQualityTicketCount = Nothing
        lblProductionTicketCount = Nothing
        lblMoldTicketCount = Nothing
        lblActiveBindingCount = Nothing

        lastSummaryColumnCount = -1
        lastCategoryColumnCount = -1
        lastDynamicMenuLayoutSignature = ""
        ticketNotificationBaselineReady = False
        lastNotifiedActiveQualityCount = 0
        lastNotifiedActiveBindingCount = 0
        lastNotifiedPendingMechanismQualityCount = 0
        lastNotifiedUpdatePackagePath = ""
        latestTicketSummary = Nothing
        mainUpdateStatus = ""
        mainBrowserReady = False
        isMainBrowserInitializing = False

        mainBrowser = New WebView2() With {
            .Dock = DockStyle.Fill,
            .BackColor = BackColor,
            .DefaultBackgroundColor = BackColor
        }
        Controls.Add(mainBrowser)

        If IsHandleCreated Then
            BeginInvoke(New MethodInvoker(AddressOf InitializeMainBrowser))
        End If

        QueueTicketRefresh()
    End Sub

    Private Async Sub InitializeMainBrowser()
        Await InitializeMainBrowserAsync()
    End Sub

    Private Async Function InitializeMainBrowserAsync() As Task
        Dim targetBrowser = mainBrowser
        If targetBrowser Is Nothing OrElse targetBrowser.IsDisposed OrElse mainBrowserReady OrElse isMainBrowserInitializing Then Return

        isMainBrowserInitializing = True
        Try
            Dim userDataFolder = Path.Combine(AppPaths.LocalAppDataRoot, "WebView2", "MainDashboard")
            Directory.CreateDirectory(userDataFolder)
            Dim environment = Await CoreWebView2Environment.CreateAsync(Nothing, userDataFolder)
            If targetBrowser.IsDisposed OrElse Not ReferenceEquals(targetBrowser, mainBrowser) Then Return

            Await targetBrowser.EnsureCoreWebView2Async(environment)
            If targetBrowser.CoreWebView2 Is Nothing OrElse Not ReferenceEquals(targetBrowser, mainBrowser) Then Return

            targetBrowser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = False
            targetBrowser.CoreWebView2.Settings.AreDevToolsEnabled = AppState.IsAdmin
            AddHandler targetBrowser.CoreWebView2.NavigationCompleted, AddressOf MainBrowser_NavigationCompleted
            AddHandler targetBrowser.CoreWebView2.WebMessageReceived, AddressOf MainBrowser_WebMessageReceived

            Dim htmlPath = AppPaths.MainDashboardHtmlPath
            If Not File.Exists(htmlPath) Then
                targetBrowser.NavigateToString(BuildMainDashboardMessageHtml("Ana ekran HTML dosyası bulunamadı: " & htmlPath))
                Return
            End If

            targetBrowser.CoreWebView2.Navigate(New Uri(htmlPath).AbsoluteUri)
        Catch ex As Exception
            ErrorLogService.Log("FrmMain.InitializeMainBrowserAsync", ex)
            If targetBrowser IsNot Nothing AndAlso Not targetBrowser.IsDisposed Then
                targetBrowser.NavigateToString(BuildMainDashboardMessageHtml("Modern ana ekran açılamadı: " & ex.Message))
            End If
        Finally
            isMainBrowserInitializing = False
        End Try
    End Function

    Private Sub MainBrowser_NavigationCompleted(sender As Object, e As CoreWebView2NavigationCompletedEventArgs)
        If Not e.IsSuccess OrElse mainBrowser Is Nothing OrElse mainBrowser.CoreWebView2 Is Nothing Then Return
        mainBrowserReady = True
        SendMainDashboardPayload(latestTicketSummary)
    End Sub

    Private Sub MainBrowser_WebMessageReceived(sender As Object, e As CoreWebView2WebMessageReceivedEventArgs)
        Try
            Using document = JsonDocument.Parse(e.WebMessageAsJson)
                Dim root = document.RootElement
                Dim messageType As JsonElement
                If Not root.TryGetProperty("type", messageType) OrElse
                   Not String.Equals(messageType.GetString(), "main-command", StringComparison.Ordinal) Then Return

                Dim commandElement As JsonElement
                If Not root.TryGetProperty("command", commandElement) Then Return
                Dim command = If(commandElement.GetString(), "").Trim().ToLowerInvariant()
                If command = "" Then Return

                RegisterUserActivity()
                If Not IsMainDashboardCommandAllowed(command) Then
                    AuthorizationService.ShowDenied(New UnauthorizedAccessException("Bu işlem için yetkiniz bulunmuyor."), Me)
                    Return
                End If

                ' WebView2 mesaj geri çağrısı tamamlanmadan modal pencere açılırsa,
                ' kendi içinde WebView2 kullanan Ölçüm Girişi, Hurda ve REWORK
                ' pencereleri COM geri çağrısı içinde kilitlenip boş görünebilir.
                ' Komutu UI mesaj kuyruğuna bırakarak bu geri çağrının önce
                ' tamamen sonlanmasını sağla.
                Dim queuedCommand = command
                BeginInvoke(
                    New MethodInvoker(
                        Sub()
                            If isClosing OrElse IsDisposed Then Return
                            ExecuteMainDashboardCommand(queuedCommand)
                        End Sub))
            End Using
        Catch ex As Exception
            ErrorLogService.Log("FrmMain.MainBrowser_WebMessageReceived", ex)
        End Try
    End Sub

    Private Function IsMainDashboardCommandAllowed(command As String) As Boolean
        Select Case command
            Case "incoming-measurement-entry", "plastic-measurement-entry" : Return AppState.CanOpenMeasurement
            Case "incoming-measurement-history", "plastic-measurement-history" : Return AppState.CanViewMeasurementHistory
            Case "quality-tickets" : Return AppState.CanOpenQualityTickets
            Case "plastic-shift" : Return AppState.CanOpenPlasticShiftTracking
            Case "plastic-shift-errors" : Return AppState.CanOpenPlasticShiftErrorReport
            Case "spc-dashboard" : Return AppState.CanOpenSpcDashboard
            Case "scrap-dashboard" : Return AppState.CanOpenScrapDashboard
            Case "rework-dashboard" : Return AppState.CanOpenReworkDashboard
            Case "mechanism-control" : Return AppState.CanOpenMechanismQualityControl
            Case "ino-tracking" : Return AppState.CanOpenInoTracking
            Case "mechanism-shift" : Return AppState.CanOpenMechanismShiftTracking
            Case "production-binding" : Return AppState.CanOpenProductionBinding
            Case "mold-plan" : Return AppState.CanOpenMoldConnectionPlan
            Case "drawing-search" : Return AppState.CanOpenProductionDrawingSearch
            Case "binding-dashboard" : Return AppState.CanOpenMoldBindingDashboard
            Case "production-tickets" : Return AppState.CanOpenQualityToProductionTickets
            Case "mold-tickets" : Return AppState.CanOpenMoldTickets
            Case "new-mold" : Return AppState.CanOpenNewMoldCommissioning
            Case "products" : Return AppState.CanViewTechnicalDrawingAdmin
            Case "control-points" : Return AppState.CanViewTechnicalDrawingAdmin
            Case "msa-dashboard" : Return AppState.CanOpenMsaDashboard
            Case "test-requests" : Return AppState.CanOpenTestRequests
            Case "package-meters" : Return AppState.CanOpenPackageMeterControls
            Case "users", "audit", "update-wizard", "data-health" : Return AppState.CanOpenUserAdmin
            Case "permission-matrix" : Return AppState.CanViewPermissionMatrix
            Case "change-password", "switch-user", "exit" : Return True
            Case Else : Return False
        End Select
    End Function

    Private Sub ExecuteMainDashboardCommand(command As String)
        Select Case command
            Case "incoming-measurement-entry" : OpenMeasurementForScope(ProductInfo.DrawingScopeIncomingQuality)
            Case "incoming-measurement-history" : OpenHistoryForScope(ProductInfo.DrawingScopeIncomingQuality)
            Case "plastic-measurement-entry" : OpenMeasurementForScope(ProductInfo.DrawingScopePlastic)
            Case "plastic-measurement-history" : OpenHistoryForScope(ProductInfo.DrawingScopePlastic)
            Case "quality-tickets" : OpenQualityTickets(Me, EventArgs.Empty)
            Case "plastic-shift" : OpenPlasticShiftTracking(Me, EventArgs.Empty)
            Case "plastic-shift-errors" : OpenPlasticShiftErrorReports(Me, EventArgs.Empty)
            Case "spc-dashboard" : OpenSpcDashboard(Me, EventArgs.Empty)
            Case "scrap-dashboard" : OpenScrapDashboard(Me, EventArgs.Empty)
            Case "rework-dashboard" : OpenReworkDashboard(Me, EventArgs.Empty)
            Case "mechanism-control" : OpenMechanismQualityControl(Me, EventArgs.Empty)
            Case "ino-tracking" : OpenInoTracking(Me, EventArgs.Empty)
            Case "mechanism-shift" : OpenMechanismShiftTracking(Me, EventArgs.Empty)
            Case "production-binding" : OpenProductionTicketEntry(Me, EventArgs.Empty)
            Case "mold-plan" : OpenMoldConnectionPlan(Me, EventArgs.Empty)
            Case "drawing-search" : OpenProductionDrawingSearch(Me, EventArgs.Empty)
            Case "binding-dashboard" : OpenMoldBindingDashboard(Me, EventArgs.Empty)
            Case "production-tickets" : OpenQualityToProductionTickets(Me, EventArgs.Empty)
            Case "mold-tickets" : OpenMoldTickets(Me, EventArgs.Empty)
            Case "new-mold" : OpenNewMoldCommissioning(Me, EventArgs.Empty)
            Case "products" : OpenProducts(Me, EventArgs.Empty)
            Case "control-points" : OpenControlPoints(Me, EventArgs.Empty)
            Case "msa-dashboard" : OpenMsaDashboard(Me, EventArgs.Empty)
            Case "test-requests" : OpenTestRequests(Me, EventArgs.Empty)
            Case "package-meters" : OpenPackageMeterControls(Me, EventArgs.Empty)
            Case "users" : OpenUsers(Me, EventArgs.Empty)
            Case "audit" : OpenAudit(Me, EventArgs.Empty)
            Case "update-wizard" : OpenUpdateWizard(Me, EventArgs.Empty)
            Case "data-health" : OpenDataHealth(Me, EventArgs.Empty)
            Case "permission-matrix" : OpenPermissionMatrix(Me, EventArgs.Empty)
            Case "change-password" : ChangeOwnPassword(Me, EventArgs.Empty)
            Case "switch-user" : SwitchUser(Me, EventArgs.Empty)
            Case "exit" : Close()
        End Select
    End Sub

    Private Shared Function BuildMainDashboardMessageHtml(message As String) As String
        Return "<!doctype html><html><head><meta charset='utf-8'><style>" &
            "body{font-family:Segoe UI,sans-serif;background:#f3f7fb;color:#0d2748;padding:32px}" &
            ".box{background:#fff;border:1px solid #dbe5f0;border-radius:14px;padding:24px}" &
            "</style></head><body><div class='box'><b>A Blok Kalite Kontrol Süreçleri</b><p>" &
            WebUtility.HtmlEncode(If(message, "")) & "</p></div></body></html>"
    End Function

    Private Sub SendMainDashboardPayload(Optional summary As TicketSummary = Nothing)
        If Not mainBrowserReady OrElse mainBrowser Is Nothing OrElse mainBrowser.CoreWebView2 Is Nothing Then Return
        If summary Is Nothing Then summary = If(latestTicketSummary, New TicketSummary())

        Dim kpis As New List(Of Dictionary(Of String, Object)) From {
            New Dictionary(Of String, Object) From {
                {"label", "Aktif Kalite Ticket"}, {"value", summary.ActiveQualityCount},
                {"short", "KT"}, {"note", "Açık veya görüldü durumundaki kayıt"}
            },
            New Dictionary(Of String, Object) From {
                {"label", "Aktif Üretim Ticket"}, {"value", summary.ActiveProductionIssueCount},
                {"short", "ÜT"}, {"note", "Üretim tarafında işlem bekleyen kayıt"}
            },
            New Dictionary(Of String, Object) From {
                {"label", "Açık Kalıp Ticket"}, {"value", summary.OpenMoldCount},
                {"short", "KP"}, {"note", "Kapatılmamış kalıp kaydı"}
            },
            New Dictionary(Of String, Object) From {
                {"label", "Devam Eden Kalıp Bağlama"}, {"value", summary.ActiveBindingCount},
                {"short", "KB"}, {"note", summary.ActiveMoldCount.ToString() & " farklı kalıp"}
            }
        }

        Dim payload As New Dictionary(Of String, Object) From {
            {"type", "main-dashboard-data"},
            {"user", New Dictionary(Of String, Object) From {
                {"name", If(AppState.CurrentUserName, "")},
                {"role", AppState.NormalizeRole(AppState.CurrentRole)}
            }},
            {"isPermissionTest", AppState.CurrentUserIsPermissionTestAccount},
            {"updateStatus", mainUpdateStatus},
            {"generatedAt", DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")},
            {"kpis", kpis},
            {"groups", BuildMainDashboardGroups(summary)}
        }

        Dim options As New JsonSerializerOptions With {.PropertyNamingPolicy = JsonNamingPolicy.CamelCase}
        mainBrowser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload, options))
    End Sub

    Private Function BuildMainDashboardGroups(summary As TicketSummary) As List(Of Dictionary(Of String, Object))
        Dim groups As New List(Of Dictionary(Of String, Object))()

        Dim incoming As New List(Of Dictionary(Of String, Object))()
        Dim canSeeIncomingScope = AppState.IsIncomingQualityControlUser OrElse
                                  AppState.IsQualityControlManager OrElse
                                  AppState.IsManager OrElse
                                  AppState.IsAdmin
        If canSeeIncomingScope AndAlso AppState.CanOpenMeasurement Then incoming.Add(DashboardItem("incoming-measurement-entry", "Ölçüm Girişi", "Giriş kalite teknik resimleriyle ölçüm kaydı oluşturun", "ÖL", True))
        If canSeeIncomingScope AndAlso AppState.CanViewMeasurementHistory Then incoming.Add(DashboardItem("incoming-measurement-history", "Ölçüm Geçmişi", "Giriş kalite ölçüm kayıtlarını inceleyin", "GH"))
        AddDashboardGroup(groups, "Giriş Kalite Kontrol", incoming)

        Dim plastic As New List(Of Dictionary(Of String, Object))()
        Dim canSeePlasticScope = AppState.IsQualityControlUser OrElse
                                 AppState.IsQualityControlManager OrElse
                                 AppState.IsManager OrElse
                                 AppState.IsAdmin
        If canSeePlasticScope AndAlso AppState.CanOpenMeasurement Then plastic.Add(DashboardItem("plastic-measurement-entry", "Ölçüm Girişi", "Plastikhane teknik resimleriyle ölçüm kaydı oluşturun", "ÖL", True))
        If canSeePlasticScope AndAlso AppState.CanViewMeasurementHistory Then plastic.Add(DashboardItem("plastic-measurement-history", "Ölçüm Geçmişi", "Plastikhane ölçüm kayıtlarını inceleyin", "GH"))
        If AppState.CanOpenQualityTickets Then plastic.Add(DashboardItem("quality-tickets", "Kalite Ticketları", "Açık kalite bildirimlerini yönetin", "KT", False, DashboardBadge(summary.ActiveQualityCount, "aktif")))
        If AppState.CanOpenPlasticShiftTracking Then plastic.Add(DashboardItem("plastic-shift", "Plastikhane Vardiya Takip Listesi", "Vardiya takip kayıtlarını açın", "VT", AppState.CanModifyPlasticShiftTracking))
        If AppState.CanOpenPlasticShiftErrorReport Then plastic.Add(DashboardItem("plastic-shift-errors", "Hata Raporları", "Vardiya hata raporlarını inceleyin", "HR"))
        If AppState.CanOpenSpcDashboard Then plastic.Add(DashboardItem("spc-dashboard", "SPC Dashboard", "İstatistiksel proses kontrol görünümü", "SPC"))
        If AppState.CanOpenScrapDashboard Then plastic.Add(DashboardItem("scrap-dashboard", "Hurda Dashboard", "Hurda performansı ve KAIZEN fırsatları", "HD"))
        If AppState.CanOpenReworkDashboard Then plastic.Add(DashboardItem("rework-dashboard", "REWORK Dashboard", "Rework verilerini analiz edin", "RW"))
        AddDashboardGroup(groups, "Plastikhane Kalite Kontrol", plastic)

        Dim mechanism As New List(Of Dictionary(Of String, Object))()
        If AppState.CanOpenMechanismQualityControl Then mechanism.Add(DashboardItem("mechanism-control", "Mekanizma Kontrol Formu", "Mekanizma kalite kontrol kayıtlarını yönetin", "MK", True, DashboardBadge(summary.PendingMechanismQualityCount, "bekleyen")))
        If AppState.CanOpenInoTracking Then mechanism.Add(DashboardItem("ino-tracking", "İNO-1 / İNO-2 Takip", "İNO kontrol ve takip modülünü açın", "İNO"))
        If AppState.CanOpenMechanismShiftTracking Then mechanism.Add(DashboardItem("mechanism-shift", "Mekanizma Vardiya Takip Listesi", "Mekanizma vardiya takip kayıtlarını açın", "MV", AppState.CanModifyMechanismShiftTracking))
        AddDashboardGroup(groups, "Mekanizma Kalite Kontrol", mechanism)

        Dim production As New List(Of Dictionary(Of String, Object))()
        If AppState.CanOpenProductionBinding Then production.Add(DashboardItem("production-binding", "Kalıp Bağlama Bildirimi Oluştur", "Yeni bağlama bildirimi veya devam eden kayıt", "KB", False, DashboardBadge(summary.ActiveBindingCount, "devam ediyor")))
        If AppState.CanOpenMoldConnectionPlan Then production.Add(DashboardItem("mold-plan", "Bağlanacak Kalıp Listesi", "Planlanan kalıp bağlantılarını inceleyin", "KL"))
        If AppState.CanOpenProductionDrawingSearch Then production.Add(DashboardItem("drawing-search", "Teknik Resim Ara", "Üretim için teknik resim bulun", "ARA"))
        If AppState.CanOpenMoldBindingDashboard Then production.Add(DashboardItem("binding-dashboard", "Kalıp Bağlama Dashboardu", "Bağlama performansını izleyin", "BD"))
        If AppState.CanOpenQualityToProductionTickets Then production.Add(DashboardItem("production-tickets", "Üretim Ticketları", "Üretime açılan ticketları yönetin", "ÜT", False, DashboardBadge(summary.ActiveProductionIssueCount, "aktif")))
        If AppState.CanOpenMoldTickets Then production.Add(DashboardItem("mold-tickets", "Kalıp Ticketları", "Kalıp ticket kayıtlarını yönetin", "KP", False, DashboardBadge(summary.OpenMoldCount, "açık")))
        AddDashboardGroup(groups, "Üretim ve Ticket Yönetimi", production)

        Dim moldShop As New List(Of Dictionary(Of String, Object))()
        If AppState.CanOpenNewMoldCommissioning Then moldShop.Add(DashboardItem("new-mold", "Yeni Kalıp Devreye Alma", "Yeni kalıp devreye alma sürecini yönetin", "YK", AppState.CanModifyNewMoldCommissioning))
        AddDashboardGroup(groups, "Kalıphane", moldShop)

        Dim engineering As New List(Of Dictionary(Of String, Object))()
        If AppState.CanViewTechnicalDrawingAdmin Then
            engineering.Add(DashboardItem("products", "Ürün / Teknik Resim Yönetimi", "Ürün ve teknik resim tanımlarını yönetin", "TR", False, If(summary.MissingDrawingCountLoaded, DashboardBadge(summary.MissingDrawingCount, "eksik"), "")))
            engineering.Add(DashboardItem("control-points", "Kontrol Ölçüleri", "Kontrol noktaları ve ölçü tanımları", "KÖ"))
        End If
        If AppState.CanOpenMsaDashboard Then engineering.Add(DashboardItem("msa-dashboard", "MSA Dashboard", "Ölçüm sistemi analizlerini açın", "MSA", AppState.CanModifyMsaDashboard))
        AddDashboardGroup(groups, "Teknik Resim ve Ölçü Tanımları", engineering)

        Dim laboratory As New List(Of Dictionary(Of String, Object))()
        If AppState.CanOpenTestRequests Then laboratory.Add(DashboardItem("test-requests", "Test / Talep Formu", "Test taleplerini oluşturun ve sonuçlandırın", "TT", AppState.CanCreateTestRequest, DashboardBadge(summary.OpenTestRequestCount, "açık")))
        If AppState.CanOpenPackageMeterControls Then laboratory.Add(DashboardItem("package-meters", "Paket Sayaç Kontrolleri", "Paket sayaç kontrol kayıtlarını açın", "PS", AppState.CanModifyPackageMeterControls))
        AddDashboardGroup(groups, "Laboratuvar ve Test Yönetimi", laboratory)

        Dim administration As New List(Of Dictionary(Of String, Object))()
        If AppState.CanOpenUserAdmin Then
            administration.Add(DashboardItem("users", "Kullanıcı Yönetimi", "Kullanıcı ve rol tanımlarını yönetin", "KY"))
            administration.Add(DashboardItem("audit", "Log Kayıtları", "Denetim ve işlem geçmişini inceleyin", "LOG"))
            Dim updateBadge = If(String.IsNullOrWhiteSpace(availableUpdatePackagePath),
                                 "",
                                 DashboardBadge(1, "yeni güncelleme"))
            administration.Add(DashboardItem("update-wizard", "Program Güncelleme Sihirbazı", "Güncelleme paketini güvenli biçimde uygulayın", "UP", False, updateBadge))
            administration.Add(DashboardItem("data-health", "Veri Sağlığı", "Veri kaynaklarının durumunu kontrol edin", "VS"))
        End If
        If AppState.CanViewPermissionMatrix Then administration.Add(DashboardItem("permission-matrix", "Yetki Matrisi", "Rol ve işlem yetkilerini görüntüleyin", "YM"))
        AddDashboardGroup(groups, "Yönetim ve Sistem", administration)

        Return groups
    End Function

    Private Shared Sub AddDashboardGroup(groups As List(Of Dictionary(Of String, Object)),
                                         title As String,
                                         items As List(Of Dictionary(Of String, Object)))
        If items Is Nothing OrElse items.Count = 0 Then Return
        Dim alertCount As Integer = 0
        For Each item In items
            If Not item.ContainsKey("badge") Then Continue For

            Dim badgeText = If(TryCast(item("badge"), String), "").Trim()
            If badgeText = "" Then Continue For

            Dim firstPart = badgeText.Split(New Char() {" "c}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
            Dim itemCount As Integer
            If Integer.TryParse(firstPart, itemCount) AndAlso itemCount > 0 Then alertCount += itemCount
        Next

        groups.Add(New Dictionary(Of String, Object) From {
            {"title", title},
            {"alertCount", alertCount},
            {"items", items}
        })
    End Sub

    Private Shared Function DashboardItem(command As String,
                                          title As String,
                                          subtitle As String,
                                          shortText As String,
                                          Optional primary As Boolean = False,
                                          Optional badge As String = "") As Dictionary(Of String, Object)
        Return New Dictionary(Of String, Object) From {
            {"command", command}, {"title", title}, {"subtitle", subtitle},
            {"short", shortText}, {"primary", primary}, {"badge", badge}
        }
    End Function

    Private Shared Function DashboardBadge(value As Integer, unit As String) As String
        If value <= 0 Then Return ""
        Return value.ToString() & " " & unit
    End Function

    Private Sub FrmMain_Resize(sender As Object, e As EventArgs)
        ApplyMainScreenMetrics()
        If categoryGrid IsNot Nothing Then ReflowCategoryGrid(categoryGrid)
        If summaryGrid IsNot Nothing Then ReflowSummaryGrid(summaryGrid)
    End Sub

    Private Sub ApplyMainScreenMetrics()
        If mainLayoutHost Is Nothing OrElse mainLayoutHost.IsDisposed OrElse mainLayoutHost.RowStyles.Count < 3 Then Return

        Dim logicalWorkingWidth = ResponsiveFormService.GetLogicalWorkingAreaWidth(Me)
        Dim logicalWorkingHeight = ResponsiveFormService.GetLogicalWorkingAreaHeight(Me)
        Dim logicalClientHeight = ResponsiveFormService.GetLogicalClientHeight(Me)
        Dim effectiveHeight = If(logicalClientHeight > 0, logicalClientHeight, logicalWorkingHeight)
        Dim dpiScale = Math.Max(96, DeviceDpi) / 96.0R
        Dim compact = ResponsiveFormService.GetLayoutProfile(Me) = ResponsiveLayoutProfile.Compact
        Dim tightHeight = effectiveHeight > 0 AndAlso effectiveHeight < 860
        Dim veryTightHeight = effectiveHeight > 0 AndAlso effectiveHeight < 760

        Dim logicalPadding = If(compact OrElse tightHeight,
                                New Padding(12, 10, 12, 12),
                                New Padding(24, 18, 24, 24))
        mainLayoutHost.Padding = ScaleLogicalPadding(logicalPadding, dpiScale)

        Dim headerLogicalHeight = If(veryTightHeight, 80.0R, If(tightHeight, 86.0R, 92.0R))
        Dim summaryLogicalHeight = If(compact,
                                      150.0R,
                                      If(logicalWorkingWidth >= 1180 AndAlso tightHeight, 68.0R, If(tightHeight, 72.0R, 76.0R)))
        mainLayoutHost.RowStyles(0).Height = CSng(Math.Round(headerLogicalHeight * dpiScale))
        mainLayoutHost.RowStyles(1).Height = CSng(Math.Round(summaryLogicalHeight * dpiScale))
    End Sub

    Private Shared Function ScaleLogicalPadding(value As Padding, scale As Double) As Padding
        Return New Padding(
            CInt(Math.Round(value.Left * scale)),
            CInt(Math.Round(value.Top * scale)),
            CInt(Math.Round(value.Right * scale)),
            CInt(Math.Round(value.Bottom * scale)))
    End Function

    Private Function BuildHeaderPanel() As Control
        Dim pnl As New Panel() With {
            .Dock = DockStyle.Fill,
            .BackColor = Color.White,
            .Padding = New Padding(14, 8, 14, 8),
            .Margin = New Padding(0, 0, 0, 10),
            .AutoScroll = False
        }
        pnl.BorderStyle = BorderStyle.FixedSingle

        Dim content As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 1,
            .BackColor = Color.White,
            .Margin = New Padding(0),
            .Padding = New Padding(0)
        }
        Dim compact = ResponsiveFormService.GetLogicalWorkingAreaWidth(Me) < 900
        content.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, If(compact, 56.0F, 70.0F)))
        content.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, If(compact, 44.0F, 30.0F)))
        content.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        pnl.Controls.Add(content)

        Dim titleLayout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .BackColor = Color.White,
            .Margin = New Padding(0, 0, 12, 0),
            .Padding = New Padding(0)
        }
        titleLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 55.0F))
        titleLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 45.0F))
        content.Controls.Add(titleLayout, 0, 0)

        Dim title As New Label() With {
            .Text = "A Blok",
            .Font = New Font("Segoe UI", 18, FontStyle.Bold),
            .Dock = DockStyle.Fill,
            .AutoSize = False,
            .AutoEllipsis = True,
            .TextAlign = ContentAlignment.MiddleLeft,
            .ForeColor = Color.FromArgb(32, 44, 62),
            .Margin = New Padding(4, 0, 0, 0)
        }
        titleLayout.Controls.Add(title, 0, 0)

        Dim subTitle As New Label() With {
            .Text = "Kalite kontrol, üretim bildirimi, ticket ve teknik resim yönetimi ana ekranı",
            .Font = New Font("Segoe UI", 9.75F, FontStyle.Regular),
            .Dock = DockStyle.Fill,
            .AutoSize = False,
            .AutoEllipsis = True,
            .TextAlign = ContentAlignment.MiddleLeft,
            .ForeColor = Color.DimGray,
            .Margin = New Padding(5, 0, 0, 0)
        }
        titleLayout.Controls.Add(subTitle, 0, 1)

        Dim isPermissionTestAccount = AppState.CurrentUserIsPermissionTestAccount
        Dim userText = If(isPermissionTestAccount, "TEST • ", "") &
                       AppState.CurrentUserName & " / " & AppState.NormalizeRole(AppState.CurrentRole)
        Dim infoPanel As New Panel() With {
            .Dock = DockStyle.Fill,
            .Margin = New Padding(0),
            .BackColor = If(isPermissionTestAccount,
                            Color.FromArgb(255, 242, 196),
                            Color.FromArgb(247, 250, 253)),
            .MinimumSize = New Size(220, 0)
        }
        infoPanel.BorderStyle = BorderStyle.FixedSingle

        Dim infoLayout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 2,
            .Padding = New Padding(10, 4, 10, 4),
            .Margin = New Padding(0),
            .BackColor = Color.Transparent
        }
        infoLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 60.0F))
        infoLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 40.0F))
        infoLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 20.0F))
        infoLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        infoPanel.Controls.Add(infoLayout)

        Dim lblUserTitle As New Label() With {
            .Text = If(isPermissionTestAccount, "YETKİ TEST HESABI", "Aktif Kullanıcı"),
            .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold),
            .Dock = DockStyle.Fill,
            .ForeColor = If(isPermissionTestAccount, Color.FromArgb(156, 84, 0), Color.Gray)
        }
        infoLayout.Controls.Add(lblUserTitle, 0, 0)

        Dim lblUserValue As New Label() With {
            .Text = userText,
            .Font = New Font("Segoe UI", 11.0F, FontStyle.Bold),
            .Dock = DockStyle.Fill,
            .AutoEllipsis = True,
            .ForeColor = If(isPermissionTestAccount,
                            Color.FromArgb(128, 55, 0),
                            Color.FromArgb(25, 58, 100)),
            .TextAlign = ContentAlignment.MiddleLeft
        }
        infoLayout.Controls.Add(lblUserValue, 0, 1)

        Dim lblVersion As New Label() With {
            .Text = "Sürüm: " & ApplicationInstanceService.CurrentBuildVersion(),
            .Font = New Font("Segoe UI", 8.75F, FontStyle.Bold),
            .Dock = DockStyle.Fill,
            .AutoEllipsis = True,
            .ForeColor = Color.FromArgb(25, 58, 100),
            .TextAlign = ContentAlignment.MiddleRight,
            .Margin = New Padding(4, 0, 0, 0)
        }
        infoLayout.Controls.Add(lblVersion, 1, 1)

        Dim lblPc As New Label() With {
            .Text = "Bilgisayar: " & Environment.MachineName,
            .Font = New Font("Segoe UI", 8.75F, FontStyle.Regular),
            .Dock = DockStyle.Fill,
            .ForeColor = Color.DimGray,
            .TextAlign = ContentAlignment.MiddleRight,
            .AutoEllipsis = True
        }
        infoLayout.Controls.Add(lblPc, 1, 0)

        content.Controls.Add(infoPanel, 1, 0)
        Return pnl
    End Function

    Private Function BuildSummaryPanel() As Control
        Dim table As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 4,
            .RowCount = 1,
            .BackColor = BackColor,
            .Margin = New Padding(0, 0, 0, 10),
            .Padding = New Padding(0)
        }

        For i As Integer = 0 To 3
            table.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25.0F))
        Next
        table.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        table.Controls.Add(CreateSummaryCard("Aktif Kalite Ticket", lblQualityTicketCount, Color.FromArgb(233, 64, 87)), 0, 0)
        table.Controls.Add(CreateSummaryCard("Aktif Üretim Ticket", lblProductionTicketCount, Color.FromArgb(255, 159, 67)), 1, 0)
        table.Controls.Add(CreateSummaryCard("Açık Kalıp Ticket", lblMoldTicketCount, Color.FromArgb(136, 84, 208)), 2, 0)
        table.Controls.Add(CreateSummaryCard("Devam Eden Kalıp Bağlama", lblActiveBindingCount, Color.FromArgb(16, 172, 132)), 3, 0)
        summaryGrid = table
        AddHandler table.Resize, Sub() ReflowSummaryGrid(table)
        ReflowSummaryGrid(table)
        Return table
    End Function

    Private Function CreateSummaryCard(title As String, ByRef valueLabel As Label, accentColor As Color) As Control
        Dim card As New Panel() With {
            .Dock = DockStyle.Fill,
            .Margin = New Padding(0, 0, 12, 0),
            .BackColor = Color.White,
            .Padding = New Padding(0)
        }
        card.BorderStyle = BorderStyle.FixedSingle

        Dim cardLayout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 2,
            .BackColor = Color.White,
            .Margin = New Padding(0),
            .Padding = New Padding(0)
        }
        cardLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 6.0F))
        cardLayout.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        cardLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 44.0F))
        cardLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 56.0F))
        card.Controls.Add(cardLayout)

        Dim accent As New Panel() With {
            .BackColor = accentColor,
            .Dock = DockStyle.Fill,
            .Margin = New Padding(0)
        }
        cardLayout.SetRowSpan(accent, 2)
        cardLayout.Controls.Add(accent, 0, 0)

        Dim lblTitle As New Label() With {
            .Text = title,
            .Font = New Font("Segoe UI", 9.25F, FontStyle.Bold),
            .Dock = DockStyle.Fill,
            .Margin = New Padding(14, 2, 8, 0),
            .ForeColor = Color.DimGray,
            .TextAlign = ContentAlignment.MiddleLeft,
            .AutoEllipsis = True
        }
        cardLayout.Controls.Add(lblTitle, 1, 0)

        valueLabel = New Label() With {
            .Text = "0",
            .Font = New Font("Segoe UI", 18.0F, FontStyle.Bold),
            .Dock = DockStyle.Fill,
            .Margin = New Padding(14, 0, 8, 2),
            .ForeColor = accentColor,
            .TextAlign = ContentAlignment.MiddleLeft,
            .AutoEllipsis = True
        }
        cardLayout.Controls.Add(valueLabel, 1, 1)

        Return card
    End Function

    Private Sub ReflowSummaryGrid(table As TableLayoutPanel)
        If table Is Nothing OrElse table.IsDisposed OrElse table.Controls.Count = 0 Then Return

        ApplyMainScreenMetrics()
        Dim compact = ResponsiveFormService.GetLayoutProfile(table) = ResponsiveLayoutProfile.Compact
        Dim columnCount = If(compact, 2, 4)
        If lastSummaryColumnCount = columnCount Then Return

        table.SuspendLayout()
        Try
            lastSummaryColumnCount = columnCount
            Dim rowCount = CInt(Math.Ceiling(table.Controls.Count / CDbl(columnCount)))
            table.ColumnCount = columnCount
            table.RowCount = rowCount
            table.ColumnStyles.Clear()
            table.RowStyles.Clear()

            For i As Integer = 0 To columnCount - 1
                table.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F / columnCount))
            Next
            For i As Integer = 0 To rowCount - 1
                table.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F / rowCount))
            Next

            For i As Integer = 0 To table.Controls.Count - 1
                table.SetCellPosition(table.Controls(i), New TableLayoutPanelCellPosition(i Mod columnCount, i \ columnCount))
            Next
        Finally
            table.ResumeLayout(True)
        End Try
    End Sub

    Private Function BuildContentPanel() As Control
        Dim hasPlasticMeasurement = AppState.CanOpenMeasurement OrElse
                                    AppState.CanViewMeasurementHistory OrElse
                                    AppState.CanOpenQualityTickets OrElse
                                    AppState.CanOpenSpcDashboard OrElse
                                    AppState.CanOpenScrapDashboard OrElse
                                    AppState.CanOpenReworkDashboard
        Dim hasPlasticQuality = hasPlasticMeasurement OrElse AppState.CanOpenPlasticShiftTracking
        Dim hasMechanismQuality = AppState.CanOpenMechanismQualityControl OrElse
                                  AppState.CanOpenInoTracking OrElse
                                  AppState.CanOpenMechanismShiftTracking
        Dim hasProduction = AppState.CanOpenProductionBinding OrElse AppState.CanOpenMoldConnectionPlan OrElse AppState.CanOpenProductionDrawingSearch OrElse AppState.CanOpenMoldBindingDashboard OrElse
                            AppState.CanOpenQualityToProductionTickets OrElse AppState.CanOpenMoldTickets
        Dim hasEngineering = AppState.CanViewTechnicalDrawingAdmin OrElse AppState.CanOpenMsaDashboard
        Dim hasAdmin = AppState.CanOpenUserAdmin OrElse AppState.CanViewPermissionMatrix
        Dim hasTestRequests = AppState.CanOpenTestRequests
        Dim hasMoldShop = AppState.CanOpenNewMoldCommissioning

        Dim categoryCount As Integer = 1
        If hasPlasticQuality Then categoryCount += 1
        If hasMechanismQuality Then categoryCount += 1
        If hasProduction Then categoryCount += 1
        If hasEngineering Then categoryCount += 1
        If hasAdmin Then categoryCount += 1
        If hasTestRequests Then categoryCount += 1
        If hasMoldShop Then categoryCount += 1

        Dim logicalScreenWidth = ResponsiveFormService.GetLogicalWorkingAreaWidth(Me)
        Dim logicalScreenHeight = ResponsiveFormService.GetLogicalWorkingAreaHeight(Me)
        Dim maxColumns = If(logicalScreenWidth < 900, 1, If(logicalScreenWidth < 1450, 2, 3))
        If logicalScreenHeight > 0 AndAlso logicalScreenHeight < 820 AndAlso logicalScreenWidth >= 1180 Then
            maxColumns = 3
        End If
        Dim columnCount As Integer = Math.Min(categoryCount, maxColumns)
        Dim rowCount As Integer = Math.Max(1, CInt(Math.Ceiling(categoryCount / CDbl(columnCount))))

        Dim contentPanel As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = columnCount,
            .RowCount = rowCount,
            .Padding = New Padding(0),
            .BackColor = BackColor,
            .Margin = New Padding(0),
            .AutoScroll = False
        }

        For i As Integer = 0 To columnCount - 1
            contentPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F / columnCount))
        Next
        For i As Integer = 0 To rowCount - 1
            contentPanel.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F / rowCount))
        Next

        Dim grpPlasticQuality As FlowLayoutPanel = Nothing
        Dim grpMechanismQuality As FlowLayoutPanel = Nothing
        Dim grpProduction As FlowLayoutPanel = Nothing
        Dim grpEngineering As FlowLayoutPanel = Nothing
        Dim grpAdmin As FlowLayoutPanel = Nothing
        Dim grpTestRequests As FlowLayoutPanel = Nothing
        Dim grpMoldShop As FlowLayoutPanel = Nothing
        Dim grpSession As FlowLayoutPanel = Nothing
        Dim groupIndex As Integer = 0

        If hasPlasticQuality Then
            Dim qualityGroupTitle = If(AppState.IsIncomingQualityControlUser AndAlso Not AppState.IsQualityControlUser AndAlso Not AppState.IsQualityControlManager AndAlso Not AppState.IsAdmin,
                                       "Giriş Kalite Kontrol",
                                       "Plastikhane Kalite Kontrol")
            grpPlasticQuality = AddCategoryGroup(contentPanel, qualityGroupTitle, Color.FromArgb(232, 245, 238), groupIndex)
            groupIndex += 1
        End If

        If hasMechanismQuality Then
            grpMechanismQuality = AddCategoryGroup(contentPanel, "Mekanizma Kalite Kontrol", Color.FromArgb(232, 245, 233), groupIndex)
            groupIndex += 1
        End If

        If hasProduction Then
            grpProduction = AddCategoryGroup(contentPanel, "Üretim ve Ticket Yönetimi", Color.FromArgb(255, 244, 230), groupIndex)
            groupIndex += 1
        End If

        If hasMoldShop Then
            grpMoldShop = AddCategoryGroup(contentPanel, "Kalıphane", Color.FromArgb(242, 238, 250), groupIndex)
            groupIndex += 1
        End If

        If hasEngineering Then
            grpEngineering = AddCategoryGroup(contentPanel, "Teknik Resim ve Ölçü Tanımları", Color.FromArgb(237, 247, 237), groupIndex)
            groupIndex += 1
        End If

        If hasTestRequests Then
            grpTestRequests = AddCategoryGroup(contentPanel, "Laboratuvar ve Test Yönetimi", Color.FromArgb(234, 242, 255), groupIndex)
            groupIndex += 1
        End If

        If hasAdmin Then
            grpAdmin = AddCategoryGroup(contentPanel, "Yönetim ve Sistem", Color.FromArgb(243, 239, 255), groupIndex)
            groupIndex += 1
        End If

        grpSession = AddCategoryGroup(contentPanel, "Oturum", Color.FromArgb(240, 240, 240), groupIndex)

        If AppState.CanOpenMeasurement Then
            AddMenuButton(grpPlasticQuality, "Ölçüm Girişi", AddressOf OpenMeasurement, True)
        End If
        If AppState.CanViewMeasurementHistory Then
            AddMenuButton(grpPlasticQuality, "Ölçüm Geçmişi", AddressOf OpenHistory, False)
        End If

        If AppState.CanOpenQualityTickets Then
            qualityTicketButton = AddMenuButton(grpPlasticQuality, "Kalite Ticketları", AddressOf OpenQualityTickets, False)
        End If

        If AppState.CanOpenPlasticShiftTracking Then
            AddMenuButton(grpPlasticQuality,
                          "Plastikhane Vardiya Takip Listesi",
                          AddressOf OpenPlasticShiftTracking,
                          AppState.CanModifyPlasticShiftTracking)
        End If

        If AppState.CanOpenPlasticShiftErrorReport Then
            AddMenuButton(grpPlasticQuality,
                          "Hata Raporları",
                          AddressOf OpenPlasticShiftErrorReports,
                          False)
        End If

        If AppState.CanOpenSpcDashboard Then
            AddMenuButton(grpPlasticQuality, "SPC Dashboard", AddressOf OpenSpcDashboard, False)
        End If

        If AppState.CanOpenScrapDashboard Then
            AddMenuButton(grpPlasticQuality, "Hurda Dashboard", AddressOf OpenScrapDashboard, False)
        End If

        If AppState.CanOpenReworkDashboard Then
            AddMenuButton(grpPlasticQuality, "REWORK Dashboard", AddressOf OpenReworkDashboard, False)
        End If

        If AppState.CanOpenMechanismQualityControl Then
            mechanismQualityButton = AddMenuButton(grpMechanismQuality, "Mekanizma Kontrol Formu", AddressOf OpenMechanismQualityControl, True)
        End If

        If AppState.CanOpenInoTracking Then
            AddMenuButton(grpMechanismQuality, "İNO-1 / İNO-2 Takip", AddressOf OpenInoTracking)
        End If

        If AppState.CanOpenMechanismShiftTracking Then
            AddMenuButton(grpMechanismQuality,
                          "Mekanizma Vardiya Takip Listesi",
                          AddressOf OpenMechanismShiftTracking,
                          AppState.CanModifyMechanismShiftTracking)
        End If

        If AppState.CanOpenProductionBinding Then
            productionBindingButton = AddMenuButton(grpProduction, "Kalıp Bağlama Bildirimi Oluştur", AddressOf OpenProductionTicketEntry, False)
        End If

        If AppState.CanOpenMoldConnectionPlan Then
            AddMenuButton(grpProduction, "Bağlanacak Kalıp Listesi", AddressOf OpenMoldConnectionPlan, False)
        End If

        If AppState.CanOpenProductionDrawingSearch Then
            AddMenuButton(grpProduction, "Teknik Resim Ara", AddressOf OpenProductionDrawingSearch, False)
        End If

        If AppState.CanOpenMoldBindingDashboard Then
            AddMenuButton(grpProduction, "Kalıp Bağlama Dashboardu", AddressOf OpenMoldBindingDashboard, False)
        End If

        If AppState.CanOpenQualityToProductionTickets Then
            productionIssueTicketButton = AddMenuButton(grpProduction, "Üretim Ticketları", AddressOf OpenQualityToProductionTickets, False)
        End If

        If AppState.CanOpenMoldTickets Then
            moldTicketButton = AddMenuButton(grpProduction, "Kalıp Ticketları", AddressOf OpenMoldTickets, False)
        End If

        If AppState.CanOpenNewMoldCommissioning Then
            AddMenuButton(grpMoldShop,
                          "Yeni Kalıp Devreye Alma",
                          AddressOf OpenNewMoldCommissioning,
                          AppState.CanModifyNewMoldCommissioning)
        End If

        If AppState.CanViewTechnicalDrawingAdmin Then
            productAdminButton = AddMenuButton(grpEngineering, "Ürün / Teknik Resim Yönetimi", AddressOf OpenProducts, False)
            AddMenuButton(grpEngineering, "Kontrol Ölçüleri", AddressOf OpenControlPoints, False)
        End If

        If AppState.CanOpenMsaDashboard Then
            AddMenuButton(grpEngineering, "MSA Dashboard", AddressOf OpenMsaDashboard, AppState.CanModifyMsaDashboard)
        End If

        If AppState.CanOpenTestRequests Then
            testRequestButton = AddMenuButton(grpTestRequests, "Test / Talep Formu", AddressOf OpenTestRequests, AppState.CanCreateTestRequest)
        End If
        If AppState.CanOpenPackageMeterControls Then
            AddMenuButton(grpTestRequests,
                          "Paket Sayaç Kontrolleri",
                          AddressOf OpenPackageMeterControls,
                          AppState.CanModifyPackageMeterControls)
        End If

        If AppState.CanOpenUserAdmin Then
            AddMenuButton(grpAdmin, "Kullanıcı Yönetimi", AddressOf OpenUsers, False)
            AddMenuButton(grpAdmin, "Log Kayıtları", AddressOf OpenAudit, False)
            updateWizardButton = AddMenuButton(grpAdmin, "Program Güncelleme Sihirbazı", AddressOf OpenUpdateWizard, False)
            AddMenuButton(grpAdmin, "Veri Sağlığı", AddressOf OpenDataHealth, False)
        End If
        If AppState.CanViewPermissionMatrix Then
            AddMenuButton(grpAdmin, "Yetki Matrisi", AddressOf OpenPermissionMatrix, False)
        End If

        AddMenuButton(grpSession, "Şifremi Değiştir", AddressOf ChangeOwnPassword, False)
        AddMenuButton(grpSession, "Kullanıcı Değiştir", AddressOf SwitchUser, False)
        AddMenuButton(grpSession, "Çıkış", Sub(s, e) Close(), False)

        categoryGrid = contentPanel
        AddHandler contentPanel.Resize, Sub() ReflowCategoryGrid(contentPanel)
        ReflowCategoryGrid(contentPanel)
        Return contentPanel
    End Function

    Private Sub ReflowCategoryGrid(panel As TableLayoutPanel)
        If panel Is Nothing OrElse panel.IsDisposed OrElse panel.Controls.Count = 0 OrElse isReflowingCategoryGrid Then Return

        isReflowingCategoryGrid = True
        Dim appliedColumnCount As Integer = 0
        Dim appliedRowCount As Integer = 0
        panel.SuspendLayout()
        Try
            Dim profile = ResponsiveFormService.GetLayoutProfile(panel)
            Dim logicalScreenWidth = ResponsiveFormService.GetLogicalWorkingAreaWidth(Me)
            Dim logicalScreenHeight = ResponsiveFormService.GetLogicalWorkingAreaHeight(Me)
            Dim preferThreeColumns = logicalScreenHeight > 0 AndAlso
                                     logicalScreenHeight < 820 AndAlso
                                     logicalScreenWidth >= 1180 AndAlso
                                     panel.Controls.Count >= 5
            Dim requestedColumns As Integer
            Select Case profile
                Case ResponsiveLayoutProfile.Wide
                    requestedColumns = 3
                Case ResponsiveLayoutProfile.Standard
                    requestedColumns = If(preferThreeColumns, 3, 2)
                Case Else
                    requestedColumns = 1
            End Select

            Dim columnCount = Math.Max(1, Math.Min(panel.Controls.Count, requestedColumns))
            Dim rowCount = CInt(Math.Ceiling(panel.Controls.Count / CDbl(columnCount)))
            appliedColumnCount = columnCount
            appliedRowCount = rowCount
            lastCategoryColumnCount = columnCount

            panel.ColumnCount = columnCount
            panel.RowCount = rowCount
            panel.ColumnStyles.Clear()
            panel.RowStyles.Clear()

            For i As Integer = 0 To columnCount - 1
                panel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F / columnCount))
            Next

            For i As Integer = 0 To rowCount - 1
                panel.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F / rowCount))
            Next

            For i As Integer = 0 To panel.Controls.Count - 1
                panel.SetCellPosition(panel.Controls(i), New TableLayoutPanelCellPosition(i Mod columnCount, i \ columnCount))
            Next
            UpdateCategoryGroupMargins(panel, columnCount, rowCount)
        Finally
            panel.ResumeLayout(True)
            Try
                If appliedColumnCount > 0 AndAlso appliedRowCount > 0 Then
                    ApplyCategoryRowHeights(panel, appliedColumnCount, appliedRowCount)
                End If
            Finally
                isReflowingCategoryGrid = False
            End Try
        End Try
    End Sub

    Private Sub ApplyCategoryRowHeights(panel As TableLayoutPanel, columnCount As Integer, rowCount As Integer)
        If panel Is Nothing OrElse panel.IsDisposed OrElse rowCount <= 0 Then Return

        panel.PerformLayout()
        Dim requiredHeights(rowCount - 1) As Integer

        For Each container As Control In panel.Controls
            Dim rowIndex = panel.GetRow(container)
            If rowIndex < 0 OrElse rowIndex >= rowCount Then Continue For

            Dim buttonPanel = FindCategoryButtonPanel(container)
            Dim requiredContainerHeight As Integer = Math.Max(150, container.MinimumSize.Height)

            If buttonPanel IsNot Nothing Then
                AdjustMenuButtonWidths(buttonPanel)
                buttonPanel.PerformLayout()

                Dim contentBottom = buttonPanel.Padding.Top
                For Each button As Button In buttonPanel.Controls.OfType(Of Button)()
                    contentBottom = Math.Max(contentBottom, button.Bottom + button.Margin.Bottom)
                Next

                Dim headerHeight = 42
                requiredContainerHeight = Math.Max(
                    requiredContainerHeight,
                    headerHeight + contentBottom + buttonPanel.Padding.Bottom + 2)
            End If

            requiredHeights(rowIndex) = Math.Max(
                requiredHeights(rowIndex),
                requiredContainerHeight + container.Margin.Bottom)
        Next

        For rowIndex As Integer = 0 To rowCount - 1
            If requiredHeights(rowIndex) <= 0 Then requiredHeights(rowIndex) = 150
        Next

        Dim availableHeight = Math.Max(1, panel.ClientSize.Height)
        Dim requiredTotal = requiredHeights.Sum()
        Dim desiredHeights As New List(Of Integer)()
        Dim enableScroll As Boolean
        If requiredTotal <= availableHeight Then
            enableScroll = False
            Dim remaining = availableHeight - requiredTotal
            Dim extraPerRow = remaining \ rowCount
            Dim extraRemainder = remaining Mod rowCount

            For rowIndex As Integer = 0 To rowCount - 1
                Dim rowHeight = requiredHeights(rowIndex) + extraPerRow + If(rowIndex < extraRemainder, 1, 0)
                desiredHeights.Add(rowHeight)
            Next
        Else
            ' Çok düşük çözünürlükte fiziksel olarak sığmayan içerik yine de başka
            ' kartların altında kalmasın. Kaydırma yalnızca bu son çare durumunda açılır.
            enableScroll = True
            For rowIndex As Integer = 0 To rowCount - 1
                desiredHeights.Add(requiredHeights(rowIndex))
            Next
        End If

        Dim layoutAlreadyMatches = panel.AutoScroll = enableScroll AndAlso panel.RowStyles.Count = desiredHeights.Count
        If layoutAlreadyMatches Then
            For rowIndex As Integer = 0 To desiredHeights.Count - 1
                Dim style = panel.RowStyles(rowIndex)
                If style.SizeType <> SizeType.Absolute OrElse Math.Abs(style.Height - desiredHeights(rowIndex)) >= 1.0F Then
                    layoutAlreadyMatches = False
                    Exit For
                End If
            Next
        End If
        If layoutAlreadyMatches Then Return

        panel.SuspendLayout()
        Try
            panel.AutoScroll = enableScroll
            panel.RowStyles.Clear()
            For Each rowHeight In desiredHeights
                panel.RowStyles.Add(New RowStyle(SizeType.Absolute, CSng(rowHeight)))
            Next
        Finally
            panel.ResumeLayout(True)
        End Try
    End Sub

    Private Shared Function FindCategoryButtonPanel(parent As Control) As FlowLayoutPanel
        If parent Is Nothing Then Return Nothing

        Dim direct = TryCast(parent, FlowLayoutPanel)
        If direct IsNot Nothing Then Return direct

        For Each child As Control In parent.Controls
            Dim found = FindCategoryButtonPanel(child)
            If found IsNot Nothing Then Return found
        Next

        Return Nothing
    End Function

    Private Sub UpdateCategoryGroupMargins(panel As TableLayoutPanel, columnCount As Integer, rowCount As Integer)
        If panel Is Nothing Then Return

        For i As Integer = 0 To panel.Controls.Count - 1
            Dim col = i Mod columnCount
            Dim row = i \ columnCount
            Dim rightMargin = If(col = columnCount - 1, 0, 16)
            Dim bottomMargin = If(row = rowCount - 1, 0, 16)
            panel.Controls(i).Margin = New Padding(0, 0, rightMargin, bottomMargin)
        Next
    End Sub

    Private Function AddCategoryGroup(parent As TableLayoutPanel, title As String, headerColor As Color, index As Integer) As FlowLayoutPanel
        Dim container As New Panel() With {
            .Dock = DockStyle.Fill,
            .Margin = New Padding(0),
            .BackColor = Color.White,
            .MinimumSize = New Size(220, 150)
        }
        container.BorderStyle = BorderStyle.FixedSingle

        Dim groupLayout As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .BackColor = Color.White,
            .Padding = New Padding(0)
        }
        groupLayout.RowStyles.Add(New RowStyle(SizeType.Absolute, 42.0F))
        groupLayout.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        container.Controls.Add(groupLayout)

        Dim headerBand As New Panel() With {
            .Dock = DockStyle.Fill,
            .BackColor = headerColor
        }
        groupLayout.Controls.Add(headerBand, 0, 0)

        Dim lblTitle As New Label() With {
            .Text = title,
            .Font = New Font("Segoe UI", 10.5F, FontStyle.Bold),
            .ForeColor = Color.FromArgb(45, 45, 45),
            .AutoSize = False,
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(14, 0, 0, 0)
        }
        headerBand.Controls.Add(lblTitle)

        Dim inner As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .Padding = New Padding(10),
            .Margin = New Padding(0),
            .AutoScroll = False,
            .WrapContents = True,
            .FlowDirection = FlowDirection.LeftToRight,
            .BackColor = Color.White
        }
        AddHandler inner.Resize, Sub() AdjustMenuButtonWidths(inner)
        groupLayout.Controls.Add(inner, 0, 1)

        Dim col = index Mod parent.ColumnCount
        Dim row = index \ parent.ColumnCount
        parent.Controls.Add(container, col, row)
        Return inner
    End Function

    Private Function AddMenuButton(panel As FlowLayoutPanel, text As String, handler As EventHandler, Optional primary As Boolean = False) As Button
        Dim btn As New Button() With {
            .Text = text,
            .Width = 240,
            .Height = 68,
            .Font = New Font("Segoe UI", 10.0F, FontStyle.Bold),
            .Margin = New Padding(8),
            .TextAlign = ContentAlignment.MiddleCenter,
            .FlatStyle = FlatStyle.Flat,
            .UseVisualStyleBackColor = False,
            .Cursor = Cursors.Hand,
            .AutoEllipsis = False,
            .Padding = New Padding(10, 0, 10, 0),
            .Tag = "RESPONSIVE_NO_AUTO_SCALE"
        }
        btn.FlatAppearance.BorderSize = 1
        btn.FlatAppearance.BorderColor = Color.FromArgb(205, 212, 220)

        If primary Then
            btn.BackColor = Color.FromArgb(232, 242, 255)
            btn.ForeColor = Color.FromArgb(31, 71, 136)
        Else
            btn.BackColor = Color.White
            btn.ForeColor = Color.FromArgb(40, 40, 40)
        End If

        AddHandler btn.MouseEnter,
            Sub()
                If btn.Enabled Then
                    btn.BackColor = If(primary, Color.FromArgb(220, 235, 255), Color.FromArgb(248, 250, 252))
                End If
            End Sub
        AddHandler btn.MouseLeave,
            Sub()
                ResetButtonVisual(btn, primary)
            End Sub

        AddHandler btn.Click, handler
        panel.Controls.Add(btn)
        AdjustMenuButtonWidths(panel)
        Return btn
    End Function

    Private Sub AdjustMenuButtonWidths(panel As FlowLayoutPanel)
        If panel Is Nothing OrElse panel.ClientSize.Width <= 0 Then Return

        Dim profile = ResponsiveFormService.GetLayoutProfile(panel)
        Dim scale As Double = If(profile = ResponsiveLayoutProfile.Compact,
                                 0.82R,
                                 If(profile = ResponsiveLayoutProfile.Standard, 0.92R, 1.0R))
        Dim logicalScreenHeight = ResponsiveFormService.GetLogicalWorkingAreaHeight(Me)
        If logicalScreenHeight > 0 AndAlso logicalScreenHeight < 820 Then scale *= 0.9R
        If logicalScreenHeight > 0 AndAlso logicalScreenHeight < 720 Then scale *= 0.92R
        scale = Math.Max(0.76R, Math.Min(1.0R, scale))
        Dim minimumButtonWidth = CInt(Math.Round(220 * scale))
        Dim buttonHeight = Math.Max(48, CInt(Math.Round(60 * scale)))
        Dim buttonMargin = Math.Max(4, CInt(Math.Round(8 * scale)))
        Dim contentWidth = Math.Max(1, panel.ClientSize.Width - panel.Padding.Left - panel.Padding.Right)
        Dim fullRowWidth = Math.Max(80, contentWidth - (buttonMargin * 2))
        Dim halfRowWidth = CInt(Math.Floor((contentWidth - (buttonMargin * 4)) / 2.0R))
        Dim twoColumnLayout = halfRowWidth >= minimumButtonWidth
        Dim baseButtonWidth = If(twoColumnLayout, halfRowWidth, fullRowWidth)

        Dim buttons = panel.Controls.
            Cast(Of Control)().
            Select(Function(ctrl) TryCast(ctrl, Button)).
            Where(Function(btn) btn IsNot Nothing).
            ToList()

        Dim preferredWidths As New Dictionary(Of Button, Integer)()

        For Each btn In buttons
            preferredWidths(btn) = MeasureMenuButtonSingleLineWidth(btn)
        Next

        Dim index As Integer = 0
        While index < buttons.Count
            Dim current = buttons(index)
            Dim currentNeedsFullRow = Not twoColumnLayout OrElse
                                      buttons.Count = 1 OrElse
                                      preferredWidths(current) > baseButtonWidth

            If currentNeedsFullRow Then
                ApplyMenuButtonSize(current, fullRowWidth, buttonHeight, buttonMargin)
                index += 1
                Continue While
            End If

            If index = buttons.Count - 1 Then
                ApplyMenuButtonSize(current, fullRowWidth, buttonHeight, buttonMargin)
                index += 1
                Continue While
            End If

            Dim nextButton = buttons(index + 1)
            Dim nextNeedsFullRow = preferredWidths(nextButton) > baseButtonWidth

            If nextNeedsFullRow Then
                ApplyMenuButtonSize(current, fullRowWidth, buttonHeight, buttonMargin)
                index += 1
            Else
                ApplyMenuButtonSize(current, baseButtonWidth, buttonHeight, buttonMargin)
                ApplyMenuButtonSize(nextButton, baseButtonWidth, buttonHeight, buttonMargin)
                index += 2
            End If
        End While
    End Sub

    Private Sub ApplyMenuButtonSize(button As Button, targetWidth As Integer, baseHeight As Integer, buttonMargin As Integer)
        If button Is Nothing Then Return

        button.AutoEllipsis = False

        Dim targetHeight = Math.Max(baseHeight, MeasureMenuButtonWrappedHeight(button, targetWidth))
        button.Width = targetWidth
        button.Height = targetHeight
        button.MinimumSize = New Size(targetWidth, targetHeight)
        button.Margin = New Padding(buttonMargin)
    End Sub

    Private Function MeasureMenuButtonSingleLineWidth(button As Button) As Integer
        If button Is Nothing OrElse String.IsNullOrWhiteSpace(button.Text) Then Return 0

        Dim measured = TextRenderer.MeasureText(
            button.Text,
            button.Font,
            New Size(2400, 600),
            TextFormatFlags.NoPrefix Or TextFormatFlags.SingleLine)

        Return measured.Width + button.Padding.Left + button.Padding.Right + 18
    End Function

    Private Function MeasureMenuButtonWrappedHeight(button As Button, width As Integer) As Integer
        If button Is Nothing OrElse String.IsNullOrWhiteSpace(button.Text) Then Return 0

        Dim textWidth = Math.Max(40, width - button.Padding.Left - button.Padding.Right - 16)
        Dim measured = TextRenderer.MeasureText(
            button.Text,
            button.Font,
            New Size(textWidth, 400),
            TextFormatFlags.NoPrefix Or TextFormatFlags.WordBreak)

        Return measured.Height + button.Padding.Top + button.Padding.Bottom + 20
    End Function

    Private Sub ResetButtonVisual(btn As Button, primary As Boolean)
        If btn Is productionBindingButton AndAlso lblActiveBindingCount IsNot Nothing Then
            Dim hasWarning As Integer = 0
            Integer.TryParse(lblActiveBindingCount.Text, hasWarning)
            If hasWarning > 0 Then
                btn.BackColor = Color.MistyRose
                btn.ForeColor = Color.DarkRed
                Return
            End If
        End If

        If btn Is qualityTicketButton AndAlso lblQualityTicketCount IsNot Nothing Then
            Dim c As Integer = 0
            Integer.TryParse(lblQualityTicketCount.Text, c)
            If c > 0 Then
                btn.BackColor = Color.MistyRose
                btn.ForeColor = Color.DarkRed
                Return
            End If
        End If

        If btn Is productionIssueTicketButton AndAlso lblProductionTicketCount IsNot Nothing Then
            Dim c As Integer = 0
            Integer.TryParse(lblProductionTicketCount.Text, c)
            If c > 0 Then
                btn.BackColor = Color.MistyRose
                btn.ForeColor = Color.DarkRed
                Return
            End If
        End If

        If btn Is moldTicketButton AndAlso lblMoldTicketCount IsNot Nothing Then
            Dim c As Integer = 0
            Integer.TryParse(lblMoldTicketCount.Text, c)
            If c > 0 Then
                btn.BackColor = Color.MistyRose
                btn.ForeColor = Color.DarkRed
                Return
            End If
        End If

        If btn Is productAdminButton AndAlso missingDrawingCount > 0 Then
            btn.BackColor = Color.MistyRose
            btn.ForeColor = Color.DarkRed
            Return
        End If

        If btn Is testRequestButton AndAlso openTestRequestCount > 0 Then
            btn.BackColor = Color.MistyRose
            btn.ForeColor = Color.DarkRed
            Return
        End If

        If btn Is mechanismQualityButton AndAlso pendingMechanismQualityCount > 0 Then
            btn.BackColor = Color.MistyRose
            btn.ForeColor = Color.DarkRed
            Return
        End If

        If primary Then
            btn.BackColor = Color.FromArgb(232, 242, 255)
            btn.ForeColor = Color.FromArgb(31, 71, 136)
        Else
            btn.BackColor = Color.White
            btn.ForeColor = Color.FromArgb(40, 40, 40)
        End If
    End Sub

    Private Sub FrmMain_Activated(sender As Object, e As EventArgs)
        QueueTicketRefresh()
        QueueUpdateAvailabilityCheck(False)
    End Sub

    Private Async Sub QueueUpdateAvailabilityCheck(force As Boolean)
        If isClosing OrElse IsDisposed OrElse isUpdateCheckInProgress OrElse Not AppState.CanOpenUserAdmin Then Return
        If Not force AndAlso DateTime.UtcNow - lastUpdateCheckUtc < TimeSpan.FromSeconds(15) Then Return

        isUpdateCheckInProgress = True
        lastUpdateCheckUtc = DateTime.UtcNow
        Try
            Dim info = Await Task.Run(Function() UpdateAvailabilityService.CheckForUpdate())
            If isClosing OrElse IsDisposed Then Return

            availableUpdatePackagePath = If(info IsNot Nothing AndAlso info.IsAvailable, info.PackagePath, "")
            If info IsNot Nothing AndAlso info.IsAvailable Then
                Dim versionText = If(info.AvailableBuildStamp, "").Trim()
                mainUpdateStatus = If(versionText = "",
                                      "Program için yeni bir güncelleme paketi bulundu.",
                                      "Yeni program sürümü bulundu: " & versionText)
                If updateWizardButton IsNot Nothing Then
                    updateWizardButton.Text = "Program Güncelleme Sihirbazı" & Environment.NewLine & "(Yeni güncelleme var)"
                    updateWizardButton.BackColor = Color.MistyRose
                    updateWizardButton.ForeColor = Color.DarkRed
                End If
                NotifyUpdateAvailable(info)
            Else
                mainUpdateStatus = ""
                If updateWizardButton IsNot Nothing Then
                    updateWizardButton.Text = "Program Güncelleme Sihirbazı"
                    updateWizardButton.BackColor = Color.White
                    updateWizardButton.ForeColor = Color.FromArgb(40, 40, 40)
                End If
                lastNotifiedUpdatePackagePath = ""
            End If
            SendMainDashboardPayload(latestTicketSummary)
        Catch ex As Exception
            ErrorLogService.Log("FrmMain.QueueUpdateAvailabilityCheck", ex)
            mainUpdateStatus = "Güncelleme durumu şu anda kontrol edilemedi."
            If Not isClosing AndAlso Not IsDisposed AndAlso updateWizardButton IsNot Nothing Then
                updateWizardButton.Text = "Program Güncelleme Sihirbazı" & Environment.NewLine & "(Kontrol edilemedi)"
                updateWizardButton.BackColor = Color.LemonChiffon
                updateWizardButton.ForeColor = Color.DarkGoldenrod
            End If
            SendMainDashboardPayload(latestTicketSummary)
        Finally
            isUpdateCheckInProgress = False
        End Try
    End Sub

    Private Sub NotifyUpdateAvailable(info As UpdateAvailabilityService.UpdateAvailabilityInfo)
        If info Is Nothing OrElse Not info.IsAvailable Then Return

        Dim packageKey = If(info.PackagePath, "").Trim()
        If packageKey = "" Then packageKey = If(info.AvailableBuildStamp, "").Trim()
        If packageKey = "" Then Return
        If String.Equals(lastNotifiedUpdatePackagePath, packageKey, StringComparison.OrdinalIgnoreCase) Then Return

        lastNotifiedUpdatePackagePath = packageKey

        Dim versionText = If(info.AvailableBuildStamp, "").Trim()
        Dim message = If(versionText = "",
                         "Program için yeni bir güncelleme paketi bulundu.",
                         "Yeni sürüm bulundu: " & versionText)

        AppNotificationService.ShowInfo("Yeni güncelleme var", message)
    End Sub

    Private Sub TicketRefreshTimer_Tick(sender As Object, e As EventArgs)
        QueueTicketRefresh()
    End Sub

    Private Async Sub FrmMain_FormClosing(sender As Object, e As FormClosingEventArgs)
        If closeCleanupCompleted Then Return

        If e.CloseReason = CloseReason.WindowsShutDown OrElse e.CloseReason = CloseReason.TaskManagerClosing Then
            closeCleanupCompleted = True
            ApplicationLifecycleService.RunExitCleanupInBackground(AppState.CurrentSessionId)
            Return
        End If

        If e.CloseReason = CloseReason.UserClosing AndAlso Not isAutoClosing Then
            If MessageBox.Show("Programdan çıkmak istediğinizden emin misiniz?",
                               "Çıkış Onayı",
                               MessageBoxButtons.YesNo,
                               MessageBoxIcon.Question,
                               MessageBoxDefaultButton.Button2) <> DialogResult.Yes Then
                e.Cancel = True
                Return
            End If
        End If

        e.Cancel = True
        If closeCleanupStarted Then Return

        closeCleanupStarted = True
        isClosing = True

        Try
            Enabled = False
            Hide()
        Catch ex As Exception
            ErrorLogService.Log("FrmMain.FormClosing.Hide", ex)
        End Try

        Try
            Dim cleanupTask = ApplicationLifecycleService.RunExitCleanupAsync(AppState.CurrentSessionId)
            Await Task.WhenAny(cleanupTask, Task.Delay(1500))
        Catch ex As Exception
            ErrorLogService.Log("FrmMain.FormClosing.Cleanup", ex)
        End Try

        closeCleanupCompleted = True
        Close()
    End Sub

    Private Sub FrmMain_FormClosed(sender As Object, e As FormClosedEventArgs)
        isClosing = True

        Try
            ticketRefreshTimer.Stop()
            RemoveHandler ticketRefreshTimer.Tick, AddressOf TicketRefreshTimer_Tick
        Catch ex As Exception
            ErrorLogService.Log("FrmMain.FormClosed.TicketTimer", ex)
        End Try

        Try
            inactivityTimer.Stop()
            RemoveHandler inactivityTimer.Tick, AddressOf InactivityTimer_Tick
        Catch ex As Exception
            ErrorLogService.Log("FrmMain.FormClosed.InactivityTimer", ex)
        End Try

        Try
            If activityFilter IsNot Nothing Then Application.RemoveMessageFilter(activityFilter)
        Catch ex As Exception
            ErrorLogService.Log("FrmMain.FormClosed.ActivityFilter", ex)
        End Try

        AppNotificationService.Shutdown()

        ApplicationLifecycleService.RunExitCleanupInBackground(AppState.CurrentSessionId)
    End Sub

    Private Sub QueueTicketRefresh()
        If isClosing OrElse IsDisposed OrElse Not IsHandleCreated Then Return

        Try
            BeginInvoke(CType(Sub() RefreshTicketButtons(), MethodInvoker))
        Catch ex As Exception
            ErrorLogService.Log("FrmMain.QueueTicketRefresh", ex)
        End Try
    End Sub

    Private NotInheritable Class TicketSummary
        Public Property ActiveBindingCount As Integer
        Public Property ActiveMoldCount As Integer
        Public Property ActiveQualityCount As Integer
        Public Property ActiveProductionIssueCount As Integer
        Public Property OpenMoldCount As Integer
        Public Property PendingMechanismQualityCount As Integer
        Public Property MissingDrawingCount As Integer
        Public Property MissingDrawingCountLoaded As Boolean
        Public Property OpenTestRequestCount As Integer
    End Class

    Private Async Sub RefreshTicketButtons()
        If isClosing OrElse IsDisposed OrElse isRefreshingTickets Then Return

        isRefreshingTickets = True
        Try
            Dim summary = Await Task.Run(Function() LoadTicketSummary())
            If isClosing OrElse IsDisposed OrElse Not IsHandleCreated Then Return

            ApplyTicketSummary(summary)
        Catch ex As Exception
            ErrorLogService.Log("FrmMain.RefreshTicketButtons", ex)
        Finally
            isRefreshingTickets = False
        End Try
    End Sub

    Private Function LoadTicketSummary() As TicketSummary
        Dim summary As New TicketSummary()

        Dim activeBindingRows = DataService.GetMoldBindingRecords().
            Where(Function(r) String.Equals(DataService.GetValue(r, "Status"), "STARTED", StringComparison.OrdinalIgnoreCase)).
            ToList()

        If Not AppState.CanViewAllMoldBindingDashboard Then
            Dim currentUser = If(AppState.CurrentUserName, "").Trim()
            activeBindingRows = activeBindingRows.
                Where(Function(r) currentUser <> "" AndAlso
                                  (String.Equals(DataService.GetValue(r, "StartedBy"), currentUser, StringComparison.OrdinalIgnoreCase) OrElse
                                   String.Equals(DataService.GetValue(r, "CompletedBy"), currentUser, StringComparison.OrdinalIgnoreCase))).
                ToList()
        End If

        summary.ActiveBindingCount = activeBindingRows.Count
        summary.ActiveMoldCount = activeBindingRows.
            Select(Function(r) DataService.GetValue(r, "MoldCode").Trim()).
            Where(Function(x) x <> "").
            Distinct(StringComparer.OrdinalIgnoreCase).
            Count()

        If summary.ActiveMoldCount = 0 Then summary.ActiveMoldCount = summary.ActiveBindingCount

        summary.ActiveQualityCount = DataService.GetProductionTickets().
            Where(Function(r)
                      Dim st = DataService.GetValue(r, "Status")
                      Return String.Equals(st, "OPEN", StringComparison.OrdinalIgnoreCase) OrElse
                             String.Equals(st, "SEEN", StringComparison.OrdinalIgnoreCase)
                  End Function).
            Count()

        summary.ActiveProductionIssueCount = DataService.GetQualityToProductionTickets().
            Where(Function(r)
                      Dim st = DataService.GetValue(r, "Status")
                      Return String.Equals(st, "OPEN", StringComparison.OrdinalIgnoreCase) OrElse
                             String.Equals(st, "SEEN", StringComparison.OrdinalIgnoreCase)
                  End Function).
            Count()

        summary.OpenMoldCount = DataService.GetMoldTickets().
            Where(Function(r) String.Equals(DataService.GetValue(r, "Status"), "OPEN", StringComparison.OrdinalIgnoreCase)).
            Count()

        If AppState.CanOpenMechanismQualityControl Then
            summary.PendingMechanismQualityCount = DataService.GetMechanismQualityControls().
                Where(Function(r) String.Equals(DataService.GetValue(r, "Status"), "PENDING", StringComparison.OrdinalIgnoreCase)).
                Count()
        End If

        If AppState.CanViewTechnicalDrawingAdmin Then
            Try
                summary.MissingDrawingCount = FrmTodayMoldDrawingStatus.CountMissingDrawings()
                summary.MissingDrawingCountLoaded = True
            Catch ex As Exception
                ErrorLogService.Log("FrmMain.LoadTicketSummary.MissingDrawings", ex)
            End Try
        End If

        If AppState.CanOpenTestRequests Then
            summary.OpenTestRequestCount = DataService.GetTestRequests().
                Where(Function(row)
                          Dim status = DataService.GetValue(row, "Status")
                          Return String.Equals(status, "OPEN", StringComparison.OrdinalIgnoreCase) OrElse
                                 String.Equals(status, "ACCEPTED", StringComparison.OrdinalIgnoreCase)
                      End Function).
                Count()
        End If

        Return summary
    End Function

    Private Sub ApplyTicketSummary(summary As TicketSummary)
        If summary Is Nothing Then Return

        latestTicketSummary = summary
        NotifyTicketSummaryChanges(summary)

        If lblActiveBindingCount IsNot Nothing Then lblActiveBindingCount.Text = summary.ActiveBindingCount.ToString()

        If productionBindingButton IsNot Nothing Then
            If summary.ActiveBindingCount > 0 Then
                productionBindingButton.Text = "Kalıp Bağlama Bildirimi Oluştur (" & summary.ActiveMoldCount.ToString() & " kalıp)"
                productionBindingButton.BackColor = Color.MistyRose
                productionBindingButton.ForeColor = Color.DarkRed
            Else
                productionBindingButton.Text = "Kalıp Bağlama Bildirimi Oluştur"
                productionBindingButton.BackColor = Color.White
                productionBindingButton.ForeColor = Color.FromArgb(40, 40, 40)
            End If
        End If

        If lblQualityTicketCount IsNot Nothing Then lblQualityTicketCount.Text = summary.ActiveQualityCount.ToString()

        If qualityTicketButton IsNot Nothing Then
            qualityTicketButton.Text = "Kalite Ticketları (" & summary.ActiveQualityCount.ToString() & " aktif)"
            If summary.ActiveQualityCount > 0 Then
                qualityTicketButton.BackColor = Color.MistyRose
                qualityTicketButton.ForeColor = Color.DarkRed
            Else
                qualityTicketButton.BackColor = Color.White
                qualityTicketButton.ForeColor = Color.FromArgb(40, 40, 40)
            End If
        End If

        If lblProductionTicketCount IsNot Nothing Then lblProductionTicketCount.Text = summary.ActiveProductionIssueCount.ToString()

        If productionIssueTicketButton IsNot Nothing Then
            productionIssueTicketButton.Text = "Üretim Ticketları (" & summary.ActiveProductionIssueCount.ToString() & " aktif)"
            If summary.ActiveProductionIssueCount > 0 Then
                productionIssueTicketButton.BackColor = Color.MistyRose
                productionIssueTicketButton.ForeColor = Color.DarkRed
            Else
                productionIssueTicketButton.BackColor = Color.White
                productionIssueTicketButton.ForeColor = Color.FromArgb(40, 40, 40)
            End If
        End If

        If lblMoldTicketCount IsNot Nothing Then lblMoldTicketCount.Text = summary.OpenMoldCount.ToString()

        If moldTicketButton IsNot Nothing Then
            moldTicketButton.Text = "Kalıp Ticketları (" & summary.OpenMoldCount.ToString() & " açık)"
            If summary.OpenMoldCount > 0 Then
                moldTicketButton.BackColor = Color.MistyRose
                moldTicketButton.ForeColor = Color.DarkRed
            Else
                moldTicketButton.BackColor = Color.White
                moldTicketButton.ForeColor = Color.FromArgb(40, 40, 40)
            End If
        End If

        If productAdminButton IsNot Nothing AndAlso summary.MissingDrawingCountLoaded Then
            missingDrawingCount = summary.MissingDrawingCount
            If missingDrawingCount > 0 Then
                productAdminButton.Text = "Ürün / Teknik Resim Yönetimi (" & missingDrawingCount.ToString() & " eksik)"
                productAdminButton.BackColor = Color.MistyRose
                productAdminButton.ForeColor = Color.DarkRed
            Else
                productAdminButton.Text = "Ürün / Teknik Resim Yönetimi"
                productAdminButton.BackColor = Color.White
                productAdminButton.ForeColor = Color.FromArgb(40, 40, 40)
            End If
        End If

        If testRequestButton IsNot Nothing Then
            openTestRequestCount = summary.OpenTestRequestCount
            If openTestRequestCount > 0 Then
                testRequestButton.Text = "Test / Talep Formu (" & openTestRequestCount.ToString() & " açık)"
                testRequestButton.BackColor = Color.MistyRose
                testRequestButton.ForeColor = Color.DarkRed
            Else
                testRequestButton.Text = "Test / Talep Formu"
                testRequestButton.BackColor = If(AppState.CanCreateTestRequest, Color.FromArgb(232, 242, 255), Color.White)
                testRequestButton.ForeColor = If(AppState.CanCreateTestRequest, Color.FromArgb(31, 71, 136), Color.FromArgb(40, 40, 40))
            End If
        End If

        If mechanismQualityButton IsNot Nothing Then
            pendingMechanismQualityCount = summary.PendingMechanismQualityCount
            If pendingMechanismQualityCount > 0 Then
                mechanismQualityButton.Text = "Mekanizma Kontrol Formu (" & pendingMechanismQualityCount.ToString() & " bekleyen)"
                mechanismQualityButton.BackColor = Color.MistyRose
                mechanismQualityButton.ForeColor = Color.DarkRed
            Else
                mechanismQualityButton.Text = "Mekanizma Kontrol Formu"
                mechanismQualityButton.BackColor = Color.FromArgb(232, 242, 255)
                mechanismQualityButton.ForeColor = Color.FromArgb(31, 71, 136)
            End If
        End If

        AdjustDynamicMenuButtonParents()
        SendMainDashboardPayload(summary)
    End Sub

    Private Sub AdjustDynamicMenuButtonParents()
        Dim panels As New List(Of FlowLayoutPanel)()

        AddDynamicMenuButtonParent(panels, productionBindingButton)
        AddDynamicMenuButtonParent(panels, qualityTicketButton)
        AddDynamicMenuButtonParent(panels, productionIssueTicketButton)
        AddDynamicMenuButtonParent(panels, moldTicketButton)
        AddDynamicMenuButtonParent(panels, mechanismQualityButton)
        AddDynamicMenuButtonParent(panels, productAdminButton)
        AddDynamicMenuButtonParent(panels, testRequestButton)
        AddDynamicMenuButtonParent(panels, updateWizardButton)

        Dim signature = String.Join(
            "||",
            panels.Select(
                Function(panel)
                    Dim buttonTexts = String.Join(
                        "|",
                        panel.Controls.OfType(Of Button)().Select(Function(button) If(button.Text, "")))
                    Return panel.ClientSize.Width.ToString() & "x" & panel.ClientSize.Height.ToString() & ":" & buttonTexts
                End Function))

        If String.Equals(signature, lastDynamicMenuLayoutSignature, StringComparison.Ordinal) Then Return
        lastDynamicMenuLayoutSignature = signature

        For Each panel In panels
            AdjustMenuButtonWidths(panel)
        Next

        If categoryGrid IsNot Nothing AndAlso Not categoryGrid.IsDisposed Then
            ReflowCategoryGrid(categoryGrid)
        End If
    End Sub

    Private Sub AddDynamicMenuButtonParent(panels As List(Of FlowLayoutPanel), button As Button)
        If panels Is Nothing OrElse button Is Nothing Then Return

        Dim panel = TryCast(button.Parent, FlowLayoutPanel)
        If panel Is Nothing OrElse panels.Contains(panel) Then Return

        panels.Add(panel)
    End Sub

    Private Sub NotifyTicketSummaryChanges(summary As TicketSummary)
        If summary Is Nothing Then Return

        If Not ticketNotificationBaselineReady Then
            lastNotifiedActiveQualityCount = summary.ActiveQualityCount
            lastNotifiedActiveBindingCount = summary.ActiveBindingCount
            lastNotifiedPendingMechanismQualityCount = summary.PendingMechanismQualityCount
            ticketNotificationBaselineReady = True
            Return
        End If

        If AppState.CanOpenQualityTickets AndAlso summary.ActiveQualityCount > lastNotifiedActiveQualityCount Then
            AppNotificationService.ShowInfo(
                "Yeni kalite ticket oluşturuldu",
                "Aktif kalite ticket sayısı: " & summary.ActiveQualityCount.ToString())
        End If

        If AppState.CanOpenMechanismQualityControl AndAlso summary.PendingMechanismQualityCount > lastNotifiedPendingMechanismQualityCount Then
            AppNotificationService.ShowInfo(
                "Mekanizma kalite kontrol bekleyen kayıt var",
                "Bekleyen mekanizma kalite kontrol kaydı: " & summary.PendingMechanismQualityCount.ToString())
        End If

        If AppState.CanOpenProductionBinding AndAlso summary.ActiveBindingCount > lastNotifiedActiveBindingCount Then
            AppNotificationService.ShowInfo(
                "Kalıp bağlama kaydı bekliyor",
                "Devam eden kalıp bağlama kaydı: " & summary.ActiveBindingCount.ToString())
        End If

        lastNotifiedActiveQualityCount = summary.ActiveQualityCount
        lastNotifiedActiveBindingCount = summary.ActiveBindingCount
        lastNotifiedPendingMechanismQualityCount = summary.PendingMechanismQualityCount
    End Sub

    Private Sub SwitchUser(sender As Object, e As EventArgs)
        Dim oldUser = AppState.CurrentUserName
        Dim oldRole = AppState.CurrentRole
        Dim oldSessionId = AppState.CurrentSessionId

        Using login As New FrmLogin()
            If login.ShowDialog(Me) = DialogResult.OK Then
                ApplicationLifecycleService.EndSessionInBackground(oldSessionId)
                ApplicationInstanceService.UpdateNow()

                lastActivityUtc = DateTime.UtcNow
                lastSessionTouchUtc = DateTime.MinValue
                consecutiveSessionValidationFailureCount = 0
                pendingSessionValidationFailure = ""
                firstSessionValidationFailureUtc = DateTime.MinValue
                QueueSessionTouch()

                Dim newUser = AppState.CurrentUserName
                Dim newRole = AppState.CurrentRole
                Dim ignoredLogTask As Task = Task.Run(Sub()
                                                          AuditService.Log("USER_SWITCH", "", "", $"Önceki kullanıcı: {oldUser} / {oldRole}; Yeni kullanıcı: {newUser} / {newRole}")
                                                      End Sub)
                BuildMainScreen()
                MessageBox.Show("Kullanıcı değiştirildi." & Environment.NewLine &
                                $"Aktif kullanıcı: {AppState.CurrentUserName} / {AppState.CurrentRole}",
                                "Kullanıcı değiştirildi", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End If
        End Using
    End Sub

    Private Sub ChangeOwnPassword(sender As Object, e As EventArgs)
        Using changePasswordForm As New FrmChangePassword(False)
            changePasswordForm.ShowDialog(Me)
        End Using
    End Sub

    Private Sub ShowAuthorizedDialog(createForm As Func(Of Form), Optional afterClose As Action = Nothing)
        Try
            Using f As Form = createForm()
                f.ShowDialog(Me)
            End Using

            If afterClose IsNot Nothing Then afterClose.Invoke()
        Catch ex As UnauthorizedAccessException
            AuthorizationService.ShowDenied(ex, Me)
        End Try
    End Sub

    Private Sub OpenMeasurement(sender As Object, e As EventArgs)
        ShowAuthorizedDialog(Function() New FrmMeasurementEntry())
    End Sub

    Private Sub OpenMeasurementForScope(drawingScope As String)
        ShowAuthorizedDialog(Function() New FrmMeasurementEntry(initialDrawingScope:=drawingScope))
    End Sub

    Private Sub OpenHistory(sender As Object, e As EventArgs)
        ShowAuthorizedDialog(Function() New FrmMeasurementHistory())
    End Sub

    Private Sub OpenHistoryForScope(drawingScope As String)
        ShowAuthorizedDialog(Function() New FrmMeasurementHistory(drawingScope))
    End Sub

    Private Sub OpenProductionTicketEntry(sender As Object, e As EventArgs)
        ShowAuthorizedDialog(Function() New FrmProductionTicketEntry(), Sub() RefreshTicketButtons())
    End Sub

    Private Sub OpenMoldConnectionPlan(sender As Object, e As EventArgs)
        ShowAuthorizedDialog(Function() New FrmMoldConnectionPlan())
    End Sub

    Private Sub OpenProductionDrawingSearch(sender As Object, e As EventArgs)
        ShowAuthorizedDialog(Function() New FrmProductionDrawingSearch())
    End Sub

    Private Sub OpenMoldBindingDashboard(sender As Object, e As EventArgs)
        ShowAuthorizedDialog(Function() New FrmMoldBindingDashboard())
    End Sub

    Private Sub OpenQualityTickets(sender As Object, e As EventArgs)
        ShowAuthorizedDialog(Function() New FrmQualityTickets(), Sub() RefreshTicketButtons())
    End Sub

    Private Sub OpenPlasticShiftTracking(sender As Object, e As EventArgs)
        ShowAuthorizedDialog(Function() New FrmPlasticShiftTracking())
    End Sub

    Private Sub OpenMechanismShiftTracking(sender As Object, e As EventArgs)
        ShowAuthorizedDialog(Function() New FrmPlasticShiftTracking(True))
    End Sub

    Private Sub OpenPlasticShiftErrorReports(sender As Object, e As EventArgs)
        ShowAuthorizedDialog(Function() New FrmPlasticShiftErrorReports())
    End Sub

    Private Sub OpenMechanismQualityControl(sender As Object, e As EventArgs)
        ShowAuthorizedDialog(Function() New FrmMechanismQualityControl())
    End Sub

    Private Sub OpenInoTracking(sender As Object, e As EventArgs)
        ShowAuthorizedDialog(Function() InoIntegrationService.CreateForm())
    End Sub

    Private Sub OpenQualityToProductionTickets(sender As Object, e As EventArgs)
        ShowAuthorizedDialog(Function() New FrmQualityToProductionTickets(), Sub() RefreshTicketButtons())
    End Sub

    Private Sub OpenMoldTickets(sender As Object, e As EventArgs)
        ShowAuthorizedDialog(Function() New FrmMoldTicketManagement(), Sub() RefreshTicketButtons())
    End Sub

    Private Sub OpenNewMoldCommissioning(sender As Object, e As EventArgs)
        ShowAuthorizedDialog(Function() New FrmNewMoldCommissionings())
    End Sub

    Private Sub OpenProducts(sender As Object, e As EventArgs)
        ShowAuthorizedDialog(Function() New FrmProductAdmin())
    End Sub

    Private Sub OpenControlPoints(sender As Object, e As EventArgs)
        ShowAuthorizedDialog(Function() New FrmControlPointAdmin())
    End Sub

    Private Sub OpenSpcDashboard(sender As Object, e As EventArgs)
        ShowAuthorizedDialog(Function() New FrmSpcDashboard())
    End Sub

    Private Sub OpenMsaDashboard(sender As Object, e As EventArgs)
        ShowAuthorizedDialog(Function() New FrmMsaDashboard())
    End Sub

    Private Sub OpenScrapDashboard(sender As Object, e As EventArgs)
        ShowAuthorizedDialog(Function() New FrmScrapDashboard())
    End Sub

    Private Sub OpenReworkDashboard(sender As Object, e As EventArgs)
        ShowAuthorizedDialog(Function() New FrmReworkDashboard())
    End Sub

    Private Sub OpenTestRequests(sender As Object, e As EventArgs)
        ShowAuthorizedDialog(Function() New FrmTestRequests(), Sub() RefreshTicketButtons())
    End Sub

    Private Sub OpenPackageMeterControls(sender As Object, e As EventArgs)
        ShowAuthorizedDialog(Function() New FrmPackageMeterControls())
    End Sub

    Private Sub OpenUsers(sender As Object, e As EventArgs)
        ShowAuthorizedDialog(Function() New FrmUserManagement())
    End Sub

    Private Sub OpenAudit(sender As Object, e As EventArgs)
        ShowAuthorizedDialog(Function() New FrmAuditLog())
    End Sub

    Private Sub OpenDataHealth(sender As Object, e As EventArgs)
        ShowAuthorizedDialog(Function() New FrmDataHealth())
    End Sub

    Private Sub OpenPermissionMatrix(sender As Object, e As EventArgs)
        ShowAuthorizedDialog(Function() New FrmPermissionMatrix())
    End Sub

    Private Sub OpenUpdateWizard(sender As Object, e As EventArgs)
        ShowAuthorizedDialog(Function() New FrmUpdateWizard(availableUpdatePackagePath),
                             Sub() QueueUpdateAvailabilityCheck(True))
    End Sub

    Private Class ActivityMessageFilter
        Implements IMessageFilter

        Private ReadOnly onActivity As Action

        Public Sub New(onActivity As Action)
            Me.onActivity = onActivity
        End Sub

        Public Function PreFilterMessage(ByRef m As Message) As Boolean Implements IMessageFilter.PreFilterMessage
            Dim msg = m.Msg

            Dim isKeyboardActivity = (msg >= &H100 AndAlso msg <= &H109)
            Dim isMouseActivity = (msg >= &H200 AndAlso msg <= &H20E)

            If isKeyboardActivity OrElse isMouseActivity Then
                If onActivity IsNot Nothing Then onActivity.Invoke()
            End If

            Return False
        End Function
    End Class
End Class
