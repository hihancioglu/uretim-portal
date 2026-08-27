const shell = document.querySelector("#drawing-viewer");
const overlay = shell?.querySelector('[data-role="overlay"]');
const panel = document.querySelector('[data-role="control-point-panel"]');
const form = panel?.querySelector('[data-role="cp-form"]');
const copyForm = panel?.querySelector('[data-role="cp-copy-form"]');
const message = panel?.querySelector('[data-role="cp-message"]');
const details = panel?.querySelector('[data-role="cp-details"]');
let currentPage = 1;
let placementMode = false;
let selectedPoint = null;
let submitUrl = shell?.dataset.controlPointCreateUrl;

function csrfToken() { return form?.querySelector('[name="csrfmiddlewaretoken"]')?.value; }
function showMessage(value, error = false) { message.textContent = value; message.classList.toggle("cp-error", error); }
function textRow(parent, label, value) { const row = document.createElement("p"); const strong = document.createElement("strong"); strong.textContent = `${label}: `; row.append(strong, document.createTextNode(value ?? "—")); parent.append(row); }
function setCoordinates(page, x, y) {
  form.elements.page_no.value = String(page); form.elements.x_ratio.value = x; form.elements.y_ratio.value = y;
  form.querySelector('[data-role="cp-coordinate"]').textContent = `Sayfa ${page} · x=${x} · y=${y}`;
}
function startPlacement(reposition = false) { placementMode = true; overlay.classList.add("placement-mode"); form.hidden = !reposition; showMessage("PDF üzerinde bir konum seçin."); }
function cancel() { placementMode = false; overlay.classList.remove("placement-mode"); form.hidden = true; selectedPoint = null; submitUrl = shell.dataset.controlPointCreateUrl; }
function populate(point) {
  for (const name of ["measure_code", "measure_name", "nominal", "lower_tolerance", "upper_tolerance", "unit", "measurement_group", "sample_frequency", "sort_no", "change_reason"]) form.elements[name].value = point[name] ?? "";
  form.elements.is_mandatory.checked = point.is_mandatory; form.elements.is_critical.checked = point.is_critical;
  setCoordinates(point.page_no, point.x_ratio, point.y_ratio);
}
async function requestJson(url, options = {}) { const response = await fetch(url, {credentials: "same-origin", ...options}); const data = await response.json(); if (!response.ok) throw new Error(data.error || "İşlem tamamlanamadı."); return data; }
async function loadPoints() {
  const data = await requestJson(`${shell.dataset.controlPointsUrl}?page=${currentPage}`);
  overlay.querySelectorAll(".control-point-marker").forEach((item) => item.remove());
  data.points.forEach((point) => {
    const marker = document.createElement("button"); marker.type = "button"; marker.className = "control-point-marker";
    marker.style.left = `${point.x_ratio * 100}%`; marker.style.top = `${point.y_ratio * 100}%`;
    marker.textContent = point.measure_code; marker.title = `Kontrol noktası ${point.measure_code} — ${point.measure_name}`; marker.setAttribute("aria-label", marker.title);
    marker.addEventListener("click", (event) => { event.stopPropagation(); openDetails(point.control_point_id); }); overlay.append(marker);
  });
}
async function openDetails(pointId) {
  try {
    const data = await requestJson(`${shell.dataset.controlPointsUrl}${pointId}/`); selectedPoint = data.active; details.replaceChildren();
    if (data.active) {
      const p = data.active; [["Ölçü kodu", p.measure_code], ["Ölçü adı", p.measure_name], ["Versiyon", p.version_no], ["Nominal", `${p.nominal} ${p.unit}`], ["Alt tolerans", p.lower_tolerance], ["Üst tolerans", p.upper_tolerance], ["Alt limit", p.lower_limit], ["Üst limit", p.upper_limit], ["Zorunlu", p.is_mandatory ? "Evet" : "Hayır"], ["Grup", p.measurement_group], ["Örnekleme", p.sample_frequency], ["Kritik", p.is_critical ? "Evet" : "Hayır"], ["Sayfa", p.page_no]].forEach(([a,b]) => textRow(details,a,b));
      if (shell.dataset.canManage === "true") {
        const edit = document.createElement("button"); edit.type="button"; edit.textContent="Düzenle / Revize et"; edit.onclick=()=>{ populate(p); submitUrl=`${shell.dataset.controlPointsUrl}${pointId}/revise/`; form.hidden=false; };
        const reposition = document.createElement("button"); reposition.type="button"; reposition.textContent="Konumu yeniden seç"; reposition.onclick=()=>{ populate(p); submitUrl=`${shell.dataset.controlPointsUrl}${pointId}/revise/`; startPlacement(true); };
        const deactivate = document.createElement("button"); deactivate.type="button"; deactivate.textContent="Bu revizyondan pasife al"; deactivate.onclick=()=>deactivatePoint(pointId);
        details.append(edit, reposition, deactivate);
      }
    }
    const heading=document.createElement("h3"); heading.textContent="Versiyon geçmişi"; const table=document.createElement("table");
    data.history.forEach((v)=>{ const row=document.createElement("tr"); [v.version_no,v.measure_code,v.revision,v.is_active?"Aktif":"Pasif",v.valid_from||"—",v.valid_to||"—",v.change_reason||"—",v.created_at,v.created_by].forEach(value=>{const cell=document.createElement("td");cell.textContent=value;row.append(cell);});table.append(row); }); details.append(heading,table);
    showMessage(`SPC kimliği: ${data.spc_key}`);
  } catch (error) { showMessage(error.message, true); }
}
async function deactivatePoint(pointId) { try { await requestJson(`${shell.dataset.controlPointsUrl}${pointId}/deactivate/`, {method:"POST", headers:{"X-CSRFToken":csrfToken()}}); details.replaceChildren(); showMessage("Kontrol noktası bu revizyonda pasife alındı."); await loadPoints(); } catch(error){ showMessage(error.message,true); } }
shell?.addEventListener("drawingviewer:rendered", async (event) => { currentPage = event.detail.pageNumber; try { await loadPoints(); } catch(error) { showMessage(error.message,true); } });
overlay?.addEventListener("click", (event) => {
  if (!placementMode || event.target !== overlay) return;
  const rect = overlay.getBoundingClientRect();
  const clamp = (value) => Math.min(1, Math.max(0, value));
  const xRatio = clamp((event.clientX - rect.left) / rect.width).toFixed(6);
  const yRatio = clamp((event.clientY - rect.top) / rect.height).toFixed(6);
  setCoordinates(currentPage, xRatio, yRatio); placementMode=false; overlay.classList.remove("placement-mode"); form.hidden=false; showMessage("Tanımı tamamlayıp kaydedin.");
});
document.querySelector('[data-cp-action="place"]')?.addEventListener("click", () => { form.reset(); form.elements.unit.value="mm"; form.elements.measurement_group.value="Genel"; form.elements.sample_frequency.value="Her Kontrol"; form.elements.is_mandatory.checked=true; submitUrl=shell.dataset.controlPointCreateUrl; startPlacement(); });
document.querySelector('[data-cp-action="cancel"]')?.addEventListener("click", cancel);
form?.addEventListener("submit", async (event) => { event.preventDefault(); try { const body=new FormData(form); if (!form.elements.is_mandatory.checked) body.delete("is_mandatory"); if (!form.elements.is_critical.checked) body.delete("is_critical"); await requestJson(submitUrl,{method:"POST",body,headers:{"X-CSRFToken":csrfToken()}}); cancel(); showMessage("Kontrol noktası kaydedildi."); await loadPoints(); } catch(error){ showMessage(error.message,true); } });

copyForm?.addEventListener("submit", async (event) => {
  event.preventDefault();
  try {
    const body = new FormData(copyForm);
    await requestJson(shell.dataset.controlPointCopyUrl, {method: "POST", body, headers: {"X-CSRFToken": csrfToken()}});
    showMessage("Kontrol noktaları taslak revizyona kopyalandı.");
    await loadPoints();
  } catch (error) { showMessage(error.message, true); }
});
