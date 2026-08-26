Public Class TestRequestStep
    Public Property RequestId As String = ""
    Public Property StepId As String = ""
    Public Property SortNo As Integer
    Public Property TestName As String = ""
    Public Property TestDescription As String = ""
    Public Property Status As String = "PENDING"
    Public Property Result As String = ""
    Public Property Explanation As String = ""
    Public Property CompletedAt As String = ""
    Public Property CompletedBy As String = ""
    Public Property CompletedComputerName As String = ""
    Public Property SkippedAt As String = ""
    Public Property SkippedBy As String = ""
    Public Property SkipReason As String = ""
    Public Property ReopenedAt As String = ""
    Public Property ReopenedBy As String = ""
    Public Property ReopenReason As String = ""
    Public Property CreatedAt As String = ""
    Public Property CreatedBy As String = ""
    Public Property UpdatedAt As String = ""
    Public Property UpdatedBy As String = ""

    Public ReadOnly Property IsResolved As Boolean
        Get
            Return String.Equals(Status, "COMPLETED", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(Status, "SKIPPED", StringComparison.OrdinalIgnoreCase)
        End Get
    End Property
End Class
