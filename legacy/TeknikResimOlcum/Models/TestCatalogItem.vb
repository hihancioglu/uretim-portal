Public Class TestCatalogItem
    Public Property TestName As String = ""
    Public Property Description As String = ""
    Public Property IsActive As String = "YES"
    Public Property SortNo As String = "0"
    Public Property CreatedBy As String = ""
    Public Property CreatedAt As String = ""
    Public Property UpdatedBy As String = ""
    Public Property UpdatedAt As String = ""

    Public ReadOnly Property DisplayName As String
        Get
            If String.IsNullOrWhiteSpace(Description) Then Return TestName
            Return TestName & " — " & Description
        End Get
    End Property
End Class
