Imports System.Linq
Imports System.Security.Cryptography
Imports System.Text

Public NotInheritable Class UserService
    Private Sub New()
    End Sub

    Private Const Iterations As Integer = 100000

    Public Shared Sub EnsureDefaultAdmin()
        MigrateUserPasswordStorage()

        CsvUtil.UpdateRowsLocked(
            AppPaths.UsersCsv,
            DataService.UserHeaders,
            Sub(rows)
                Dim admin = rows.FirstOrDefault(
                    Function(row) String.Equals(
                        DataService.GetValue(row, "Username"),
                        "admin",
                        StringComparison.OrdinalIgnoreCase))

                If admin Is Nothing Then
                    Dim initialPassword = GenerateTemporaryPassword()
                    Dim salt = CreateSalt()
                    rows.Add(New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                        {"Username", "admin"},
                        {"PasswordHash", HashPassword(initialPassword, salt)},
                        {"PasswordSalt", Convert.ToBase64String(salt)},
                        {"Role", AppState.RoleAdmin},
                        {"IsActive", "YES"},
                        {"ShowOnLogin", "NO"},
                        {"IsPermissionTestAccount", "NO"},
                        {"MustChangePassword", "YES"},
                        {"PasswordChangedAt", ""},
                        {"CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")},
                        {"LastLoginAt", "NEVER"}
                    })
                ElseIf String.IsNullOrWhiteSpace(DataService.GetValue(admin, "PasswordChangedAt")) AndAlso
                       VerifyPassword(admin, LegacyDefaultAdminPassword()) Then
                    ' Eski sabit Admin parolası artık girişte gösterilmez. Hesap ilk
                    ' kurulum durumuna yalnızca bir kez alınır. PasswordChangedAt doluysa
                    ' kullanıcı bu parolayı bilinçli seçmiş demektir ve bir daha sıfırlanmaz.
                    Dim replacementPassword = GenerateTemporaryPassword()
                    Dim salt = CreateSalt()
                    admin("PasswordHash") = HashPassword(replacementPassword, salt)
                    admin("PasswordSalt") = Convert.ToBase64String(salt)
                    admin("Role") = AppState.RoleAdmin
                    admin("IsActive") = "YES"
                    admin("ShowOnLogin") = "NO"
                    admin("IsPermissionTestAccount") = "NO"
                    admin("MustChangePassword") = "YES"
                    admin("PasswordChangedAt") = ""
                End If
            End Sub)
        UserStoreRecoveryService.CreateBackup()
    End Sub

    Public Shared Function NeedsInitialAdminPasswordSetup() As Boolean
        MigrateUserPasswordStorage()

        Dim admin = CsvUtil.ReadRows(AppPaths.UsersCsv).FirstOrDefault(
            Function(row) String.Equals(
                DataService.GetValue(row, "Username"),
                "admin",
                StringComparison.OrdinalIgnoreCase) AndAlso
                          String.Equals(
                              AppState.NormalizeRole(DataService.GetValue(row, "Role")),
                              AppState.RoleAdmin,
                              StringComparison.OrdinalIgnoreCase))

        Return admin IsNot Nothing AndAlso
               String.Equals(DataService.GetValue(admin, "MustChangePassword"), "YES", StringComparison.OrdinalIgnoreCase) AndAlso
               String.IsNullOrWhiteSpace(DataService.GetValue(admin, "PasswordChangedAt"))
    End Function

    Public Shared Sub SetInitialAdminPassword(newPassword As String)
        ValidateNewPassword(newPassword)

        CsvUtil.UpdateRowsLocked(
            AppPaths.UsersCsv,
            DataService.UserHeaders,
            Sub(rows)
                Dim admin = rows.FirstOrDefault(
                    Function(row) String.Equals(
                        DataService.GetValue(row, "Username"),
                        "admin",
                        StringComparison.OrdinalIgnoreCase) AndAlso
                                  String.Equals(
                                      AppState.NormalizeRole(DataService.GetValue(row, "Role")),
                                      AppState.RoleAdmin,
                                      StringComparison.OrdinalIgnoreCase))

                If admin Is Nothing Then Throw New InvalidOperationException("Admin hesabı bulunamadı.")

                Dim setupPending =
                    String.Equals(DataService.GetValue(admin, "MustChangePassword"), "YES", StringComparison.OrdinalIgnoreCase) AndAlso
                    String.IsNullOrWhiteSpace(DataService.GetValue(admin, "PasswordChangedAt"))
                If Not setupPending Then
                    Throw New InvalidOperationException("Admin parolası daha önce belirlenmiş. Değişiklik için Kullanıcı Yönetimi ekranını kullanın.")
                End If

                Dim salt = CreateSalt()
                admin("PasswordHash") = HashPassword(newPassword, salt)
                admin("PasswordSalt") = Convert.ToBase64String(salt)
                admin("Role") = AppState.RoleAdmin
                admin("IsActive") = "YES"
                admin("MustChangePassword") = "NO"
                admin("PasswordChangedAt") = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            End Sub)

        AuditService.Log("INITIAL_ADMIN_PASSWORD_SET", "", "", "İlk Admin parolası kullanıcı tarafından belirlendi.")
        UserStoreRecoveryService.CreateBackup()
    End Sub

    Public Shared Sub MigrateUserPasswordStorage()
        CsvUtil.UpdateRowsLockedIfChanged(
            AppPaths.UsersCsv,
            DataService.UserHeaders,
            Function(rows)
                If rows.Count = 0 Then Return False

                Dim needsRewrite As Boolean = False
                Dim cleanRows As New List(Of Dictionary(Of String, String))()

                For Each r In rows
                    Dim plainPassword As String = DataService.GetValue(r, "PasswordPlain")
                    Dim passwordHash As String = DataService.GetValue(r, "PasswordHash")
                    Dim passwordSalt As String = DataService.GetValue(r, "PasswordSalt")

                    ' Eski surumlerde PasswordPlain ve PasswordProtected alanlari vardi.
                    ' Duz sifre varsa tek seferlik hash'e cevrilir; geri cozulebilir alanlar dosyadan dusurulur.
                    If r.ContainsKey("PasswordPlain") Then needsRewrite = True

                    ' Cok eski / bozuk kayit hash-salt icermiyorsa, elde duz sifre varsa hash'e cevir.
                    If (String.IsNullOrWhiteSpace(passwordHash) OrElse String.IsNullOrWhiteSpace(passwordSalt)) AndAlso
                       Not String.IsNullOrWhiteSpace(plainPassword) Then
                        Dim salt = CreateSalt()
                        passwordHash = HashPassword(plainPassword, salt)
                        passwordSalt = Convert.ToBase64String(salt)
                        needsRewrite = True
                    End If

                    If r.ContainsKey("PasswordProtected") Then needsRewrite = True

                    Dim mustChangePassword = DataService.GetValue(r, "MustChangePassword").Trim()
                    If mustChangePassword = "" Then
                        mustChangePassword = "NO"
                        needsRewrite = True
                    End If

                    Dim normalizedRole = AppState.NormalizeRole(DataService.GetValue(r, "Role"))
                    Dim showOnLogin = DataService.GetValue(r, "ShowOnLogin").Trim().ToUpperInvariant()
                    If showOnLogin <> "YES" AndAlso showOnLogin <> "NO" Then
                        showOnLogin = If(String.Equals(normalizedRole, AppState.RoleAdmin, StringComparison.OrdinalIgnoreCase), "NO", "YES")
                        needsRewrite = True
                    End If

                    Dim isPermissionTestAccount = DataService.GetValue(r, "IsPermissionTestAccount").Trim().ToUpperInvariant()
                    If isPermissionTestAccount <> "YES" AndAlso isPermissionTestAccount <> "NO" Then
                        isPermissionTestAccount = "NO"
                        needsRewrite = True
                    End If

                    If (String.Equals(normalizedRole, AppState.RoleAdmin, StringComparison.OrdinalIgnoreCase) OrElse
                        isPermissionTestAccount = "YES") AndAlso showOnLogin <> "NO" Then
                        showOnLogin = "NO"
                        needsRewrite = True
                    End If

                    cleanRows.Add(New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                        {"Username", DataService.GetValue(r, "Username")},
                        {"PasswordHash", passwordHash},
                        {"PasswordSalt", passwordSalt},
                        {"Role", normalizedRole},
                        {"IsActive", If(String.IsNullOrWhiteSpace(DataService.GetValue(r, "IsActive")), "YES", DataService.GetValue(r, "IsActive"))},
                        {"ShowOnLogin", showOnLogin},
                        {"IsPermissionTestAccount", isPermissionTestAccount},
                        {"MustChangePassword", mustChangePassword},
                        {"PasswordChangedAt", DataService.GetValue(r, "PasswordChangedAt")},
                        {"CreatedAt", DataService.GetValue(r, "CreatedAt")},
                        {"LastLoginAt", DataService.GetValue(r, "LastLoginAt")}
                    })
                Next

                If needsRewrite Then
                    rows.Clear()
                    rows.AddRange(cleanRows)
                End If
                Return needsRewrite
            End Function)
    End Sub

    Public Shared Function Authenticate(username As String, password As String) As Boolean
        username = If(username, "").Trim()
        If username = "" Then Return False

        Dim authenticated As Boolean = False
        Dim normalizedUserName As String = ""
        Dim normalizedRole As String = ""
        Dim mustChangePassword As Boolean = False
        Dim isPermissionTestAccount As Boolean = False
        Dim sessionId As String = ""
        Dim loginAtText As String = ""
        Dim computerName = Environment.MachineName
        Dim sessionsReplacedByLogin As New List(Of Dictionary(Of String, String))()

        CsvUtil.UpdateTwoFilesLocked(
            AppPaths.UsersCsv,
            DataService.UserHeaders,
            AppPaths.ActiveSessionsCsv,
            DataService.ActiveSessionHeaders,
            Sub(userRows, sessionRows)
                Dim user = userRows.FirstOrDefault(
                    Function(row) String.Equals(
                        DataService.GetValue(row, "Username"),
                        username,
                        StringComparison.OrdinalIgnoreCase))
                If user Is Nothing Then Return
                If Not String.Equals(DataService.GetValue(user, "IsActive"), "YES", StringComparison.OrdinalIgnoreCase) Then Return
                If Not VerifyPassword(user, password) Then Return

                normalizedUserName = DataService.GetValue(user, "Username")
                normalizedRole = AppState.NormalizeRole(DataService.GetValue(user, "Role"))
                mustChangePassword =
                    String.Equals(DataService.GetValue(user, "MustChangePassword"), "YES", StringComparison.OrdinalIgnoreCase)
                isPermissionTestAccount =
                    String.Equals(DataService.GetValue(user, "IsPermissionTestAccount"), "YES", StringComparison.OrdinalIgnoreCase)

                Dim otherComputerSession = sessionRows.FirstOrDefault(
                    Function(row) String.Equals(
                        DataService.GetValue(row, "Username"),
                        normalizedUserName,
                        StringComparison.OrdinalIgnoreCase) AndAlso
                                   Not String.Equals(
                                       DataService.GetValue(row, "ComputerName"),
                                       computerName,
                                       StringComparison.OrdinalIgnoreCase))
                If otherComputerSession IsNot Nothing Then
                    Throw New InvalidOperationException(
                        "Bu kullanıcı şu anda başka bir bilgisayarda açık: " &
                        DataService.GetValue(otherComputerSession, "ComputerName") & Environment.NewLine &
                        "Aynı kullanıcı farklı bilgisayarlardan aynı anda giriş yapamaz.")
                End If

                Dim sameComputerSessions = sessionRows.
                    Where(
                        Function(row) String.Equals(
                            DataService.GetValue(row, "Username"),
                            normalizedUserName,
                            StringComparison.OrdinalIgnoreCase) AndAlso
                                       String.Equals(
                                           DataService.GetValue(row, "ComputerName"),
                                           computerName,
                                           StringComparison.OrdinalIgnoreCase)).
                    Select(Function(row) New Dictionary(Of String, String)(row, StringComparer.OrdinalIgnoreCase)).
                    ToList()
                sessionsReplacedByLogin.AddRange(sameComputerSessions)

                sessionRows.RemoveAll(
                    Function(row) String.Equals(
                        DataService.GetValue(row, "Username"),
                        normalizedUserName,
                        StringComparison.OrdinalIgnoreCase) AndAlso
                                   String.Equals(
                                       DataService.GetValue(row, "ComputerName"),
                                       computerName,
                                       StringComparison.OrdinalIgnoreCase))

                sessionId = Guid.NewGuid().ToString("N")
                Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                loginAtText = nowText
                user("LastLoginAt") = nowText
                sessionRows.Add(New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                    {"SessionId", sessionId},
                    {"Username", normalizedUserName},
                    {"ComputerName", computerName},
                    {"LoginAt", nowText},
                    {"LastSeen", nowText}
                })
                authenticated = True
            End Sub)

        If Not authenticated Then Return False

        AppState.CurrentUserName = normalizedUserName
        AppState.CurrentRole = normalizedRole
        AppState.CurrentSessionId = sessionId
        AppState.CurrentUserMustChangePassword = mustChangePassword
        AppState.CurrentUserIsPermissionTestAccount = isPermissionTestAccount

        AuditService.Log("LOGIN_OK", "", "", "Kullanıcı giriş yaptı. SessionId=" & sessionId)
        For Each replacedSession In sessionsReplacedByLogin
            DataService.RequestUserSessionEnd(
                DataService.GetValue(replacedSession, "SessionId"),
                DataService.GetValue(replacedSession, "Username"),
                DataService.GetValue(replacedSession, "ComputerName"),
                normalizedUserName,
                "SAME_USER_RELOGIN_SAME_COMPUTER")
        Next

        Return True
    End Function

    Public Shared Function AuthenticateFast(username As String, password As String) As Boolean
        username = If(username, "").Trim()
        If username = "" Then Return False

        Dim userRows = CsvUtil.ReadRows(AppPaths.UsersCsv)
        Dim user = userRows.FirstOrDefault(
            Function(row) String.Equals(
                DataService.GetValue(row, "Username"),
                username,
                StringComparison.OrdinalIgnoreCase))
        If user Is Nothing Then Return False
        If Not String.Equals(DataService.GetValue(user, "IsActive"), "YES", StringComparison.OrdinalIgnoreCase) Then Return False
        If Not VerifyPassword(user, password) Then Return False

        Dim normalizedUserName = DataService.GetValue(user, "Username")
        Dim normalizedRole = AppState.NormalizeRole(DataService.GetValue(user, "Role"))
        Dim mustChangePassword =
            String.Equals(DataService.GetValue(user, "MustChangePassword"), "YES", StringComparison.OrdinalIgnoreCase)
        Dim isPermissionTestAccount =
            String.Equals(DataService.GetValue(user, "IsPermissionTestAccount"), "YES", StringComparison.OrdinalIgnoreCase)
        Dim sessionId = Guid.NewGuid().ToString("N")
        Dim loginAtText As String = ""
        Dim computerName = Environment.MachineName
        Dim sessionsReplacedByLogin As New List(Of Dictionary(Of String, String))()

        CsvUtil.UpdateRowsLocked(
            AppPaths.ActiveSessionsCsv,
            DataService.ActiveSessionHeaders,
            Sub(sessionRows)
                sessionRows.RemoveAll(Function(row) IsLoginSessionExpired(row))

                Dim otherComputerSession = sessionRows.FirstOrDefault(
                    Function(row) String.Equals(
                        DataService.GetValue(row, "Username"),
                        normalizedUserName,
                        StringComparison.OrdinalIgnoreCase) AndAlso
                                   Not String.Equals(
                                       DataService.GetValue(row, "ComputerName"),
                                       computerName,
                                       StringComparison.OrdinalIgnoreCase))
                If otherComputerSession IsNot Nothing Then
                    Throw New InvalidOperationException(
                        "Bu kullanıcı şu anda başka bir bilgisayarda açık: " &
                        DataService.GetValue(otherComputerSession, "ComputerName") & Environment.NewLine &
                        "Aynı kullanıcı farklı bilgisayarlardan aynı anda giriş yapamaz.")
                End If

                Dim sameComputerSessions = sessionRows.
                    Where(
                        Function(row) String.Equals(
                            DataService.GetValue(row, "Username"),
                            normalizedUserName,
                            StringComparison.OrdinalIgnoreCase) AndAlso
                                       String.Equals(
                                           DataService.GetValue(row, "ComputerName"),
                                           computerName,
                                           StringComparison.OrdinalIgnoreCase)).
                    Select(Function(row) New Dictionary(Of String, String)(row, StringComparer.OrdinalIgnoreCase)).
                    ToList()
                sessionsReplacedByLogin.AddRange(sameComputerSessions)

                sessionRows.RemoveAll(
                    Function(row) String.Equals(
                        DataService.GetValue(row, "Username"),
                        normalizedUserName,
                        StringComparison.OrdinalIgnoreCase) AndAlso
                                   String.Equals(
                                       DataService.GetValue(row, "ComputerName"),
                                       computerName,
                                       StringComparison.OrdinalIgnoreCase))

                Dim nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                loginAtText = nowText
                sessionRows.Add(New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                    {"SessionId", sessionId},
                    {"Username", normalizedUserName},
                    {"ComputerName", computerName},
                    {"LoginAt", nowText},
                    {"LastSeen", nowText}
                })
            End Sub)

        AppState.CurrentUserName = normalizedUserName
        AppState.CurrentRole = normalizedRole
        AppState.CurrentSessionId = sessionId
        AppState.CurrentUserMustChangePassword = mustChangePassword
        AppState.CurrentUserIsPermissionTestAccount = isPermissionTestAccount

        AuditService.Log("LOGIN_OK", "", "", "Kullanıcı giriş yaptı. SessionId=" & sessionId)
        QueueLastLoginUpdate(normalizedUserName, loginAtText)
        QueueReplacedSessionEndRequests(sessionsReplacedByLogin, normalizedUserName)

        Return True
    End Function

    Private Shared Function IsLoginSessionExpired(row As Dictionary(Of String, String)) As Boolean
        Dim lastSeen As DateTime
        If Not DateTime.TryParse(DataService.GetValue(row, "LastSeen"), lastSeen) Then Return True
        Return lastSeen < DateTime.Now.AddMinutes(-10)
    End Function

    Private Shared Sub QueueReplacedSessionEndRequests(sessions As List(Of Dictionary(Of String, String)), requestedBy As String)
        If sessions Is Nothing OrElse sessions.Count = 0 Then Return

        Threading.Tasks.Task.Run(
            Sub()
                For Each replacedSession In sessions
                    Try
                        DataService.RequestUserSessionEnd(
                            DataService.GetValue(replacedSession, "SessionId"),
                            DataService.GetValue(replacedSession, "Username"),
                            DataService.GetValue(replacedSession, "ComputerName"),
                            requestedBy,
                            "SAME_USER_RELOGIN_SAME_COMPUTER")
                    Catch ex As Exception
                        ErrorLogService.Log("UserService.QueueReplacedSessionEndRequests", ex)
                    End Try
                Next
            End Sub)
    End Sub

    Public Shared Function GetUsers() As List(Of Dictionary(Of String, String))
        MigrateUserPasswordStorage()
        Return CsvUtil.ReadRows(AppPaths.UsersCsv)
    End Function

    Public Shared Function GetUsersWithLastLogin() As List(Of Dictionary(Of String, String))
        Dim users = GetUsers()
        If users.All(Function(row) DataService.GetValue(row, "LastLoginAt").Trim() <> "") Then Return users

        Dim latestByUser As New Dictionary(Of String, DateTime)(StringComparer.OrdinalIgnoreCase)
        For Each auditRow In CsvUtil.ReadRows(AppPaths.AuditLogCsv)
            If Not String.Equals(DataService.GetValue(auditRow, "Action"), "LOGIN_OK", StringComparison.OrdinalIgnoreCase) Then Continue For
            Dim username = DataService.GetValue(auditRow, "UserName").Trim()
            Dim loginAt As DateTime
            If username = "" OrElse Not DateTime.TryParse(DataService.GetValue(auditRow, "DateTime"), loginAt) Then Continue For

            Dim current As DateTime
            If Not latestByUser.TryGetValue(username, current) OrElse loginAt > current Then latestByUser(username) = loginAt
        Next

        CsvUtil.UpdateRowsLocked(
            AppPaths.UsersCsv,
            DataService.UserHeaders,
            Sub(rows)
                For Each row In rows
                    If DataService.GetValue(row, "LastLoginAt").Trim() <> "" Then Continue For
                    Dim username = DataService.GetValue(row, "Username").Trim()
                    Dim lastLogin As DateTime
                    row("LastLoginAt") = If(latestByUser.TryGetValue(username, lastLogin),
                                            lastLogin.ToString("yyyy-MM-dd HH:mm:ss"),
                                            "NEVER")
                Next
            End Sub)

        Return CsvUtil.ReadRows(AppPaths.UsersCsv)
    End Function

    Private Shared Sub QueueLastLoginUpdate(username As String, loginAtText As String)
        username = If(username, "").Trim()
        loginAtText = If(loginAtText, "").Trim()
        If username = "" OrElse loginAtText = "" Then Return

        Threading.Tasks.Task.Run(
            Sub()
                Try
                    Dim candidate As DateTime
                    If Not DateTime.TryParse(loginAtText, candidate) Then Return
                    CsvUtil.UpdateRowsLocked(
                        AppPaths.UsersCsv,
                        DataService.UserHeaders,
                        Sub(rows)
                            Dim user = rows.FirstOrDefault(
                                Function(row) String.Equals(DataService.GetValue(row, "Username"), username, StringComparison.OrdinalIgnoreCase))
                            If user Is Nothing Then Return

                            Dim current As DateTime
                            If DateTime.TryParse(DataService.GetValue(user, "LastLoginAt"), current) AndAlso current > candidate Then Return
                            user("LastLoginAt") = candidate.ToString("yyyy-MM-dd HH:mm:ss")
                        End Sub)
                Catch ex As Exception
                    ErrorLogService.Log("UserService.QueueLastLoginUpdate", ex)
                End Try
            End Sub)
    End Sub

    Public Shared Function IsUserAuthorizationValid(username As String, expectedRole As String) As Boolean
        username = If(username, "").Trim()
        expectedRole = AppState.NormalizeRole(expectedRole)
        If username = "" OrElse Not AppState.IsValidRole(expectedRole) Then Return False

        Dim user = CsvUtil.ReadRowsLocked(AppPaths.UsersCsv).
            FirstOrDefault(
                Function(row) String.Equals(
                    DataService.GetValue(row, "Username"),
                    username,
                    StringComparison.OrdinalIgnoreCase))
        If user Is Nothing Then Return False
        If Not String.Equals(DataService.GetValue(user, "IsActive"), "YES", StringComparison.OrdinalIgnoreCase) Then Return False

        Return String.Equals(
            AppState.NormalizeRole(DataService.GetValue(user, "Role")),
            expectedRole,
            StringComparison.OrdinalIgnoreCase)
    End Function

    Public Shared Sub SaveUser(username As String,
                               password As String,
                               role As String,
                               isActive As String,
                               showOnLogin As String,
                               isPermissionTestAccount As String)
        username = username.Trim()
        role = AppState.NormalizeRole(role)
        If username = "" Then Throw New ArgumentException("Kullanıcı adı boş olamaz.")
        If Not AppState.IsValidRole(role) Then Throw New ArgumentException("Geçerli bir kullanıcı rolü seçilmelidir.")

        isActive = If(String.Equals(isActive, "YES", StringComparison.OrdinalIgnoreCase), "YES", "NO")
        showOnLogin = If(String.Equals(showOnLogin, "YES", StringComparison.OrdinalIgnoreCase), "YES", "NO")
        isPermissionTestAccount = If(String.Equals(isPermissionTestAccount, "YES", StringComparison.OrdinalIgnoreCase), "YES", "NO")
        If String.Equals(role, AppState.RoleAdmin, StringComparison.OrdinalIgnoreCase) OrElse
           isPermissionTestAccount = "YES" Then
            showOnLogin = "NO"
        End If

        MigrateUserPasswordStorage()
        UserStoreRecoveryService.CreateBackup()

        CsvUtil.UpdateTwoFilesLocked(
            AppPaths.UsersCsv,
            DataService.UserHeaders,
            AppPaths.ActiveSessionsCsv,
            DataService.ActiveSessionHeaders,
            Sub(userRows, sessionRows)
                Dim existing = userRows.FirstOrDefault(
                    Function(r) String.Equals(DataService.GetValue(r, "Username"), username, StringComparison.OrdinalIgnoreCase))
                If existing Is Nothing Then
                    If password.Trim() = "" Then Throw New ArgumentException("Yeni kullanıcı için şifre zorunludur.")
                    Dim salt = CreateSalt()
                    userRows.Add(New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                        {"Username", username},
                        {"PasswordHash", HashPassword(password, salt)},
                        {"PasswordSalt", Convert.ToBase64String(salt)},
                        {"Role", role},
                        {"IsActive", isActive},
                        {"ShowOnLogin", showOnLogin},
                        {"IsPermissionTestAccount", isPermissionTestAccount},
                        {"MustChangePassword", "YES"},
                        {"PasswordChangedAt", ""},
                        {"CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")},
                        {"LastLoginAt", "NEVER"}
                    })
                Else
                    Dim existingRole = AppState.NormalizeRole(DataService.GetValue(existing, "Role"))
                    Dim roleChanged = Not String.Equals(existingRole, role, StringComparison.OrdinalIgnoreCase)
                    Dim testAccountChanged = Not String.Equals(
                        DataService.GetValue(existing, "IsPermissionTestAccount"),
                        isPermissionTestAccount,
                        StringComparison.OrdinalIgnoreCase)
                    Dim remainsActive = String.Equals(isActive, "YES", StringComparison.OrdinalIgnoreCase)
                    Dim remainsAdmin = String.Equals(role, AppState.RoleAdmin, StringComparison.OrdinalIgnoreCase)
                    If String.Equals(existingRole, AppState.RoleAdmin, StringComparison.OrdinalIgnoreCase) AndAlso
                       HasActiveSession(sessionRows, DataService.GetValue(existing, "Username")) AndAlso
                       (Not remainsActive OrElse Not remainsAdmin) Then
                        Throw New InvalidOperationException(
                            "Açık oturumu bulunan Admin hesabı pasif yapılamaz veya Admin rolünden çıkarılamaz.")
                    End If

                    existing("Role") = role
                    existing("IsActive") = isActive
                    existing("ShowOnLogin") = showOnLogin
                    existing("IsPermissionTestAccount") = isPermissionTestAccount
                    If password.Trim() <> "" Then
                        Dim salt = CreateSalt()
                        existing("PasswordHash") = HashPassword(password, salt)
                        existing("PasswordSalt") = Convert.ToBase64String(salt)
                        existing("MustChangePassword") = "NO"
                        existing("PasswordChangedAt") = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    End If

                    If roleChanged OrElse testAccountChanged OrElse Not remainsActive Then
                        sessionRows.RemoveAll(
                            Function(session) String.Equals(
                                DataService.GetValue(session, "Username"),
                                username,
                                StringComparison.OrdinalIgnoreCase))
                    End If
                End If
            End Sub)
        AuditService.Log(
            "USER_SAVE",
            "",
            "",
            "Kullanıcı kaydedildi: " & username &
            "; GirişteGöster=" & showOnLogin &
            "; YetkiTestHesabı=" & isPermissionTestAccount)
        UserStoreRecoveryService.CreateBackup()
    End Sub

    Public Shared Sub SetPasswordByAdmin(username As String, newPassword As String)
        If Not AppState.IsAdmin Then Throw New UnauthorizedAccessException("Kullanıcı parolalarını yalnızca Admin belirleyebilir.")

        username = If(username, "").Trim()
        If username = "" Then Throw New ArgumentException("Parolası belirlenecek kullanıcı seçilmelidir.")
        ValidateNewPassword(newPassword)

        MigrateUserPasswordStorage()
        UserStoreRecoveryService.CreateBackup()

        Dim salt = CreateSalt()
        Dim normalizedUserName As String = ""
        CsvUtil.UpdateRowsLocked(
            AppPaths.UsersCsv,
            DataService.UserHeaders,
            Sub(rows)
                Dim existing = rows.FirstOrDefault(
                    Function(r) String.Equals(DataService.GetValue(r, "Username"), username, StringComparison.OrdinalIgnoreCase))
                If existing Is Nothing Then Throw New InvalidOperationException("Kullanıcı bulunamadı: " & username)

                existing("PasswordHash") = HashPassword(newPassword, salt)
                existing("PasswordSalt") = Convert.ToBase64String(salt)
                existing("MustChangePassword") = "NO"
                existing("PasswordChangedAt") = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                normalizedUserName = DataService.GetValue(existing, "Username")
            End Sub)

        AuditService.Log("USER_PASSWORD_SET_BY_ADMIN", "", "", "Kullanıcı parolası Admin tarafından belirlendi: " & normalizedUserName)
        UserStoreRecoveryService.CreateBackup()
    End Sub

    Public Shared Sub ChangeOwnPassword(currentPassword As String, newPassword As String)
        Dim username = If(AppState.CurrentUserName, "").Trim()
        If username = "" Then Throw New UnauthorizedAccessException("Parola değiştirmek için açık bir kullanıcı oturumu gereklidir.")

        ValidateNewPassword(newPassword)
        UserStoreRecoveryService.CreateBackup()

        CsvUtil.UpdateRowsLocked(
            AppPaths.UsersCsv,
            DataService.UserHeaders,
            Sub(rows)
                Dim existing = rows.FirstOrDefault(
                    Function(row) String.Equals(
                        DataService.GetValue(row, "Username"),
                        username,
                        StringComparison.OrdinalIgnoreCase))
                If existing Is Nothing Then Throw New InvalidOperationException("Açık kullanıcı hesabı bulunamadı: " & username)
                If Not String.Equals(DataService.GetValue(existing, "IsActive"), "YES", StringComparison.OrdinalIgnoreCase) Then
                    Throw New UnauthorizedAccessException("Kullanıcı hesabı aktif değil.")
                End If
                If Not VerifyPassword(existing, currentPassword) Then
                    Throw New UnauthorizedAccessException("Mevcut parola yanlış.")
                End If
                If VerifyPassword(existing, newPassword) Then
                    Throw New ArgumentException("Yeni parola mevcut paroladan farklı olmalıdır.")
                End If

                Dim salt = CreateSalt()
                existing("PasswordHash") = HashPassword(newPassword, salt)
                existing("PasswordSalt") = Convert.ToBase64String(salt)
                existing("MustChangePassword") = "NO"
                existing("PasswordChangedAt") = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            End Sub)

        AppState.CurrentUserMustChangePassword = False
        AuditService.Log("USER_PASSWORD_CHANGE", "", "", "Kullanıcı kendi parolasını değiştirdi: " & username)
        UserStoreRecoveryService.CreateBackup()
    End Sub

    Public Shared Function MustCurrentUserChangePassword() As Boolean
        Dim username = If(AppState.CurrentUserName, "").Trim()
        If username = "" Then Return False

        Dim user = CsvUtil.ReadRows(AppPaths.UsersCsv).
            FirstOrDefault(
                Function(row) String.Equals(
                    DataService.GetValue(row, "Username"),
                    username,
                    StringComparison.OrdinalIgnoreCase))
        If user Is Nothing Then Return False

        Return String.Equals(
            DataService.GetValue(user, "MustChangePassword"),
            "YES",
            StringComparison.OrdinalIgnoreCase)
    End Function

    Public Shared Sub DeleteUser(username As String)
        If Not AppState.IsAdmin Then Throw New UnauthorizedAccessException("Kullanıcıları yalnızca Admin silebilir.")

        username = If(username, "").Trim()
        If username = "" Then Throw New ArgumentException("Silinecek kullanıcı seçilmelidir.")
        If String.Equals(username, AppState.CurrentUserName, StringComparison.OrdinalIgnoreCase) Then
            Throw New InvalidOperationException("Açık olan ADMIN hesabı kendi kendisini silemez.")
        End If

        MigrateUserPasswordStorage()
        UserStoreRecoveryService.CreateBackup()

        Dim normalizedUserName As String = ""
        Dim normalizedRole As String = ""
        CsvUtil.UpdateTwoFilesLocked(
            AppPaths.UsersCsv,
            DataService.UserHeaders,
            AppPaths.ActiveSessionsCsv,
            DataService.ActiveSessionHeaders,
            Sub(userRows, sessionRows)
                Dim existing = userRows.FirstOrDefault(
                    Function(r) String.Equals(DataService.GetValue(r, "Username"), username, StringComparison.OrdinalIgnoreCase))
                If existing Is Nothing Then Throw New InvalidOperationException("Kullanıcı bulunamadı: " & username)

                normalizedUserName = DataService.GetValue(existing, "Username")
                normalizedRole = AppState.NormalizeRole(DataService.GetValue(existing, "Role"))
                If String.Equals(normalizedRole, AppState.RoleAdmin, StringComparison.OrdinalIgnoreCase) AndAlso
                   HasActiveSession(sessionRows, normalizedUserName) Then
                    Throw New InvalidOperationException(
                        "Açık oturumu bulunan Admin hesabı silinemez. Admin oturumları başka bir Admin tarafından kapatılamaz.")
                End If

                userRows.RemoveAll(
                    Function(r) String.Equals(DataService.GetValue(r, "Username"), username, StringComparison.OrdinalIgnoreCase))
                sessionRows.RemoveAll(
                    Function(r) String.Equals(DataService.GetValue(r, "Username"), username, StringComparison.OrdinalIgnoreCase))
            End Sub)

        AuditService.Log("USER_DELETE", "", "", "Kullanıcı silindi: " & normalizedUserName & "; Rol=" & normalizedRole)
        UserStoreRecoveryService.CreateBackup()
    End Sub

    Public Shared Function ToggleUserActive(username As String) As Boolean
        If Not AppState.IsAdmin Then Throw New UnauthorizedAccessException("Kullanıcı durumunu yalnızca Admin değiştirebilir.")

        username = If(username, "").Trim()
        If username = "" Then Throw New ArgumentException("Durumu değiştirilecek kullanıcı seçilmelidir.")

        MigrateUserPasswordStorage()
        UserStoreRecoveryService.CreateBackup()

        Dim newIsActive As Boolean = False
        Dim normalizedUserName As String = ""
        CsvUtil.UpdateTwoFilesLocked(
            AppPaths.UsersCsv,
            DataService.UserHeaders,
            AppPaths.ActiveSessionsCsv,
            DataService.ActiveSessionHeaders,
            Sub(userRows, sessionRows)
                Dim existing = userRows.FirstOrDefault(
                    Function(r) String.Equals(DataService.GetValue(r, "Username"), username, StringComparison.OrdinalIgnoreCase))
                If existing Is Nothing Then Throw New InvalidOperationException("Kullanıcı bulunamadı: " & username)

                Dim isCurrentlyActive = String.Equals(DataService.GetValue(existing, "IsActive"), "YES", StringComparison.OrdinalIgnoreCase)
                If isCurrentlyActive AndAlso String.Equals(username, AppState.CurrentUserName, StringComparison.OrdinalIgnoreCase) Then
                    Throw New InvalidOperationException("Açık olan ADMIN hesabı kendi kendisini pasif yapamaz.")
                End If

                newIsActive = Not isCurrentlyActive
                normalizedUserName = DataService.GetValue(existing, "Username")
                Dim normalizedRole = AppState.NormalizeRole(DataService.GetValue(existing, "Role"))
                If Not newIsActive AndAlso
                   String.Equals(normalizedRole, AppState.RoleAdmin, StringComparison.OrdinalIgnoreCase) AndAlso
                   HasActiveSession(sessionRows, normalizedUserName) Then
                    Throw New InvalidOperationException(
                        "Açık oturumu bulunan Admin hesabı pasif yapılamaz. Admin oturumları başka bir Admin tarafından kapatılamaz.")
                End If

                existing("IsActive") = If(newIsActive, "YES", "NO")
                If Not newIsActive Then
                    sessionRows.RemoveAll(
                        Function(r) String.Equals(DataService.GetValue(r, "Username"), username, StringComparison.OrdinalIgnoreCase))
                End If
            End Sub)

        AuditService.Log(
            "USER_ACTIVE_TOGGLE",
            "",
            "",
            "Kullanıcı durumu değiştirildi: " & normalizedUserName &
            "; YeniDurum=" & If(newIsActive, "AKTİF", "PASİF"))
        UserStoreRecoveryService.CreateBackup()
        Return newIsActive
    End Function

    Public Shared Function EndAllOtherSessionsByAdmin() As Integer
        If Not AppState.IsAdmin Then Throw New UnauthorizedAccessException("Tüm oturumları yalnızca Admin kapatabilir.")

        Dim currentSessionId = If(AppState.CurrentSessionId, "").Trim()
        If currentSessionId = "" Then Throw New InvalidOperationException("Açık Admin oturumu bulunamadı.")
        Dim adminUserNames = GetUsers().
            Where(Function(r) String.Equals(
                AppState.NormalizeRole(DataService.GetValue(r, "Role")),
                AppState.RoleAdmin,
                StringComparison.OrdinalIgnoreCase)).
            Select(Function(r) DataService.GetValue(r, "Username")).
            Where(Function(username) username.Trim() <> "").
            ToHashSet(StringComparer.OrdinalIgnoreCase)
        Dim sessionsClosedByAdmin As New List(Of Dictionary(Of String, String))()

        Dim closedCount = CsvUtil.UpdateRowsLocked(
            AppPaths.ActiveSessionsCsv,
            DataService.ActiveSessionHeaders,
            Function(rows)
                If Not rows.Any(
                    Function(r) String.Equals(
                        DataService.GetValue(r, "SessionId"),
                        currentSessionId,
                        StringComparison.OrdinalIgnoreCase)) Then
                    Throw New InvalidOperationException("Açık Admin oturumu aktif oturum listesinde bulunamadı. Yeniden giriş yapıp tekrar deneyin.")
                End If

                Dim sessionsToClose = rows.
                    Where(
                        Function(r)
                            Dim rowSessionId = DataService.GetValue(r, "SessionId")
                            Dim rowUsername = DataService.GetValue(r, "Username")
                            Return Not String.Equals(rowSessionId, currentSessionId, StringComparison.OrdinalIgnoreCase) AndAlso
                                   Not adminUserNames.Contains(rowUsername)
                        End Function).
                    Select(Function(r) New Dictionary(Of String, String)(r, StringComparer.OrdinalIgnoreCase)).
                    ToList()
                sessionsClosedByAdmin.AddRange(sessionsToClose)

                For Each sessionToClose In sessionsToClose
                    rows.RemoveAll(
                        Function(r) String.Equals(
                            DataService.GetValue(r, "SessionId"),
                            DataService.GetValue(sessionToClose, "SessionId"),
                            StringComparison.OrdinalIgnoreCase))
                Next

                Return sessionsToClose.Count
            End Function)

        For Each closedSession In sessionsClosedByAdmin
            DataService.RequestUserSessionEnd(
                DataService.GetValue(closedSession, "SessionId"),
                DataService.GetValue(closedSession, "Username"),
                DataService.GetValue(closedSession, "ComputerName"),
                AppState.CurrentUserName,
                "ADMIN_END_ALL_OTHER_SESSIONS")
        Next

        AuditService.Log(
            "ADMIN_END_ALL_OTHER_SESSIONS",
            "",
            "",
            "Admin oturumları korunarak diğer açık oturumlar kapatıldı. Kapatılan oturum: " & closedCount.ToString())
        Return closedCount
    End Function

    Public Shared Function GetActiveSessionsByAdmin() As List(Of Dictionary(Of String, String))
        If Not AppState.IsAdmin Then Throw New UnauthorizedAccessException("Açık oturumları yalnızca Admin görebilir.")

        Dim users = GetUsers().
            GroupBy(Function(r) DataService.GetValue(r, "Username"), StringComparer.OrdinalIgnoreCase).
            ToDictionary(
                Function(g) g.Key,
                Function(g) AppState.NormalizeRole(DataService.GetValue(g.First(), "Role")),
                StringComparer.OrdinalIgnoreCase)

        Dim sessions = DataService.GetActiveUserSessions()
        For Each session In sessions
            Dim username = DataService.GetValue(session, "Username")
            Dim role = ""
            If users.TryGetValue(username, role) Then session("Role") = role Else session("Role") = ""
            session("IsCurrent") = If(
                String.Equals(DataService.GetValue(session, "SessionId"), AppState.CurrentSessionId, StringComparison.OrdinalIgnoreCase),
                "YES",
                "NO")
        Next

        Return sessions
    End Function

    Public Shared Sub EndSessionByAdmin(sessionId As String)
        If Not AppState.IsAdmin Then Throw New UnauthorizedAccessException("Oturumları yalnızca Admin kapatabilir.")

        sessionId = If(sessionId, "").Trim()
        If sessionId = "" Then Throw New ArgumentException("Kapatılacak oturum seçilmelidir.")
        If String.Equals(sessionId, AppState.CurrentSessionId, StringComparison.OrdinalIgnoreCase) Then
            Throw New InvalidOperationException("Kullanmakta olduğunuz Admin oturumunu bu ekrandan kapatamazsınız.")
        End If

        Dim adminUserNames = GetUsers().
            Where(Function(r) String.Equals(
                AppState.NormalizeRole(DataService.GetValue(r, "Role")),
                AppState.RoleAdmin,
                StringComparison.OrdinalIgnoreCase)).
            Select(Function(r) DataService.GetValue(r, "Username")).
            Where(Function(username) username.Trim() <> "").
            ToHashSet(StringComparer.OrdinalIgnoreCase)

        Dim closedUser As String = ""
        Dim closedComputer As String = ""
        Dim removed = CsvUtil.UpdateRowsLocked(
            AppPaths.ActiveSessionsCsv,
            DataService.ActiveSessionHeaders,
            Function(rows)
                Dim session = rows.FirstOrDefault(
                    Function(r) String.Equals(
                        DataService.GetValue(r, "SessionId"),
                        sessionId,
                        StringComparison.OrdinalIgnoreCase))
                If session Is Nothing Then Return False

                closedUser = DataService.GetValue(session, "Username")
                If adminUserNames.Contains(closedUser) Then
                    Throw New InvalidOperationException("Admin oturumları başka bir Admin tarafından kapatılamaz.")
                End If
                closedComputer = DataService.GetValue(session, "ComputerName")
                rows.Remove(session)
                Return True
            End Function)

        If Not removed Then Throw New InvalidOperationException("Seçili oturum zaten kapanmış veya listeden kaldırılmış.")

        DataService.RequestUserSessionEnd(
            sessionId,
            closedUser,
            closedComputer,
            AppState.CurrentUserName,
            "ADMIN_END_SESSION")

        AuditService.Log(
            "ADMIN_END_SESSION",
            "",
            "",
            "Admin açık oturumu kapattı. Kullanıcı=" & closedUser & "; Bilgisayar=" & closedComputer & "; SessionId=" & sessionId)
    End Sub

    Private Shared Function CreateSalt() As Byte()
        Dim salt(15) As Byte
        RandomNumberGenerator.Fill(salt)
        Return salt
    End Function

    Private Shared Function GenerateTemporaryPassword() As String
        Const upperChars As String = "ABCDEFGHJKLMNPQRSTUVWXYZ"
        Const lowerChars As String = "abcdefghijkmnopqrstuvwxyz"
        Const digitChars As String = "23456789"
        Const allChars As String = upperChars & lowerChars & digitChars

        Dim chars As New List(Of Char) From {
            upperChars(RandomNumberGenerator.GetInt32(upperChars.Length)),
            lowerChars(RandomNumberGenerator.GetInt32(lowerChars.Length)),
            digitChars(RandomNumberGenerator.GetInt32(digitChars.Length))
        }

        While chars.Count < 14
            chars.Add(allChars(RandomNumberGenerator.GetInt32(allChars.Length)))
        End While

        For i As Integer = chars.Count - 1 To 1 Step -1
            Dim swapIndex = RandomNumberGenerator.GetInt32(i + 1)
            Dim temp = chars(i)
            chars(i) = chars(swapIndex)
            chars(swapIndex) = temp
        Next

        Return New String(chars.ToArray())
    End Function

    Private Shared Function HasActiveSession(rows As IEnumerable(Of Dictionary(Of String, String)), username As String) As Boolean
        Return rows.Any(
            Function(row) String.Equals(
                DataService.GetValue(row, "Username"),
                username,
                StringComparison.OrdinalIgnoreCase))
    End Function

    Private Shared Sub ValidateNewPassword(password As String)
        password = If(password, "")
        If password = "" Then Throw New ArgumentException("Yeni parola boş olamaz.")
    End Sub

    Private Shared Function VerifyPassword(user As Dictionary(Of String, String), password As String) As Boolean
        If user Is Nothing Then Return False

        Dim saltText = DataService.GetValue(user, "PasswordSalt")
        Dim stored = DataService.GetValue(user, "PasswordHash")
        If String.IsNullOrWhiteSpace(saltText) OrElse String.IsNullOrWhiteSpace(stored) Then Return False

        Try
            Dim salt = Convert.FromBase64String(saltText)
            Dim hash = HashPassword(If(password, ""), salt)
            Return SlowEquals(hash, stored)
        Catch ex As FormatException
            Return False
        End Try
    End Function

    Private Shared Function LegacyDefaultAdminPassword() As String
        Return Encoding.ASCII.GetString(Convert.FromBase64String("YWRtaW4xMjM="))
    End Function

    Private Shared Function HashPassword(password As String, salt As Byte()) As String
        Using pbkdf2 As New Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256)
            Return Convert.ToBase64String(pbkdf2.GetBytes(32))
        End Using
    End Function

    Private Shared Function SlowEquals(a As String, b As String) As Boolean
        Dim ab = Encoding.UTF8.GetBytes(a)
        Dim bb = Encoding.UTF8.GetBytes(b)
        Return CryptographicOperations.FixedTimeEquals(ab, bb)
    End Function
End Class
