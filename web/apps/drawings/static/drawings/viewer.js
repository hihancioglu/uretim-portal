const shell = document.querySelector("#drawing-viewer");
const viewport = shell.querySelector('[data-role="viewport"]');
const stage = shell.querySelector('[data-role="page-stage"]');
const canvas = shell.querySelector('[data-role="canvas"]');
const overlay = shell.querySelector('[data-role="overlay"]');
const status = shell.querySelector('[data-role="status"]');
const pageInput = shell.querySelector('[data-role="page"]');
const pageCount = shell.querySelector('[data-role="page-count"]');
const zoomText = shell.querySelector('[data-role="zoom"]');
const MIN_SCALE = 0.25;
const MAX_SCALE = 5;
let documentHandle;
let pageNumber = 1;
let scale = 1;
let mode = "actual";
let renderTask;
let generation = 0;

window.pdfjsLib.GlobalWorkerOptions.workerSrc = shell.dataset.workerUrl;

function clampScale(value) { return Math.min(MAX_SCALE, Math.max(MIN_SCALE, value)); }
function showError(message = "Teknik resim görüntülenemedi.") {
  status.textContent = message;
  status.classList.add("viewer-error");
}
async function calculateScale(page) {
  const unit = page.getViewport({ scale: 1 });
  if (mode === "fit-width") return clampScale((viewport.clientWidth - 32) / unit.width);
  if (mode === "fit-page") return clampScale(Math.min((viewport.clientWidth - 32) / unit.width, (viewport.clientHeight - 32) / unit.height));
  return scale;
}
async function renderPage() {
  if (!documentHandle) return;
  const requested = ++generation;
  if (renderTask) renderTask.cancel();
  status.hidden = false;
  status.textContent = "Sayfa hazırlanıyor…";
  try {
    const page = await documentHandle.getPage(pageNumber);
    const logicalScale = await calculateScale(page);
    scale = logicalScale;
    const cssViewport = page.getViewport({ scale: logicalScale });
    const ratio = Math.min(window.devicePixelRatio || 1, 3);
    const outputViewport = page.getViewport({ scale: logicalScale * ratio });
    canvas.width = Math.floor(outputViewport.width);
    canvas.height = Math.floor(outputViewport.height);
    canvas.style.width = `${cssViewport.width}px`;
    canvas.style.height = `${cssViewport.height}px`;
    stage.style.width = `${cssViewport.width}px`;
    stage.style.height = `${cssViewport.height}px`;
    overlay.style.width = `${cssViewport.width}px`;
    overlay.style.height = `${cssViewport.height}px`;
    const context = canvas.getContext("2d", { alpha: false });
    renderTask = page.render({ canvasContext: context, viewport: outputViewport });
    await renderTask.promise;
    if (requested !== generation) return;
    status.hidden = true;
    pageInput.value = pageNumber;
    zoomText.textContent = `${Math.round(logicalScale * 100)}%`;
    shell.dispatchEvent(new CustomEvent("drawingviewer:rendered", { detail: { pageNumber, cssWidth: cssViewport.width, cssHeight: cssViewport.height } }));
  } catch (error) {
    if (error?.name !== "RenderingCancelledException" && requested === generation) showError("PDF sayfası yüklenemedi veya dosya bozuk.");
  } finally { renderTask = undefined; }
}
function setPage(value) { pageNumber = Math.min(documentHandle.numPages, Math.max(1, Number.parseInt(value, 10) || 1)); renderPage(); }
function setMode(nextMode, nextScale = scale) { mode = nextMode; scale = clampScale(nextScale); renderPage(); }
shell.addEventListener("click", (event) => {
  const action = event.target.closest("[data-action]")?.dataset.action;
  if (!action || !documentHandle) return;
  if (action === "previous") setPage(pageNumber - 1);
  if (action === "next") setPage(pageNumber + 1);
  if (action === "zoom-in") setMode("custom", scale * 1.25);
  if (action === "zoom-out") setMode("custom", scale / 1.25);
  if (action === "actual") setMode("actual", 1);
  if (action === "fit-page") setMode("fit-page");
  if (action === "fit-width") setMode("fit-width");
});
pageInput.addEventListener("change", () => setPage(pageInput.value));
new ResizeObserver(() => { if (mode.startsWith("fit")) renderPage(); }).observe(viewport);
try {
  documentHandle = await window.pdfjsLib.getDocument({ url: shell.dataset.contentUrl, withCredentials: true }).promise;
  pageCount.textContent = documentHandle.numPages;
  await renderPage();
} catch (_) { showError("PDF yüklenemedi. Yetkinizi ve dosyanın geçerli olduğunu kontrol edin."); }
