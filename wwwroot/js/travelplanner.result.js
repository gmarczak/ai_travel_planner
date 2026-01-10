(function () {
  // Shared autocomplete helper - currently disabled for OpenStreetMap
  function ensureMapsReady() {
    return new Promise((resolve, reject) => {
      // OpenStreetMap/Leaflet doesn't have Places API
      reject(new Error('Places API not available with OpenStreetMap'));
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

  function wireAutocomplete(inputId, targetSelector, addBtnId) {
    const inputEl = document.getElementById(inputId);
    const target = document.querySelector(targetSelector);
    const addBtn = document.getElementById(addBtnId);
    if (!inputEl || !target) return;

    // Disable autocomplete - not available with OpenStreetMap
    if (inputEl) {
      inputEl.disabled = true;
      inputEl.placeholder = 'Place search coming soon with OpenStreetMap';
    }
    if (addBtn) {
      addBtn.disabled = true;
      addBtn.title = 'Place search not available yet';
    }

    console.log('ℹ️ Places autocomplete disabled (OpenStreetMap migration)');
  }

  function init() {
    // Wire Details tab inputs if present
    wireAutocomplete('accommodation-search', 'textarea[name="accommodations"]', 'add-accommodation-btn');
    wireAutocomplete('activity-search', 'textarea[name="activities"]', 'add-activity-btn');
    wireAutocomplete('transport-search', 'textarea[name="transportation"]', 'add-transport-btn');
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init); else init();
})();
