// Minimal tabs controller without CSS frameworks.
// Usage: add buttons/links with [data-tab]="<name>" and panels with id="tab-<name>" and [data-tab-panel].
// The active panel is the one without the hidden attribute.
(function () {
  function selectTab(name) {
    console.log(`📑 [tabs.js] Selecting tab: ${name}`);
    const panels = document.querySelectorAll('[data-tab-panel]');
    panels.forEach(p => {
      if (p.id === `tab-${name}`) { 
        p.removeAttribute('hidden'); 
        console.log(`  ✅ Showing panel: ${p.id}`);
      }
      else { 
        p.setAttribute('hidden', ''); 
      }
    });
    const triggers = document.querySelectorAll('[data-tab]');
    triggers.forEach(t => {
      const isActive = t.getAttribute('data-tab') === name;
      t.setAttribute('aria-selected', isActive ? 'true' : 'false');
    });

    // Scroll to top on mobile when switching tabs
    if (window.innerWidth < 768) {
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }

    // Emit a "tab-selected" event so other scripts can react
    try {
      document.dispatchEvent(new CustomEvent('tab-selected', { detail: { name } }));
    } catch {}

    // Also emit a "tab-visible" event (back-compat with older listeners)
    try {
      document.dispatchEvent(new CustomEvent('tab-visible', { detail: { tab: name } }));
    } catch {}

    // If switching to map, trigger a resize for Leaflet (and legacy Google Maps path if present)
    if (name.toLowerCase().includes('map')) {
      console.log('🗺️ [tabs.js] Map tab visible');
      setTimeout(() => {
        try {
          if (window.__travelPlannerMap && window.LeafletMap && typeof window.LeafletMap.invalidateSize === 'function') {
            window.LeafletMap.invalidateSize(window.__travelPlannerMap);
            console.log('✅ [tabs.js] Leaflet resize triggered');
          } else if (window.__travelPlannerMap && window.google && google.maps) {
            const center = window.__travelPlannerMap.getCenter();
            google.maps.event.trigger(window.__travelPlannerMap, 'resize');
            if (center) window.__travelPlannerMap.setCenter(center);
            console.log('✅ [tabs.js] Google map resize triggered');
          }
        } catch (e) {
          console.error('❌ [tabs.js] Error resizing map:', e);
        }
      }, 50);
    }
  }

  function wire() {
    document.addEventListener('click', function (ev) {
      const t = ev.target.closest('[data-tab]');
      if (!t) return;
      const name = t.getAttribute('data-tab');
      if (!name) return;
      ev.preventDefault();
      selectTab(name);
    });

    // Initialize default tab if any panel is not hidden
    const visible = Array.from(document.querySelectorAll('[data-tab-panel]')).find(p => !p.hasAttribute('hidden'));
    if (visible && visible.id.startsWith('tab-')) {
      console.log(`📑 [tabs.js] Initializing default tab from visible panel: ${visible.id}`);
      selectTab(visible.id.substring(4));
    } else {
      console.log('📑 [tabs.js] No visible tab panel found at init');
    }
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', wire);
  } else {
    wire();
  }
})();
