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
      if (document.querySelector('[data-caliper-mode]')?.checked) {
        const next = forms.slice(index + 1).concat(forms.slice(0, index)).find(candidate => candidate.closest('[data-measurement-row]').dataset.status === 'PENDING');
        next?.elements.measured_value.focus();
      }
    } catch (failure) { error.textContent = failure.message; }
    finally { delete form.dataset.inflight; }
  });
});
