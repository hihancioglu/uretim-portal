Imports System.Windows.Forms

Public NotInheritable Class MeasurementCorrectionUiService
    Private Sub New()
    End Sub

    Public Shared Function EditMeasurement(owner As IWin32Window,
                                           measurementRow As Dictionary(Of String, String)) As Boolean
        AuthorizationService.Require(AppState.IsAdmin, "Geçmiş Ölçüm Düzeltme")
        If measurementRow Is Nothing Then Throw New ArgumentNullException(NameOf(measurementRow))

        Dim recordId = DataService.GetValue(measurementRow, "RecordId").Trim()
        Dim eyeNo = DataService.GetValue(measurementRow, "EyeNo").Trim()
        Dim measureId = DataService.GetValue(measurementRow, "MeasureId").Trim()
        Dim measureName = DataService.GetValue(measurementRow, "MeasureName").Trim()
        Dim oldValue = DataService.GetValue(measurementRow, "MeasuredValue").Trim()
        Dim lowerLimit = DataService.GetValue(measurementRow, "LowerLimit").Trim()
        Dim upperLimit = DataService.GetValue(measurementRow, "UpperLimit").Trim()
        Dim measurementDate = DataService.GetValue(measurementRow, "MeasurementDate").Trim()
        Dim operatorName = DataService.GetValue(measurementRow, "OperatorName").Trim()
        Dim trCode = DataService.GetValue(measurementRow, "TrCode").Trim()

        If recordId = "" OrElse measureId = "" OrElse oldValue = "" Then
            Throw New InvalidOperationException("Seçili satır düzenlenebilir bir ölçüm kaydı değildir.")
        End If

        Using correctionForm As New FrmMeasurementCorrection(
            recordId,
            trCode,
            eyeNo,
            measureId,
            measureName,
            oldValue,
            lowerLimit,
            upperLimit,
            measurementDate,
            operatorName)

            If correctionForm.ShowDialog(owner) <> DialogResult.OK Then Return False
            Dim confirmation = MessageBox.Show(
                owner,
                "Ölçüm değeri değiştirilecek:" & Environment.NewLine &
                oldValue & "  →  " & correctionForm.NewValueText & Environment.NewLine & Environment.NewLine &
                "Bu değişiklik SPC analizlerini etkileyecektir. Devam edilsin mi?",
                "Geçmiş ölçümü düzelt",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2)
            If confirmation <> DialogResult.Yes Then Return False

            Dim newResult = DataService.CorrectMeasurementValue(
                recordId,
                eyeNo,
                measureId,
                measurementDate,
                correctionForm.NewValueText,
                correctionForm.CorrectionReason)

            measurementRow("MeasuredValue") = correctionForm.NewValueText
            measurementRow("Result") = newResult

            MessageBox.Show(
                owner,
                "Ölçüm değeri düzeltildi." & Environment.NewLine &
                "Yeni sonuç: " & newResult & Environment.NewLine &
                "Eski ve yeni değer düzeltme geçmişine kaydedildi.",
                "Düzeltme tamamlandı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information)
            Return True
        End Using
    End Function
End Class
