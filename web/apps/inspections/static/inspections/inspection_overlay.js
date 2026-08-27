const shell = document.querySelector('#drawing-viewer');
const overlay = shell?.querySelector('[data-role="overlay"]');
let markers = [];
function renderMarkers(pageNumber) {
  overlay.replaceChildren();
  markers.filter(marker => marker.page_no === pageNumber).forEach(marker => {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = `inspection-marker inspection-marker--${marker.state.toLowerCase()}${marker.is_critical ? ' inspection-marker--critical' : ''}`;
    button.dataset.requirementId = marker.requirement_id;
    button.dataset.state = marker.state;
    button.style.left = `${marker.x_ratio * 100}%`;
    button.style.top = `${marker.y_ratio * 100}%`;
    button.textContent = `${marker.measure_code} ${marker.state}`;
    button.title = `${marker.measure_code} — ${marker.measure_name} (${marker.state})`;
    button.addEventListener('click', () => parent.postMessage({type: 'inspection:focus-row', requirementId: marker.requirement_id}, location.origin));
    overlay.append(button);
  });
}
shell.addEventListener('drawingviewer:rendered', event => renderMarkers(event.detail.pageNumber));
window.addEventListener('message', event => {
  if (event.origin !== location.origin) return;
  if (event.data?.type === 'inspection:marker-state') {
    const marker = markers.find(item => item.requirement_id === event.data.requirementId);
    if (marker) { marker.state = event.data.state; renderMarkers(Number(shell.querySelector('[data-role="page"]').value)); }
  }
  if (event.data?.type === 'inspection:highlight-marker') overlay.querySelector(`[data-requirement-id="${CSS.escape(event.data.requirementId)}"]`)?.focus();
});
try {
  const response = await fetch(shell.dataset.inspectionOverlayUrl, {headers: {'Accept': 'application/json'}});
  if (!response.ok) throw new Error();
  markers = (await response.json()).markers;
  renderMarkers(Number(shell.querySelector('[data-role="page"]').value));
} catch (_) { shell.querySelector('[data-role="status"]').textContent = 'Kontrol işaretleri yüklenemedi.'; }
