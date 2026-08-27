const csrf = form => form.querySelector('[name=csrfmiddlewaretoken]').value;
document.querySelectorAll('[data-confirm]').forEach(form => form.addEventListener('submit', event => { if (!confirm(form.dataset.confirm)) event.preventDefault(); }));
const forms = [...document.querySelectorAll('[data-measurement-form]')];
forms.forEach((form, index) => {
  const input = form.elements.measured_value;
  input.addEventListener('keydown', async event => {
    if (event.key !== 'Enter') return;
    event.preventDefault();
    if (form.dataset.inflight === 'true') return;
    form.dataset.inflight = 'true';
    const error = form.querySelector('[data-error]'); error.textContent = '';
    try {
      const response = await fetch(form.action, {method:'POST', headers:{'X-CSRFToken':csrf(form)}, body:new FormData(form)});
      const data = await response.json();
      if (!response.ok) throw new Error(data.error || 'Ölçüm kaydedilemedi.');
      const row = form.closest('[data-measurement-row]');
      row.dataset.status = data.result; row.querySelector('[data-result]').textContent = data.result;
      tellViewer({type: 'inspection:marker-state', requirementId: row.dataset.requirementId, state: data.result});
      if (document.querySelector('[data-caliper-mode]')?.checked) {
        const next = forms.slice(index + 1).concat(forms.slice(0, index)).find(candidate => candidate.closest('[data-measurement-row]').dataset.status === 'PENDING');
        next?.elements.measured_value.focus();
      }
    } catch (failure) { error.textContent = failure.message; }
    finally { delete form.dataset.inflight; }
  });
});
const viewer = document.querySelector('[data-inspection-viewer]');
function tellViewer(message) { viewer?.contentWindow?.postMessage(message, location.origin); }
window.addEventListener('message', event => {
  if (event.origin !== location.origin || event.data?.type !== 'inspection:focus-row') return;
  document.querySelector(`[data-measurement-row][data-requirement-id="${CSS.escape(event.data.requirementId)}"] input[name="measured_value"]`)?.focus();
});
document.querySelectorAll('[data-measurement-row]').forEach(row => row.addEventListener('click', () => tellViewer({type: 'inspection:highlight-marker', requirementId: row.dataset.requirementId})));
document.querySelector('[data-group-filter]')?.addEventListener('change', event => {
  document.querySelectorAll('[data-measurement-row]').forEach(row => { row.hidden = Boolean(event.target.value) && row.dataset.group !== event.target.value; });
});
