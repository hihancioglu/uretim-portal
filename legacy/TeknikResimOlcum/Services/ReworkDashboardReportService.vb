Imports System.IO
Imports System.Text
Imports System.Text.Json

Public NotInheritable Class ReworkDashboardReportService
    Private Const ChartScriptTag As String = "<script src=""vendor/chart-4.4.1.umd.min.js""></script>"

    Private Sub New()
    End Sub

    Public Shared Sub CreateReport(outputPath As String, result As ReworkImportResult)
        If String.IsNullOrWhiteSpace(outputPath) Then
            Throw New ArgumentException("Rapor dosya yolu belirtilmedi.", NameOf(outputPath))
        End If
        If result Is Nothing OrElse Not result.IsSuccess OrElse result.Records Is Nothing OrElse result.Records.Count = 0 Then
            Throw New InvalidOperationException("Raporlanacak REWORK kaydı bulunamadı.")
        End If
        If Not File.Exists(AppPaths.ReworkDashboardHtmlPath) Then
            Throw New FileNotFoundException("REWORK dashboard HTML dosyası bulunamadı.", AppPaths.ReworkDashboardHtmlPath)
        End If

        Dim chartPath = Path.Combine(AppPaths.ResourcesDir, "vendor", "chart-4.4.1.umd.min.js")
        If Not File.Exists(chartPath) Then
            Throw New FileNotFoundException("REWORK raporu için Chart.js dosyası bulunamadı.", chartPath)
        End If

        Dim html = File.ReadAllText(AppPaths.ReworkDashboardHtmlPath, Encoding.UTF8)
        If Not html.Contains(ChartScriptTag, StringComparison.Ordinal) Then
            Throw New InvalidDataException("REWORK dashboard içindeki Chart.js bağlantısı bulunamadı.")
        End If

        Dim chartScript = File.ReadAllText(chartPath, Encoding.UTF8).
            Replace("</script", "<\/script", StringComparison.OrdinalIgnoreCase)
        html = html.Replace(ChartScriptTag, "<script>" & chartScript & "</script>", StringComparison.Ordinal)

        Dim payload = BuildPayload(result)
        Dim jsonOptions As New JsonSerializerOptions With {
            .PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }
        Dim payloadJson = JsonSerializer.Serialize(payload, jsonOptions).
            Replace("</", "<\/", StringComparison.Ordinal)

        Dim reportStyles =
            "<style>" &
            ".hero{position:static}.report-meta{opacity:.9}" &
            "@media print{" &
            "body{background:#fff;padding:0}.page{gap:8px}.card,.kpi,.hero{break-inside:avoid}" &
            ".table-wrap{max-height:none;overflow:visible}button{display:none!important}" &
            "}" &
            "</style>"
        html = html.Replace("</head>", reportStyles & "</head>", StringComparison.OrdinalIgnoreCase)

        Dim bootstrap As New StringBuilder()
        bootstrap.AppendLine("<script>")
        bootstrap.Append("const reportPayload=").Append(payloadJson).AppendLine(";")
        bootstrap.AppendLine("currentState=reportPayload.state||null;")
        bootstrap.AppendLine("allRecords=Array.isArray(reportPayload.records)?reportPayload.records:[];")
        bootstrap.AppendLine("const reportNotice=el('notice');")
        bootstrap.AppendLine("reportNotice.style.display=reportPayload.isSuccess?'none':'block';")
        bootstrap.AppendLine("reportNotice.textContent=reportPayload.statusMessage||'';")
        bootstrap.AppendLine("const reportSource=currentState?`${esc(currentState.originalFileName)}<br>${esc(currentState.importedAt)} · ${esc(currentState.importedBy)} · ${amount(currentState.recordCount)} kayıt`:'REWORK veri kaynağı bulunamadı.';")
        bootstrap.AppendLine("el('sourceInfo').innerHTML=`${reportSource}<br><span class=""report-meta"">Rapor: ${esc(reportPayload.generatedAt)} · ${esc(reportPayload.generatedBy)}</span>`;")
        bootstrap.AppendLine("hydrateFilters();")
        bootstrap.AppendLine("render();")
        bootstrap.AppendLine("</script>")
        html = html.Replace("</body>", bootstrap.ToString() & "</body>", StringComparison.OrdinalIgnoreCase)

        Dim outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath))
        If Not String.IsNullOrWhiteSpace(outputDirectory) Then Directory.CreateDirectory(outputDirectory)
        File.WriteAllText(outputPath, html, New UTF8Encoding(False))
    End Sub

    Private Shared Function BuildPayload(result As ReworkImportResult) As Dictionary(Of String, Object)
        Dim recordPayloads As New List(Of Dictionary(Of String, Object))()
        For Each record In result.Records
            recordPayloads.Add(New Dictionary(Of String, Object) From {
                {"operationDate", record.OperationDate.ToString("yyyy-MM-dd")},
                {"workCenter", record.WorkCenter},
                {"workCenterDescription", record.WorkCenterDescription},
                {"tourTemplate", record.TourTemplate},
                {"materialDescription", record.MaterialDescription},
                {"completedQuantity", record.CompletedQuantity},
                {"operationDescription", record.OperationDescription},
                {"sourceSheet", record.SourceSheet},
                {"sourceRowNumber", record.SourceRowNumber}
            })
        Next

        Return New Dictionary(Of String, Object) From {
            {"isSuccess", result.IsSuccess},
            {"statusMessage", result.StatusMessage},
            {"state", result.State},
            {"records", recordPayloads},
            {"generatedAt", DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")},
            {"generatedBy", If(AppState.CurrentUserName, "").Trim()}
        }
    End Function
End Class
