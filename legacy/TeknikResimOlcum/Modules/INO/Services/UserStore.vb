Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Security.Cryptography
Imports System.Text

Public Class UserStore
    Public Const RoleAdmin As String = "ADMİN"
    Public Const RoleMechanism As String = "MEKANİZMA"
    Public Const RoleApproval As String = "ONAY"

    Private Class UserRecord
        Public Property UserName As String
        Public Property SaltBase64 As String
        Public Property HashBase64 As String
        Public Property PasswordTextBase64 As String
        Public Property RoleName As String
    End Class

    Private ReadOnly storePath As String
    Private ReadOnly createDefaultsWhenMissing As Boolean
    Private ReadOnly users As New Dictionary(Of String, UserRecord)(StringComparer.OrdinalIgnoreCase)

    Public Sub New(Optional dataDirectory As String = "", Optional createDefaults As Boolean = True)
        If String.IsNullOrWhiteSpace(dataDirectory) Then dataDirectory = AppDomain.CurrentDomain.BaseDirectory
        Directory.CreateDirectory(dataDirectory)
        storePath = System.IO.Path.Combine(dataDirectory, "INO_Users.dat")
        createDefaultsWhenMissing = createDefaults
        LoadOrCreate()
    End Sub

    Public Function GetRoleOptions() As String()
        Return New String() {RoleAdmin, RoleMechanism, RoleApproval}
    End Function

    Public Function GetUsers() As List(Of String)
        Return users.Keys.OrderBy(Function(x) x).ToList()
    End Function

    Public Function HasUser(userName As String) As Boolean
        Return users.ContainsKey(userName)
    End Function

    Public Function GetRole(userName As String) As String
        If String.IsNullOrWhiteSpace(userName) Then Return ""

        If users.ContainsKey(userName) Then
            Dim storedRole = NormalizeRole(users(userName).RoleName)
            If storedRole.Length > 0 Then Return storedRole
        End If

        Return InferRoleFromUserName(userName)
    End Function

    Public Function CountUsersWithRole(roleName As String) As Integer
        Dim normalizedRole = NormalizeRole(roleName)
        Dim count As Integer = 0

        For Each userName In users.Keys
            If String.Equals(GetRole(userName), normalizedRole, StringComparison.OrdinalIgnoreCase) Then
                count += 1
            End If
        Next

        Return count
    End Function

    Public Function GetVisiblePassword(userName As String) As String
        If Not users.ContainsKey(userName) Then Return ""

        Dim encoded = users(userName).PasswordTextBase64

        If String.IsNullOrWhiteSpace(encoded) Then
            Return "(eski şifre görüntülenemez)"
        End If

        Try
            Return Encoding.UTF8.GetString(Convert.FromBase64String(encoded))
        Catch
            Return "(şifre okunamadı)"
        End Try
    End Function

    Public Function ValidatePassword(userName As String, password As String) As Boolean
        If Not users.ContainsKey(userName) Then Return False

        Dim record = users(userName)
        Dim salt = Convert.FromBase64String(record.SaltBase64)
        Dim hash = ComputeHash(password, salt)

        Return String.Equals(Convert.ToBase64String(hash), record.HashBase64, StringComparison.Ordinal)
    End Function

    Public Sub SetPassword(userName As String, password As String)
        If String.IsNullOrWhiteSpace(userName) Then Throw New ArgumentException("Kullanıcı adı boş olamaz.")
        If String.IsNullOrWhiteSpace(password) Then Throw New ArgumentException("Şifre boş olamaz.")

        Dim roleName = If(users.ContainsKey(userName), users(userName).RoleName, InferRoleFromUserName(userName))
        SetUserWithoutSave(userName, password, roleName)
        Save()
    End Sub

    Public Sub CreateOrUpdateUser(userName As String, password As String, roleName As String)
        userName = If(userName, "").Trim()
        roleName = NormalizeRole(roleName)

        If String.IsNullOrWhiteSpace(userName) Then Throw New ArgumentException("Kullanıcı adı boş olamaz.")
        If String.IsNullOrWhiteSpace(roleName) Then Throw New ArgumentException("Rol seçilmelidir.")

        If users.ContainsKey(userName) Then
            If String.IsNullOrWhiteSpace(password) Then
                users(userName).RoleName = roleName
            Else
                SetUserWithoutSave(userName, password, roleName)
            End If
        Else
            If String.IsNullOrWhiteSpace(password) Then Throw New ArgumentException("Yeni kullanıcı için şifre zorunludur.")
            SetUserWithoutSave(userName, password, roleName)
        End If

        Save()
    End Sub

    Public Sub DeleteUser(userName As String)
        If String.IsNullOrWhiteSpace(userName) Then Throw New ArgumentException("Kullanıcı seçilmelidir.")

        If users.ContainsKey(userName) Then
            users.Remove(userName)
            Save()
        End If
    End Sub

    Private Sub LoadOrCreate()
        If Not File.Exists(storePath) Then
            If createDefaultsWhenMissing Then
                CreateDefaults()
                Save()
            End If
            Return
        End If

        users.Clear()

        For Each line In File.ReadAllLines(storePath, Encoding.UTF8)
            If String.IsNullOrWhiteSpace(line) Then Continue For

            Dim parts = line.Split("|"c)

            If parts.Length = 3 Then
                users(parts(0)) = New UserRecord With {
                    .UserName = parts(0),
                    .SaltBase64 = parts(1),
                    .HashBase64 = parts(2),
                    .PasswordTextBase64 = "",
                    .RoleName = InferRoleFromUserName(parts(0))
                }
            ElseIf parts.Length = 4 Then
                users(parts(0)) = New UserRecord With {
                    .UserName = parts(0),
                    .SaltBase64 = parts(1),
                    .HashBase64 = parts(2),
                    .PasswordTextBase64 = parts(3),
                    .RoleName = InferRoleFromUserName(parts(0))
                }
            ElseIf parts.Length >= 5 Then
                users(parts(0)) = New UserRecord With {
                    .UserName = parts(0),
                    .SaltBase64 = parts(1),
                    .HashBase64 = parts(2),
                    .PasswordTextBase64 = parts(3),
                    .RoleName = NormalizeRole(parts(4))
                }
            End If
        Next

        If users.Count = 0 Then
            If createDefaultsWhenMissing Then
                CreateDefaults()
                Save()
            End If
            Return
        End If

        MigrateKnownDefaultPasswords()
        EnsureRoles()
        Save()
    End Sub

    Private Sub MigrateKnownDefaultPasswords()
        Dim defaults As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
            {"MEKANİZMA", "1234"},
            {"ADMİN", "admin"},
            {"OZAN ÇAĞLAYAN", "1234"},
            {"GÜLİZ KARTAL", "1234"},
            {"NESLİHAN ŞENOL", "1234"},
            {"AYAR", "1234"}
        }

        For Each kvp In defaults
            If Not users.ContainsKey(kvp.Key) Then Continue For
            If Not String.IsNullOrWhiteSpace(users(kvp.Key).PasswordTextBase64) Then Continue For

            If ValidatePassword(kvp.Key, kvp.Value) Then
                users(kvp.Key).PasswordTextBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(kvp.Value))
            End If
        Next
    End Sub

    Private Sub EnsureDefaultUsers()
        If Not users.ContainsKey("MEKANİZMA") Then SetUserWithoutSave("MEKANİZMA", "1234", RoleMechanism)
        If Not users.ContainsKey("ADMİN") Then SetUserWithoutSave("ADMİN", "admin", RoleAdmin)
        If Not users.ContainsKey("OZAN ÇAĞLAYAN") Then SetUserWithoutSave("OZAN ÇAĞLAYAN", "1234", RoleApproval)
        If Not users.ContainsKey("GÜLİZ KARTAL") Then SetUserWithoutSave("GÜLİZ KARTAL", "1234", RoleApproval)
        If Not users.ContainsKey("NESLİHAN ŞENOL") Then SetUserWithoutSave("NESLİHAN ŞENOL", "1234", RoleApproval)
        If Not users.ContainsKey("AYAR") Then SetUserWithoutSave("AYAR", "1234", RoleApproval)
    End Sub

    Private Sub EnsureRoles()
        For Each record In users.Values
            If String.IsNullOrWhiteSpace(record.RoleName) Then
                record.RoleName = InferRoleFromUserName(record.UserName)
            Else
                record.RoleName = NormalizeRole(record.RoleName)
            End If
        Next
    End Sub

    Private Sub CreateDefaults()
        users.Clear()
        EnsureDefaultUsers()
    End Sub

    Private Sub SetUserWithoutSave(userName As String, password As String, roleName As String)
        Dim salt = CreateSalt()
        Dim hash = ComputeHash(password, salt)

        users(userName) = New UserRecord With {
            .UserName = userName,
            .SaltBase64 = Convert.ToBase64String(salt),
            .HashBase64 = Convert.ToBase64String(hash),
            .PasswordTextBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(password)),
            .RoleName = NormalizeRole(roleName)
        }
    End Sub

    Private Sub Save()
        Dim lines = users.Values.
            OrderBy(Function(x) x.UserName).
            Select(Function(x) x.UserName & "|" & x.SaltBase64 & "|" & x.HashBase64 & "|" & If(x.PasswordTextBase64, "") & "|" & NormalizeRole(x.RoleName)).
            ToArray()

        File.WriteAllLines(storePath, lines, Encoding.UTF8)
    End Sub

    Private Function NormalizeRole(roleName As String) As String
        Dim r = If(roleName, "").Trim().ToUpperInvariant()

        If r = "ADMIN" OrElse r = "ADMİN" OrElse r = "YÖNETİCİ" Then Return RoleAdmin
        If r = "MEKANIZMA" OrElse r = "MEKANİZMA" Then Return RoleMechanism
        If r = "ONAY" OrElse r = "ONAYCI" OrElse r = "KALİTE" Then Return RoleApproval

        Return r
    End Function

    Private Function InferRoleFromUserName(userName As String) As String
        If String.Equals(userName, "ADMİN", StringComparison.OrdinalIgnoreCase) Then Return RoleAdmin
        If String.Equals(userName, "MEKANİZMA", StringComparison.OrdinalIgnoreCase) Then Return RoleMechanism

        If String.Equals(userName, "OZAN ÇAĞLAYAN", StringComparison.OrdinalIgnoreCase) OrElse
           String.Equals(userName, "GÜLİZ KARTAL", StringComparison.OrdinalIgnoreCase) OrElse
           String.Equals(userName, "NESLİHAN ŞENOL", StringComparison.OrdinalIgnoreCase) OrElse
           String.Equals(userName, "AYAR", StringComparison.OrdinalIgnoreCase) Then
            Return RoleApproval
        End If

        Return RoleApproval
    End Function

    Private Function CreateSalt() As Byte()
        Dim salt(15) As Byte

        Using rng = RandomNumberGenerator.Create()
            rng.GetBytes(salt)
        End Using

        Return salt
    End Function

    Private Function ComputeHash(password As String, salt As Byte()) As Byte()
        Dim passwordBytes = Encoding.UTF8.GetBytes(If(password, ""))
        Dim input(salt.Length + passwordBytes.Length - 1) As Byte

        Buffer.BlockCopy(salt, 0, input, 0, salt.Length)
        Buffer.BlockCopy(passwordBytes, 0, input, salt.Length, passwordBytes.Length)

        Using sha = SHA256.Create()
            Return sha.ComputeHash(input)
        End Using
    End Function
End Class
