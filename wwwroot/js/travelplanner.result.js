(function(){
  // Shared Google Places Autocomplete helper used by forms in Details tab
  function ensureMapsReady() {
    return new Promise((resolve, reject) => {
      if (window.google && google.maps && google.maps.places) return resolve();
      // Fallback to global callback fired by _Layout when Maps loads
      if (document.readyState === 'complete' || document.readyState === 'interactive') {
        const check = setInterval(() => {
          if (window.google && google.maps && google.maps.places) {
            clearInterval(check);
            resolve();
          }
        }, 200);
        // timeout after 10s
        setTimeout(() => { clearInterval(check); reject(new Error('Google Maps not ready')); }, 10000);
      } else {
        window.addEventListener('load', () => {
          if (window.google && google.maps && google.maps.places) resolve(); else reject(new Error('Google Maps not ready'));
        });
      }
    });
  }

  function lineForPlace(place) {
    try {
      const name = place.name || '';
      const addr = place.formatted_address || '';
      const rating = place.rating ? ` (★${place.rating})` : '';
      return `${name}${rating}${addr ? ' — ' + addr : ''}`.trim();
    } catch { return ''; }
  }

  function wireAutocomplete(inputId, targetSelector, addBtnId){
    const inputEl = document.getElementById(inputId);
    const target = document.querySelector(targetSelector);
    const addBtn = document.getElementById(addBtnId);
    if (!inputEl || !target) return;

    let autocomplete = null;
    let lastPlace = null;

    function applyPlace(p){
      if (!p) return;
      const line = lineForPlace(p);
      if (!line) return;
      const current = target.value || '';
      const sep = current && !current.endsWith('\n') ? '\n' : '';
      target.value = current + sep + line;
      inputEl.value = '';
      // Trigger input event so any listeners update
      target.dispatchEvent(new Event('input', { bubbles: true }));
    }

    ensureMapsReady().then(() => {
      try {
        autocomplete = new google.maps.places.Autocomplete(inputEl, { types: ['establishment', 'geocode'] });
        autocomplete.setFields(['place_id','name','geometry','photos','rating','formatted_address','types']);
        autocomplete.addListener('place_changed', () => {
          const place = autocomplete.getPlace();
          lastPlace = place || null;
        });
      } catch (e) { console.warn('Autocomplete init failed', e); }
    }).catch(err => console.warn(err));

    if (addBtn) {
      addBtn.addEventListener('click', () => {
        if (lastPlace && lastPlace.place_id) {
          // Prefer details to enrich fields
          try {
            const svc = new google.maps.places.PlacesService(document.createElement('div'));
            svc.getDetails({ placeId: lastPlace.place_id, fields: ['place_id','name','geometry','photos','rating','formatted_address','types','formatted_phone_number','opening_hours'] }, (details, status) => {
              if (status === google.maps.places.PlacesServiceStatus.OK && details) applyPlace(details);
              else applyPlace(lastPlace);
            });
          } catch { applyPlace(lastPlace); }
        } else {
          // If user typed free text, just append it
          const txt = (inputEl.value || '').trim();
          if (txt) {
            const current = target.value || '';
            const sep = current && !current.endsWith('\n') ? '\n' : '';
            target.value = current + sep + txt;
            inputEl.value = '';
            target.dispatchEvent(new Event('input', { bubbles: true }));
          }
        }
      });
    }
  }

  function init(){
    // Wire Details tab inputs if present
    wireAutocomplete('accommodation-search', 'textarea[name="accommodations"]', 'add-accommodation-btn');
    wireAutocomplete('activity-search', 'textarea[name="activities"]', 'add-activity-btn');
    wireAutocomplete('transport-search', 'textarea[name="transportation"]', 'add-transport-btn');
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init); else init();
})();
