Imports System.Collections.Generic
Imports System.IO
Imports System.Reflection

Public NotInheritable Class OutlookEmailDraftService
    Private Sub New()
    End Sub

    Public Shared Function TryOpenEditableDraft(subject As String,
                                                htmlBody As String,
                                                Optional toText As String = "",
                                                Optional ccText As String = "",
                                                Optional attachmentPaths As IEnumerable(Of String) = Nothing) As Boolean
        Try
            Dim outlookType = Type.GetTypeFromProgID("Outlook.Application")
            If outlookType Is Nothing Then Return False

            Dim outlookApp = Activator.CreateInstance(outlookType)
            Dim mailItem = outlookType.InvokeMember(
                "CreateItem",
                BindingFlags.InvokeMethod,
                Nothing,
                outlookApp,
                New Object() {0})
            If mailItem Is Nothing Then Return False

            Dim mailType = mailItem.GetType()
            mailType.InvokeMember("To", BindingFlags.SetProperty, Nothing, mailItem, New Object() {If(toText, "")})
            mailType.InvokeMember("CC", BindingFlags.SetProperty, Nothing, mailItem, New Object() {If(ccText, "")})
            mailType.InvokeMember("Subject", BindingFlags.SetProperty, Nothing, mailItem, New Object() {If(subject, "")})
            mailType.InvokeMember("HTMLBody", BindingFlags.SetProperty, Nothing, mailItem, New Object() {If(htmlBody, "")})
            AddAttachments(mailItem, attachmentPaths)

            ' Display yalnızca düzenlenebilir taslağı açar; e-posta otomatik gönderilmez.
            mailType.InvokeMember("Display", BindingFlags.InvokeMethod, Nothing, mailItem, New Object() {False})

            Try
                Dim inspector = mailType.InvokeMember("GetInspector", BindingFlags.GetProperty, Nothing, mailItem, Nothing)
                If inspector IsNot Nothing Then
                    inspector.GetType().InvokeMember("Activate", BindingFlags.InvokeMethod, Nothing, inspector, Nothing)
                End If
            Catch
            End Try

            Return True
        Catch ex As Exception
            ErrorLogService.Log("OutlookEmailDraftService.OpenDraft", ex)
            Return False
        End Try
    End Function

    Public Shared Function TrySendMail(subject As String,
                                       htmlBody As String,
                                       toText As String,
                                       Optional ccText As String = "",
                                       Optional attachmentPaths As IEnumerable(Of String) = Nothing) As Boolean
        Try
            If String.IsNullOrWhiteSpace(toText) AndAlso String.IsNullOrWhiteSpace(ccText) Then Return True

            Dim outlookType = Type.GetTypeFromProgID("Outlook.Application")
            If outlookType Is Nothing Then Return False

            Dim outlookApp = Activator.CreateInstance(outlookType)
            Dim mailItem = outlookType.InvokeMember(
                "CreateItem",
                BindingFlags.InvokeMethod,
                Nothing,
                outlookApp,
                New Object() {0})
            If mailItem Is Nothing Then Return False

            Dim mailType = mailItem.GetType()
            mailType.InvokeMember("To", BindingFlags.SetProperty, Nothing, mailItem, New Object() {If(toText, "")})
            mailType.InvokeMember("CC", BindingFlags.SetProperty, Nothing, mailItem, New Object() {If(ccText, "")})
            mailType.InvokeMember("Subject", BindingFlags.SetProperty, Nothing, mailItem, New Object() {If(subject, "")})
            mailType.InvokeMember("HTMLBody", BindingFlags.SetProperty, Nothing, mailItem, New Object() {If(htmlBody, "")})
            AddAttachments(mailItem, attachmentPaths)
            mailType.InvokeMember("Send", BindingFlags.InvokeMethod, Nothing, mailItem, Nothing)
            Return True
        Catch ex As Exception
            ErrorLogService.Log("OutlookEmailDraftService.SendMail", ex)
            Return False
        End Try
    End Function

    Private Shared Sub AddAttachments(mailItem As Object, attachmentPaths As IEnumerable(Of String))
        If mailItem Is Nothing OrElse attachmentPaths Is Nothing Then Return

        Dim uniquePaths As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each rawPath In attachmentPaths
            Dim fullPath = If(rawPath, "").Trim()
            If fullPath = "" OrElse Not File.Exists(fullPath) OrElse Not uniquePaths.Add(fullPath) Then Continue For

            Dim attachments = mailItem.GetType().InvokeMember(
                "Attachments",
                BindingFlags.GetProperty,
                Nothing,
                mailItem,
                Nothing)
            If attachments Is Nothing Then Throw New InvalidOperationException("Outlook e-posta ekleri oluşturulamadı.")

            attachments.GetType().InvokeMember(
                "Add",
                BindingFlags.InvokeMethod,
                Nothing,
                attachments,
                New Object() {fullPath})
        Next
    End Sub
End Class
