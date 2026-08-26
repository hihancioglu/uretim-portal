Imports System
Imports System.Windows.Forms

Public Class AppActivityMessageFilter
    Implements IMessageFilter

    Private ReadOnly onActivity As Action

    Public Sub New(activityCallback As Action)
        onActivity = activityCallback
    End Sub

    Public Function PreFilterMessage(ByRef m As Message) As Boolean Implements IMessageFilter.PreFilterMessage
        Select Case m.Msg
            Case &H100 To &H109, &H200 To &H20E
                If onActivity IsNot Nothing Then onActivity.Invoke()
        End Select

        Return False
    End Function
End Class
