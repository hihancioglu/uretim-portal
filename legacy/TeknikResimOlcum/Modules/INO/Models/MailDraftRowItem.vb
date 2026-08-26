Public Class MailDraftRowItem
    Public Property InternalId As Integer
    Public Property Sira As String
    Public Property SayacAdi As String
    Public Property SiparisYeri As String
    Public Property IsEmriNo As String

    Public Overrides Function ToString() As String
        Return $"{Sira} | {SayacAdi} | {SiparisYeri} | {IsEmriNo}"
    End Function
End Class
