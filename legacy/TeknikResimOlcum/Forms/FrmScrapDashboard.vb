Imports System.Drawing
Imports System.IO
Imports System.Text
Imports System.Text.Json
Imports System.Windows.Forms
Imports Microsoft.Web.WebView2.Core
Imports Microsoft.Web.WebView2.WinForms

Public Class FrmScrapDashboard
    Inherits Form

    Private Const SlotScrap As String = "scrap"
    Private Const SlotProduction As String = "production"

    Private ReadOnly browser As New WebView2()
    Private bridgeInstalled As Boolean = False
    Private navigationHandlerAttached As Boolean = False
    Private restoreSentForCurrentPage As Boolean = False

    Public Sub New()
        AuthorizationService.Require(AppState.CanOpenScrapDashboard, "Hurda Dashboard")
        AppIconService.Apply(Me)

        Text = "Hurda Dashboard"
        StartPosition = FormStartPosition.CenterParent
        WindowState = FormWindowState.Maximized
        MinimumSize = New Size(1000, 650)
        BackColor = Color.White

        browser.Dock = DockStyle.Fill
        Controls.Add(browser)

        AddHandler Shown, AddressOf FrmScrapDashboard_Shown
    End Sub

    Private Async Sub FrmScrapDashboard_Shown(sender As Object, e As EventArgs)
        Await LoadDashboardAsync()
    End Sub

    Private Async Function LoadDashboardAsync() As Threading.Tasks.Task
        Try
            Dim userDataFolder = Path.Combine(AppPaths.LocalAppDataRoot, "WebView2", "ScrapDashboard")
            Directory.CreateDirectory(userDataFolder)

            Dim environment = Await CoreWebView2Environment.CreateAsync(Nothing, userDataFolder)
            Await browser.EnsureCoreWebView2Async(environment)
            browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = True
            browser.CoreWebView2.Settings.AreDevToolsEnabled = AppState.IsAdmin
            Await InstallPersistenceBridgeAsync()

            If Not navigationHandlerAttached Then
                AddHandler browser.CoreWebView2.NavigationCompleted, AddressOf Browser_NavigationCompleted
                navigationHandlerAttached = True
            End If

            Dim htmlPath = AppPaths.ScrapDashboardHtmlPath
            If Not File.Exists(htmlPath) Then
                browser.NavigateToString(BuildMessageHtml("Hurda dashboard dosyası bulunamadı: " & htmlPath))
                Return
            End If

            restoreSentForCurrentPage = False
            browser.CoreWebView2.Navigate(New Uri(htmlPath).AbsoluteUri)
        Catch ex As Exception
            ErrorLogService.Log("FrmScrapDashboard.LoadDashboardAsync", ex)
            Try
                browser.NavigateToString(BuildMessageHtml("Hurda dashboard açılamadı: " & ex.Message))
            Catch
            End Try
        End Try
    End Function

    Private Async Function InstallPersistenceBridgeAsync() As Threading.Tasks.Task
        If bridgeInstalled OrElse browser.CoreWebView2 Is Nothing Then Return

        AddHandler browser.CoreWebView2.WebMessageReceived, AddressOf Browser_WebMessageReceived
        Await browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(BuildPersistenceBridgeScript())
        bridgeInstalled = True
    End Function

    Private Sub Browser_NavigationCompleted(sender As Object, e As CoreWebView2NavigationCompletedEventArgs)
        If Not e.IsSuccess Then Return

        Try
            PostHostStateToBrowser(LoadPersistedState())
        Catch ex As Exception
            ErrorLogService.Log("FrmScrapDashboard.RestorePersistedDashboardFilesAsync", ex)
        End Try
    End Sub

    Private Async Sub Browser_WebMessageReceived(sender As Object, e As CoreWebView2WebMessageReceivedEventArgs)
        Try
            Using document = JsonDocument.Parse(e.WebMessageAsJson)
                Dim root = document.RootElement
                Dim messageType = GetJsonString(root, "type")

                If String.Equals(messageType, "scrap-dashboard-file-selected", StringComparison.OrdinalIgnoreCase) Then
                    SavePersistedDashboardFile(root)
                ElseIf String.Equals(messageType, "scrap-dashboard-ready", StringComparison.OrdinalIgnoreCase) Then
                    Await RestorePersistedDashboardFilesAsync()
                End If
            End Using
        Catch ex As Exception
            ErrorLogService.Log("FrmScrapDashboard.Browser_WebMessageReceived", ex)
        End Try
    End Sub

    Private Sub SavePersistedDashboardFile(root As JsonElement)
        Dim slot = NormalizeSlot(GetJsonString(root, "slot"))
        If String.IsNullOrWhiteSpace(slot) Then Return

        Dim fileName = CleanFileName(GetJsonString(root, "fileName"))
        If String.IsNullOrWhiteSpace(fileName) Then
            fileName = If(String.Equals(slot, SlotProduction, StringComparison.OrdinalIgnoreCase), "uretim.xlsx", "hurda.xlsx")
        End If

        Dim contentBase64 = GetJsonString(root, "contentBase64")
        If String.IsNullOrWhiteSpace(contentBase64) Then Return

        Directory.CreateDirectory(AppPaths.ScrapDashboardDataDir)

        Dim bytes = Convert.FromBase64String(contentBase64)
        Dim storedFileName = StoredFileNameForSlot(slot, fileName)
        RemoveOldStoredFilesForSlot(slot)

        Dim storedPath = Path.Combine(AppPaths.ScrapDashboardDataDir, storedFileName)
        File.WriteAllBytes(storedPath, bytes)

        Dim state = LoadPersistedState()
        Dim info As New PersistedDashboardFile With {
            .FileName = fileName,
            .StoredFileName = storedFileName,
            .SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            .SavedBy = If(AppState.CurrentUserName, "").Trim(),
            .ComputerName = Environment.MachineName,
            .LastModified = GetJsonInt64(root, "lastModified")
        }

        If String.Equals(slot, SlotProduction, StringComparison.OrdinalIgnoreCase) Then
            state.Production = info
        Else
            state.Scrap = info
        End If

        If state.History Is Nothing Then state.History = New List(Of PersistedDashboardHistory)()
        state.History.Insert(0, New PersistedDashboardHistory With {
            .Slot = slot,
            .FileName = info.FileName,
            .StoredFileName = info.StoredFileName,
            .SavedAt = info.SavedAt,
            .SavedBy = info.SavedBy,
            .ComputerName = info.ComputerName
        })

        If state.History.Count > 50 Then
            state.History = state.History.Take(50).ToList()
        End If

        SavePersistedState(state)
        PostHostStateToBrowser(state)
    End Sub

    Private Async Function RestorePersistedDashboardFilesAsync() As Threading.Tasks.Task
        If browser.CoreWebView2 Is Nothing Then Return
        If restoreSentForCurrentPage Then Return
        restoreSentForCurrentPage = True

        Dim state = LoadPersistedState()

        PostHostStateToBrowser(state)
        Await PostPersistedFileToBrowserAsync(SlotScrap, state.Scrap)
        Await PostPersistedFileToBrowserAsync(SlotProduction, state.Production)
    End Function

    Private Sub PostHostStateToBrowser(state As ScrapDashboardState)
        If browser.CoreWebView2 Is Nothing Then Return

        Try
            Dim payload As New Dictionary(Of String, Object)()
            payload("type") = "scrap-dashboard-host-state"
            payload("state") = state
            payload("products") = BuildProductReferencePayload()
            browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload))
        Catch ex As Exception
            ErrorLogService.Log("FrmScrapDashboard.PostHostStateToBrowser", ex)
        End Try
    End Sub

    Private Async Function PostPersistedFileToBrowserAsync(slot As String, info As PersistedDashboardFile) As Threading.Tasks.Task
        If browser.CoreWebView2 Is Nothing OrElse info Is Nothing Then Return

        Dim storedFileName = If(info.StoredFileName, "").Trim()
        If String.IsNullOrWhiteSpace(storedFileName) Then Return

        Dim storedPath = Path.Combine(AppPaths.ScrapDashboardDataDir, storedFileName)
        If Not File.Exists(storedPath) Then Return

        Dim payload = Await Threading.Tasks.Task.Run(Function()
                                                         Dim data As New Dictionary(Of String, Object)()
                                                         data("type") = "scrap-dashboard-restore-file"
                                                         data("slot") = slot
                                                         data("fileName") = If(info.FileName, storedFileName)
                                                         data("lastModified") = info.LastModified
                                                         data("contentBase64") = Convert.ToBase64String(File.ReadAllBytes(storedPath))
                                                         Return data
                                                     End Function)

        browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(payload))
    End Function

    Private Shared Function LoadPersistedState() As ScrapDashboardState
        Try
            If File.Exists(AppPaths.ScrapDashboardStateJson) Then
                Dim json = File.ReadAllText(AppPaths.ScrapDashboardStateJson, Encoding.UTF8)
                Dim state = JsonSerializer.Deserialize(Of ScrapDashboardState)(json)
                If state IsNot Nothing Then
                    If state.Scrap Is Nothing Then state.Scrap = New PersistedDashboardFile()
                    If state.Production Is Nothing Then state.Production = New PersistedDashboardFile()
                    If state.History Is Nothing Then state.History = New List(Of PersistedDashboardHistory)()
                    Return state
                End If
            End If
        Catch ex As Exception
            ErrorLogService.Log("FrmScrapDashboard.LoadPersistedState", ex)
        End Try

        Return New ScrapDashboardState()
    End Function

    Private Shared Sub SavePersistedState(state As ScrapDashboardState)
        Directory.CreateDirectory(AppPaths.ScrapDashboardDataDir)

        Dim options As New JsonSerializerOptions With {.WriteIndented = True}
        File.WriteAllText(AppPaths.ScrapDashboardStateJson, JsonSerializer.Serialize(state, options), Encoding.UTF8)
    End Sub

    Private Shared Function NormalizeSlot(slot As String) As String
        Dim value = If(slot, "").Trim().ToLowerInvariant()
        If String.Equals(value, SlotProduction, StringComparison.OrdinalIgnoreCase) Then Return SlotProduction
        If String.Equals(value, SlotScrap, StringComparison.OrdinalIgnoreCase) Then Return SlotScrap
        Return ""
    End Function

    Private Shared Function StoredFileNameForSlot(slot As String, originalFileName As String) As String
        Dim extension = Path.GetExtension(If(originalFileName, "")).Trim().ToLowerInvariant()
        If Not {".xlsx", ".xls", ".xlsm"}.Contains(extension) Then extension = ".xlsx"

        If String.Equals(slot, SlotProduction, StringComparison.OrdinalIgnoreCase) Then
            Return "LastProductionData" & extension
        End If

        Return "LastScrapData" & extension
    End Function

    Private Shared Sub RemoveOldStoredFilesForSlot(slot As String)
        Try
            Dim prefix = If(String.Equals(slot, SlotProduction, StringComparison.OrdinalIgnoreCase), "LastProductionData.", "LastScrapData.")
            If Not Directory.Exists(AppPaths.ScrapDashboardDataDir) Then Return

            For Each path In Directory.GetFiles(AppPaths.ScrapDashboardDataDir, prefix & "*")
                File.Delete(path)
            Next
        Catch ex As Exception
            ErrorLogService.Log("FrmScrapDashboard.RemoveOldStoredFilesForSlot", ex)
        End Try
    End Sub

    Private Shared Function CleanFileName(fileName As String) As String
        Dim value = Path.GetFileName(If(fileName, "").Trim())
        If String.IsNullOrWhiteSpace(value) Then Return ""

        For Each invalidChar In Path.GetInvalidFileNameChars()
            value = value.Replace(invalidChar, "_"c)
        Next

        Return value
    End Function

    Private Shared Function GetJsonString(root As JsonElement, propertyName As String) As String
        Dim value As JsonElement
        If root.TryGetProperty(propertyName, value) AndAlso value.ValueKind <> JsonValueKind.Null Then
            Return value.ToString()
        End If

        Return ""
    End Function

    Private Shared Function GetJsonInt64(root As JsonElement, propertyName As String) As Long
        Dim value As JsonElement
        If root.TryGetProperty(propertyName, value) Then
            Dim result As Long
            If value.ValueKind = JsonValueKind.Number AndAlso value.TryGetInt64(result) Then Return result
            If Long.TryParse(value.ToString(), result) Then Return result
        End If

        Return 0
    End Function

    Private Shared Function BuildProductReferencePayload() As List(Of Dictionary(Of String, Object))
        Dim items As New List(Of Dictionary(Of String, Object))()

        Try
            For Each product In DataService.GetProducts(False)
                Dim item As New Dictionary(Of String, Object)()
                item("trCode") = If(product.TrCode, "").Trim()
                item("productName") = If(product.ProductName, "").Trim()
                item("drawingScope") = ProductInfo.NormalizeDrawingScope(product.DrawingScope)
                item("drawingRev") = If(product.DrawingRev, "").Trim()
                item("drawingFile") = If(product.DrawingFile, "").Trim()
                item("isActive") = If(product.IsActive, "").Trim()
                items.Add(item)
            Next
        Catch ex As Exception
            ErrorLogService.Log("FrmScrapDashboard.BuildProductReferencePayload", ex)
        End Try

        Return items
    End Function

    Private Shared Function BuildPersistenceBridgeScript() As String
        Return <![CDATA[
(function(){
  if (window.__tekScrapDashboardBridgeInstalled) return;
  window.__tekScrapDashboardBridgeInstalled = true;

  var hostState = { state: null, products: [] };
  var lastIssueCount = null;
  var readyMessageSent = false;

  function hasHost(){
    return !!(window.chrome && window.chrome.webview);
  }

  function bytesToBase64(bytes){
    var binary = "";
    var chunk = 0x8000;
    for (var i = 0; i < bytes.length; i += chunk){
      binary += String.fromCharCode.apply(null, bytes.subarray(i, i + chunk));
    }
    return btoa(binary);
  }

  function base64ToBytes(base64){
    var binary = atob(base64 || "");
    var bytes = new Uint8Array(binary.length);
    for (var i = 0; i < binary.length; i++){
      bytes[i] = binary.charCodeAt(i);
    }
    return bytes;
  }

  function markSavedFile(input, fileName, restored){
    if (!input || !input.parentElement) return;
    var note = input.parentElement.querySelector(".tek-scrap-saved-note");
    if (!note){
      note = document.createElement("div");
      note.className = "tek-scrap-saved-note";
      note.style.marginTop = "4px";
      note.style.fontSize = "11px";
      note.style.fontWeight = "700";
      note.style.color = "#1d4ed8";
      input.parentElement.appendChild(note);
    }
    note.textContent = (restored ? "Otomatik yüklenen dosya: " : "Kaydedilen dosya: ") + (fileName || "");
  }

  function esc(value){
    return String(value == null ? "" : value).replace(/[&<>"']/g, function(ch){
      return ({ "&":"&amp;", "<":"&lt;", ">":"&gt;", '"':"&quot;", "'":"&#39;" })[ch];
    });
  }

  function pick(obj){
    if (!obj) return null;
    for (var i = 1; i < arguments.length; i++){
      var key = arguments[i];
      if (obj[key] !== undefined && obj[key] !== null) return obj[key];
    }
    return null;
  }

  function fmtSaved(info){
    if (!info) return "Henüz yok";
    var file = pick(info, "FileName", "fileName") || "—";
    var savedAt = pick(info, "SavedAt", "savedAt") || "—";
    var savedBy = pick(info, "SavedBy", "savedBy") || "";
    return file + " | " + savedAt + (savedBy ? " | " + savedBy : "");
  }

  function productToken(value){
    try{
      if (window.canonicalProductToken) return window.canonicalProductToken(value) || "";
      var helpers = window.__HURDA_HELPERS__ || {};
      if (helpers.canonicalProductToken) return helpers.canonicalProductToken(value) || "";
    }catch(_){}
    var text = String(value || "").toUpperCase().replace(/\u00A0/g, " ").replace(/[‐‑‒–—−]/g, "-").trim();
    if (!text) return "";

    function makeCode(base, suffix){
      var parts = [];
      function push(v){
        var p = String(v || "").replace(/^0+(?=\d)/, "").replace(/[^0-9A-Z]+/g, "").trim();
        if (p) parts.push(p);
      }
      push(base);
      if (suffix){
        var tokens = String(suffix).match(/[0-9A-Z]{1,4}/g) || [];
        tokens.forEach(push);
      }
      return parts.join("-");
    }

    var trPair = text.match(/\bT\s*R\s*(?:NO|NUMARA|NUMARASI|KODU|KOD|CODE)?\s*[\s.\-_:;\/\\]*([0-9]{2,})(?:\s*[\-_ .]\s*([0-9A-Z]{1,4}))?\b/);
    var pPair = text.match(/(?:^|[^A-Z0-9])P\s*[\s.\-_:;\/\\]*([0-9]{2,})(?:\s*[\-\/_ .]\s*([0-9A-Z]{1,4}))?(?=$|[^A-Z0-9])/);
    if (trPair && pPair){
      var trCode = makeCode(trPair[1], trPair[2]);
      var pCode = makeCode(pPair[1], pPair[2]);
      if (trCode && pCode) return "TR " + trCode + " / P " + pCode;
    }

    var match = text.match(/\bT\s*B\s*[\.\-_\s]*M\s*K\s*Z\s*[\s.\-_:;\/\\]*([0-9]{2,})((?:\s*[\-\/_ .]\s*[0-9A-Z]{1,4})*)\b/);
    if (match) {
      var tbCode = makeCode(match[1], match[2]);
      return tbCode ? ("TB.MKZ." + tbCode) : "";
    }

    match = pPair;
    if (match) {
      var pOnlyCode = makeCode(match[1], match[2]);
      return pOnlyCode ? ("P " + pOnlyCode) : "";
    }

    match = trPair || text.match(/\b(?:TUR\s*SABLONU|TUR|TR\s*NO|TR\s*KODU|TR\s*KOD|TR\s*CODE)\s*[\s.\-_:;\/\\]*([0-9]{2,})(?:\s*[\-\/_ .]\s*([0-9A-Z]{1,4}))?\b/);
    if (match) {
      var trOnlyCode = makeCode(match[1], match[2]);
      return trOnlyCode ? ("TR " + trOnlyCode) : "";
    }

    match = text.match(/^\s*([0-9]{2,})(?:\s*[\-\/_ .]\s*([0-9A-Z]{1,4}))?\s*$/);
    if (match) {
      var bareCode = makeCode(match[1], match[2]);
      return bareCode ? ("TR " + bareCode) : "";
    }

    match = text.match(/(?:^|[^0-9A-Z])([0-9]{2,})(?:\s*[\-\/_ .]\s*([0-9A-Z]{1,4}))?(?=$|[^0-9A-Z])/);
    if (match && (!/^(19|20)\d{2}$/.test(match[1]) || match[2])){
      var legacyCode = makeCode(match[1], match[2]);
      return legacyCode ? ("TR " + legacyCode) : "";
    }

    return "";
  }

  function getKnownProductTokens(){
    var set = new Set();
    (hostState.products || []).forEach(function(product){
      var active = String(pick(product, "isActive", "IsActive") || "").toUpperCase();
      if (active === "NO" || active === "FALSE" || active === "0") return;

      [pick(product, "trCode", "TrCode"), pick(product, "productName", "ProductName")].forEach(function(value){
        var token = productToken(value);
        if (token) set.add(token);
      });
    });
    return set;
  }

  function getInternalRows(){
    try{
      var internal = window.__HURDA_INTERNAL__ || {};
      var rows = internal.getROWS ? internal.getROWS() : [];
      return Array.isArray(rows) ? rows : [];
    }catch(_){
      return [];
    }
  }

  function getInternalProdRows(){
    try{
      var internal = window.__HURDA_INTERNAL__ || {};
      var rows = internal.getROWS_PROD ? internal.getROWS_PROD() : [];
      return Array.isArray(rows) ? rows : [];
    }catch(_){
      return [];
    }
  }

  function selectedProductField(){
    var el = document.getElementById("urunField");
    return el ? (el.value || "") : "";
  }

  function findRowProductToken(row, field){
    if (!row) return "";
    if (field && row[field] != null){
      var fieldToken = productToken(row[field]);
      if (fieldToken) return fieldToken;
    }

    for (var key in row){
      if (!Object.prototype.hasOwnProperty.call(row, key)) continue;
      var token = productToken(row[key]);
      if (token) return token;
    }

    return "";
  }

  function findRowProductText(row, field){
    if (!row) return "";
    if (field && row[field] != null && String(row[field]).trim()) return String(row[field]).trim();

    var candidates = ["Malzeme Açıklaması", "Malzeme Aciklamasi", "TR Kodu", "Ürün", "Urun", "İş Emri Malzeme Tanımı", "Is Emri Malzeme Tanimi"];
    for (var i = 0; i < candidates.length; i++){
      var value = row[candidates[i]];
      if (value != null && String(value).trim()) return String(value).trim();
    }

    return "";
  }

  function computeMissingIssues(){
    var rows = getInternalRows();
    var known = getKnownProductTokens();
    var field = selectedProductField();
    var aggregate = new Map();

    rows.forEach(function(row){
      var token = findRowProductToken(row, field);
      var productText = findRowProductText(row, field);
      var reason = "";

      if (!token){
        reason = "TR / ürün kodu yakalanamadı";
      } else if (!known.has(token)){
        reason = "Ürün / Teknik Resim Yönetimi'nde aktif kayıt yok";
      }

      if (!reason) return;
      var key = reason + "|" + (token || "—") + "|" + (productText || "—");
      var item = aggregate.get(key);
      if (!item){
        item = { reason: reason, token: token || "—", product: productText || "—", count: 0 };
        aggregate.set(key, item);
      }
      item.count++;
    });

    return Array.from(aggregate.values()).sort(function(a, b){
      return b.count - a.count || String(a.token).localeCompare(String(b.token), "tr");
    });
  }

  function ensureInfoPanel(){
    var existing = document.getElementById("tekScrapInfoPanel");
    if (existing) return existing;

    var panel = document.createElement("section");
    panel.id = "tekScrapInfoPanel";
    panel.style.cssText = "margin:10px 16px 14px 16px;padding:10px 12px;border:1px solid #dbeafe;background:#f8fbff;border-radius:12px;font-family:Segoe UI,Arial,sans-serif;color:#0f172a;box-shadow:0 1px 2px rgba(15,23,42,.04)";

    var insertBefore = document.querySelector("main") || document.body.firstChild;
    document.body.insertBefore(panel, insertBefore);
    return panel;
  }

  function renderInfoPanel(){
    var panel = ensureInfoPanel();
    var state = hostState.state || {};
    var scrap = pick(state, "Scrap", "scrap");
    var production = pick(state, "Production", "production");
    var issues = computeMissingIssues();
    var rows = getInternalRows();
    var prodRows = getInternalProdRows();
    lastIssueCount = issues.length;

    panel.innerHTML =
      '<div style="display:grid;grid-template-columns:1.35fr 1.35fr .7fr .7fr auto auto auto;gap:10px;align-items:stretch">' +
        '<div style="padding:8px 10px;background:#fff;border:1px solid #e5e7eb;border-radius:10px"><div style="font-size:11px;color:#64748b;font-weight:700">SON HURDA DOSYASI</div><div style="font-size:13px;font-weight:700;white-space:nowrap;overflow:hidden;text-overflow:ellipsis" title="'+esc(fmtSaved(scrap))+'">'+esc(fmtSaved(scrap))+'</div></div>' +
        '<div style="padding:8px 10px;background:#fff;border:1px solid #e5e7eb;border-radius:10px"><div style="font-size:11px;color:#64748b;font-weight:700">SON ÜRETİM DOSYASI</div><div style="font-size:13px;font-weight:700;white-space:nowrap;overflow:hidden;text-overflow:ellipsis" title="'+esc(fmtSaved(production))+'">'+esc(fmtSaved(production))+'</div></div>' +
        '<div style="padding:8px 10px;background:#fff;border:1px solid #e5e7eb;border-radius:10px;text-align:center"><div style="font-size:11px;color:#64748b;font-weight:700">HURDA SATIRI</div><div style="font-size:18px;font-weight:800;color:#1d4ed8">'+rows.length+'</div></div>' +
        '<div style="padding:8px 10px;background:#fff;border:1px solid #e5e7eb;border-radius:10px;text-align:center"><div style="font-size:11px;color:#64748b;font-weight:700">ÜRETİM SATIRI</div><div style="font-size:18px;font-weight:800;color:#047857">'+prodRows.length+'</div></div>' +
        '<button type="button" id="tekScrapValidationBtn" style="border:1px solid #0f766e;background:#ecfdf5;color:#115e59;border-radius:10px;font-weight:800;padding:8px 12px;min-width:150px">Veri Doğrulama</button>' +
        '<button type="button" id="tekScrapHistoryBtn" style="border:1px solid #1d4ed8;background:#fff;color:#1d4ed8;border-radius:10px;font-weight:800;padding:8px 12px;min-width:140px">Yükleme Geçmişi</button>' +
        '<button type="button" id="tekScrapMissingBtn" style="border:1px solid '+(issues.length ? '#dc2626' : '#16a34a')+';background:'+(issues.length ? '#fee2e2' : '#dcfce7')+';color:'+(issues.length ? '#991b1b' : '#166534')+';border-radius:10px;font-weight:800;padding:8px 12px;min-width:190px">Eksik / Eşleşmeyen: '+issues.length+'</button>' +
      '</div>';

    var validationBtn = document.getElementById("tekScrapValidationBtn");
    if (validationBtn) validationBtn.onclick = showValidationPanel;

    var historyBtn = document.getElementById("tekScrapHistoryBtn");
    if (historyBtn) historyBtn.onclick = showHistoryModal;

    var missingBtn = document.getElementById("tekScrapMissingBtn");
    if (missingBtn) missingBtn.onclick = showMissingModal;
  }

  function ensureModal(){
    var modal = document.getElementById("tekScrapModal");
    if (modal) return modal;

    modal = document.createElement("div");
    modal.id = "tekScrapModal";
    modal.style.cssText = "display:none;position:fixed;inset:0;z-index:999999;background:rgba(15,23,42,.45);align-items:center;justify-content:center;padding:24px";
    modal.innerHTML = '<div style="width:min(1100px,96vw);max-height:86vh;overflow:auto;background:#fff;border-radius:14px;border:1px solid #cbd5e1;box-shadow:0 20px 50px rgba(0,0,0,.25)"><div style="display:flex;justify-content:space-between;align-items:center;padding:14px 16px;background:#1f4e84;color:#fff;border-radius:14px 14px 0 0"><div id="tekScrapModalTitle" style="font-weight:800;font-size:16px"></div><button id="tekScrapModalClose" type="button" style="background:#fff;color:#1f4e84;border:0;border-radius:8px;padding:6px 12px;font-weight:800">Kapat</button></div><div id="tekScrapModalBody" style="padding:14px 16px"></div></div>';
    document.body.appendChild(modal);
    modal.querySelector("#tekScrapModalClose").onclick = function(){ modal.style.display = "none"; };
    modal.addEventListener("click", function(event){ if (event.target === modal) modal.style.display = "none"; });
    return modal;
  }

  function showModal(title, bodyHtml){
    var modal = ensureModal();
    modal.querySelector("#tekScrapModalTitle").textContent = title;
    modal.querySelector("#tekScrapModalBody").innerHTML = bodyHtml;
    modal.style.display = "flex";
  }

  function showHistoryModal(){
    var state = hostState.state || {};
    var history = pick(state, "History", "history") || [];
    var rowsHtml = history.length ? history.map(function(item){
      var slot = String(pick(item, "Slot", "slot") || "") === "production" ? "Üretim" : "Hurda";
      return '<tr><td>'+esc(slot)+'</td><td>'+esc(pick(item, "FileName", "fileName") || "")+'</td><td>'+esc(pick(item, "SavedAt", "savedAt") || "")+'</td><td>'+esc(pick(item, "SavedBy", "savedBy") || "")+'</td><td>'+esc(pick(item, "ComputerName", "computerName") || "")+'</td></tr>';
    }).join("") : '<tr><td colspan="5" style="color:#64748b">Henüz yükleme geçmişi yok.</td></tr>';

    showModal("Hurda Dashboard - Yükleme Geçmişi",
      '<table style="width:100%;border-collapse:collapse;font-size:13px"><thead><tr style="background:#eaf1ff"><th style="text-align:left;padding:8px;border:1px solid #cbd5e1">Tip</th><th style="text-align:left;padding:8px;border:1px solid #cbd5e1">Dosya</th><th style="text-align:left;padding:8px;border:1px solid #cbd5e1">Yükleme Tarihi</th><th style="text-align:left;padding:8px;border:1px solid #cbd5e1">Yükleyen</th><th style="text-align:left;padding:8px;border:1px solid #cbd5e1">Bilgisayar</th></tr></thead><tbody>'+rowsHtml+'</tbody></table>');
  }

  function showValidationPanel(){
    try{
      if (typeof window.__renderValidationPanel === "function") window.__renderValidationPanel();
      var panel = document.getElementById("secValidation");
      if (panel){
        try{ panel.scrollIntoView({ behavior: "smooth", block: "start" }); }
        catch(_){ panel.scrollIntoView(true); }
        panel.style.outline = "3px solid #0f766e";
        panel.style.outlineOffset = "3px";
        setTimeout(function(){ panel.style.outline = ""; panel.style.outlineOffset = ""; }, 1800);
        return;
      }
      showModal("Veri Doğrulama", '<div style="color:#92400e;font-weight:700">Veri Doğrulama paneli bu dashboard dosyasında bulunamadı.</div>');
    }catch(err){
      showModal("Veri Doğrulama", '<div style="color:#991b1b;font-weight:700">Doğrulama paneli açılamadı.</div><pre style="white-space:pre-wrap">'+esc(err && err.message ? err.message : err)+'</pre>');
    }
  }

  function showMissingModal(){
    var issues = computeMissingIssues();
    var rowsHtml = issues.length ? issues.map(function(item){
      return '<tr><td style="padding:8px;border:1px solid #cbd5e1;font-weight:700">'+esc(item.reason)+'</td><td style="padding:8px;border:1px solid #cbd5e1">'+esc(item.token)+'</td><td style="padding:8px;border:1px solid #cbd5e1">'+esc(item.product)+'</td><td style="padding:8px;border:1px solid #cbd5e1;text-align:right">'+item.count+'</td></tr>';
    }).join("") : '<tr><td colspan="4" style="padding:10px;border:1px solid #cbd5e1;color:#166534;font-weight:800">Eksik veya eşleşmeyen ürün görünmüyor.</td></tr>';

    showModal("Eksik / Eşleşmeyen Ürünler",
      '<div style="margin-bottom:10px;color:#475569">Bu kontrol Hurda dosyasındaki yakalanan TR/ürün kodlarını programın Ürün / Teknik Resim Yönetimi kayıtları ile karşılaştırır.</div>' +
      '<table style="width:100%;border-collapse:collapse;font-size:13px"><thead><tr style="background:#fff7ed"><th style="text-align:left;padding:8px;border:1px solid #cbd5e1">Sebep</th><th style="text-align:left;padding:8px;border:1px solid #cbd5e1">TR / Kod</th><th style="text-align:left;padding:8px;border:1px solid #cbd5e1">Ürün Alanı</th><th style="text-align:right;padding:8px;border:1px solid #cbd5e1">Satır</th></tr></thead><tbody>'+rowsHtml+'</tbody></table>');
  }

  function schedulePanelRefresh(){
    clearTimeout(window.__tekScrapPanelTimer);
    window.__tekScrapPanelTimer = setTimeout(function(){
      try{ renderInfoPanel(); }catch(err){ console.error("Hurda panel guncellenemedi", err); }
    }, 650);
  }

  async function sendFileToHost(slot, input){
    try{
      if (!hasHost() || !input || !input.files || input.files.length === 0) return;
      var file = input.files[0];
      var buffer = await file.arrayBuffer();
      var payload = {
        type: "scrap-dashboard-file-selected",
        slot: slot,
        fileName: file.name,
        lastModified: file.lastModified || 0,
        contentBase64: bytesToBase64(new Uint8Array(buffer))
      };
      window.chrome.webview.postMessage(payload);
      markSavedFile(input, file.name, false);
      schedulePanelRefresh();
    }catch(err){
      console.error("Hurda dashboard dosyasi kaydedilemedi", err);
    }
  }

  function attachFileHandlers(){
    var scrapInput = document.getElementById("file");
    var productionInput = document.getElementById("fileProd");

    if (scrapInput && !scrapInput.__tekPersistHooked){
      scrapInput.__tekPersistHooked = true;
      scrapInput.addEventListener("change", function(){
        if (scrapInput.__tekRestoringFile) return;
        sendFileToHost("scrap", scrapInput);
      });
    }

    if (productionInput && !productionInput.__tekPersistHooked){
      productionInput.__tekPersistHooked = true;
      productionInput.addEventListener("change", function(){
        if (productionInput.__tekRestoringFile) return;
        sendFileToHost("production", productionInput);
      });
    }
  }

  function restoreFile(message){
    try{
      if (!message || message.type !== "scrap-dashboard-restore-file") return;

      var input = document.getElementById(message.slot === "production" ? "fileProd" : "file");
      if (!input || !message.contentBase64) return;

      var bytes = base64ToBytes(message.contentBase64);
      var file = new File([bytes], message.fileName || "veri.xlsx", {
        lastModified: message.lastModified || Date.now()
      });
      var dataTransfer = new DataTransfer();
      dataTransfer.items.add(file);
      input.__tekRestoringFile = true;
      input.files = dataTransfer.files;
      markSavedFile(input, file.name, true);
      input.dispatchEvent(new Event("change", { bubbles: true }));
      schedulePanelRefresh();
      setTimeout(function(){ input.__tekRestoringFile = false; }, 0);
    }catch(err){
      console.error("Hurda dashboard dosyasi geri yuklenemedi", err);
    }
  }

  function notifyDashboardReady(){
    attachFileHandlers();
    schedulePanelRefresh();
    window.__tekScrapDashboardBridgeReady = true;
    if (!readyMessageSent && hasHost()){
      readyMessageSent = true;
      try{ window.chrome.webview.postMessage({ type: "scrap-dashboard-ready" }); }catch(_){}
    }
  }

  document.addEventListener("DOMContentLoaded", notifyDashboardReady);
  window.addEventListener("load", notifyDashboardReady);
  if (document.readyState === "interactive" || document.readyState === "complete"){
    notifyDashboardReady();
  }

  if (hasHost()){
    window.chrome.webview.addEventListener("message", function(event){
      var message = event.data || {};
      if (message.type === "scrap-dashboard-host-state"){
        hostState.state = message.state || {};
        hostState.products = message.products || [];
        schedulePanelRefresh();
        return;
      }
      restoreFile(message);
    });
  }

  setInterval(function(){
    try{
      var issues = computeMissingIssues();
      if (lastIssueCount === null || lastIssueCount !== issues.length){
        schedulePanelRefresh();
      }
    }catch(_){}
  }, 3500);
})();
]]>.Value
    End Function

    Private NotInheritable Class PersistedDashboardFile
        Public Property FileName As String = ""
        Public Property StoredFileName As String = ""
        Public Property SavedAt As String = ""
        Public Property SavedBy As String = ""
        Public Property ComputerName As String = ""
        Public Property LastModified As Long = 0
    End Class

    Private NotInheritable Class ScrapDashboardState
        Public Property Scrap As PersistedDashboardFile = New PersistedDashboardFile()
        Public Property Production As PersistedDashboardFile = New PersistedDashboardFile()
        Public Property History As List(Of PersistedDashboardHistory) = New List(Of PersistedDashboardHistory)()
    End Class

    Private NotInheritable Class PersistedDashboardHistory
        Public Property Slot As String = ""
        Public Property FileName As String = ""
        Public Property StoredFileName As String = ""
        Public Property SavedAt As String = ""
        Public Property SavedBy As String = ""
        Public Property ComputerName As String = ""
    End Class

    Private Shared Function BuildMessageHtml(message As String) As String
        Dim encoded = System.Net.WebUtility.HtmlEncode(If(message, ""))
        Dim html As New StringBuilder()
        html.AppendLine("<!doctype html><html lang=""tr""><head><meta charset=""utf-8"">")
        html.AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;background:#f4f7fb;margin:0;padding:32px;color:#0f172a}.box{background:#fff;border:1px solid #dbe3ef;padding:22px;border-radius:8px;max-width:760px}.title{font-size:18px;font-weight:700;margin-bottom:10px}</style>")
        html.AppendLine("</head><body><div class=""box""><div class=""title"">Hurda Dashboard</div><div>")
        html.Append(encoded)
        html.AppendLine("</div></div></body></html>")
        Return html.ToString()
    End Function
End Class
