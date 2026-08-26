Imports System.Collections.Generic

Public Class MeasurementDraft
    Public Property Version As Integer = 1
    Public Property UserName As String = ""
    Public Property ComputerName As String = ""
    Public Property TrCode As String = ""
    Public Property DrawingRev As String = ""
    Public Property DrawingScope As String = ProductInfo.DrawingScopePlastic
    Public Property LotNo As String = ""
    Public Property SerialNo As String = ""
    Public Property EyeCount As Integer = 1
    Public Property EyeNo As Integer = 1
    Public Property SelectedMeasureId As String = ""
    Public Property SavedAt As DateTime = DateTime.Now
    Public Property Eyes As New List(Of MeasurementDraftEye)()
End Class

Public Class MeasurementDraftEye
    Public Property EyeNo As Integer
    Public Property IsClosed As Boolean
    Public Property Values As New List(Of MeasurementDraftValue)()
End Class

Public Class MeasurementDraftValue
    Public Property MeasureId As String = ""
    Public Property MeasuredValueText As String = ""
    Public Property Result As String = ""
    Public Property Note As String = ""
End Class
