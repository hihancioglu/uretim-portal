Public Class DashboardPeriodStat
    Public Property PeriodName As String
    Public Property Total As Integer
    Public Property Approved As Integer
    Public Property Pending As Integer
    Public Property Rejected As Integer
    Public Property CheckRequired As Integer
    Public Property Ino1Pending As Integer
    Public Property Ino2Pending As Integer

    Public Sub New(name As String)
        PeriodName = name
    End Sub
End Class
