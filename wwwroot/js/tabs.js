// Minimal tabs controller without CSS frameworks.
// Usage: add buttons/links with [data-tab]="<name>" and panels with id="tab-<name>" and [data-tab-panel].
// The active panel is the one without the hidden attribute.
(function(){
  function selectTab(name){
    const panels = document.querySelectorAll('[data-tab-panel]');
    panels.forEach(p => {
      if (p.id === `tab-${name}`) { p.removeAttribute('hidden'); }
      else { p.setAttribute('hidden', ''); }
    });
    const triggers = document.querySelectorAll('[data-tab]');
    triggers.forEach(t => {
      const isActive = t.getAttribute('data-tab') === name;
      t.setAttribute('aria-selected', isActive ? 'true' : 'false');
    });

    // If switching to map, ensure Google Map resizes correctly
    if (name.toLowerCase().includes('map')) {
      setTimeout(() => {
        try {
          const el = document.getElementById('plan-map');
          if (el && el.__googleMapInstance && window.google && google.maps) {
            const map = el.__googleMapInstance;
            const center = map.getCenter();
            google.maps.event.trigger(map, 'resize');
            if (center) map.setCenter(center);
          }
        } catch {}
      }, 50);
    }
  }

  function wire(){
    document.addEventListener('click', function(ev){
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
      selectTab(visible.id.substring(4));
    }
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', wire);
  } else {
    wire();
  }
})();
