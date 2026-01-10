(function () {
    console.log('🗺️ [results-map.js] OpenStreetMap initialization script loaded');
    let gmap = null;
    const planMarkers = [];
    const dayLayers = {};
    let layerManager = null;
    let selectedDay = null;
    let __initStarted = false;
    let __initDone = false;

    function parsePlan() {
        const el = document.getElementById('travel-plan-data');
        if (!el) return null;
        try { return JSON.parse(el.textContent); } catch { return null; }
    }

    async function init() {
        if (__initStarted || __initDone) return;
        __initStarted = true;

        const plan = parsePlan();
        if (!plan) { __initStarted = false; return; }
        const mapEl = document.getElementById('plan-map');
        if (!mapEl) { __initStarted = false; return; }

        // Check if tab is hidden
        const mapPanel = mapEl.closest('[data-tab-panel]');
        if (mapPanel && mapPanel.hasAttribute('hidden')) {
            console.warn('⚠️ Map panel is hidden, waiting for visibility...');
            __initStarted = false;
            
            const observer = new MutationObserver(() => {
                if (!mapPanel.hasAttribute('hidden')) {
                    console.log('✅ Map panel now visible, initializing...');
                    observer.disconnect();
                    __initStarted = false;
                    init();
                }
            });
            observer.observe(mapPanel, { attributes: true, attributeFilter: ['hidden'] });
            return;
        }

        // Ensure minimum height
        if (mapEl.clientHeight === 0) {
            mapEl.style.minHeight = '350px';
            mapEl.style.height = '350px';
        }

        try {
            console.log('🗺️ Waiting for Leaflet API...');
            await LeafletMap.ready(20000);
            console.log('✅ Leaflet API is ready');
        } catch (e) {
            console.error('❌ Leaflet not ready', e);
            const errMsg = document.createElement('div');
            errMsg.className = 'alert alert-warning mt-2';
            errMsg.textContent = 'Map failed to load. Check console for details.';
            mapEl.parentElement && mapEl.parentElement.appendChild(errMsg);
            return;
        }

        // Create map
        gmap = LeafletMap.createMap('plan-map', { center: { lat: 52.0, lng: 19.0 }, zoom: 5 });
        window.__travelPlannerMap = gmap;
        mapEl.__leafletMapInstance = gmap;

        // Parse and render plan data
        const parsed = plan.parsedDays || [];
        if (!parsed || parsed.length === 0) {
            console.warn('No parsed days in plan');
            __initDone = true;
            return;
        }

        console.log(`📍 Rendering ${parsed.length} days on map`);
        layerManager = LeafletMapExt.createLayerManager(gmap);

        const colors = ['#2b7cff', '#34c759', '#ff9500', '#ff3b30', '#af52de', '#ffcc00'];

        // Process each day
        parsed.forEach((day, dayIndex) => {
            const dayNum = dayIndex + 1;
            const dayName = `Day ${dayNum}`;
            const dayColor = colors[dayIndex % colors.length];

            dayLayers[dayName] = {
                items: [],
                markers: [],
                path: [],
                polyline: null,
                color: dayColor,
                dayNumber: dayNum
            };

            layerManager.addLayer(dayName);

            if (!day.places || day.places.length === 0) return;

            // Add markers for each place
            day.places.forEach((place, idx) => {
                if (!place.latitude || !place.longitude) return;

                const itemIndex = idx + 1;
                const lat = place.latitude;
                const lng = place.longitude;
                const name = place.name || `Place ${itemIndex}`;

                const infoHtml = `
                    <div style='min-width: 200px; max-width: 300px;'>
                        <div style='display: flex; align-items: center; margin-bottom: 8px;'>
                            <span style='background: ${dayColor}; color: white; padding: 4px 8px; border-radius: 4px; margin-right: 8px;'>${itemIndex}</span>
                            <strong>${name}</strong>
                        </div>
                        <div style='font-size: 12px; color: #666;'>Day ${dayNum} - Stop #${itemIndex}</div>
                    </div>
                `;

                const marker = LeafletMapExt.addInteractiveMarker(gmap, {
                    lat: lat,
                    lng: lng,
                    title: name,
                    label: {
                        text: String(itemIndex),
                        color: '#ffffff',
                        fontSize: '12px',
                        fontWeight: 'bold'
                    },
                    icon: {
                        path: 'CIRCLE',
                        scale: 12,
                        fillColor: dayColor,
                        fillOpacity: 1,
                        strokeColor: '#ffffff',
                        strokeWeight: 2
                    },
                    infoHtml: infoHtml
                }, {});

                dayLayers[dayName].items.push({
                    text: name,
                    marker: marker,
                    lat: lat,
                    lng: lng,
                    index: itemIndex
                });

                dayLayers[dayName].path.push({ lat: lat, lng: lng });
                layerManager.addMarkerTo(dayName, marker);
                planMarkers.push(marker);
            });

            // Draw polyline if we have at least 2 points
            if (dayLayers[dayName].path.length > 1) {
                const poly = LeafletMapExt.drawPolyline(gmap, dayLayers[dayName].path, {
                    color: dayColor,
                    weight: 3,
                    opacity: 0.9
                });
                dayLayers[dayName].polyline = poly;
                layerManager.addPolylineTo(dayName, poly);
            }
        });

        // Fit map to show all markers
        if (planMarkers.length) {
            LeafletMap.fitToMarkers(gmap, planMarkers);
        }

        // Setup day filter controls
        setupDayFilters();

        __initDone = true;
        console.log('✅ Map fully initialized with', planMarkers.length, 'markers');
    }

    function setupDayFilters() {
        const container = document.getElementById('map-controls');
        if (!container) return;

        let html = '<div class="btn-group" role="group">';
        html += '<button class="btn btn-sm btn-primary" data-day="all">All Days</button>';
        
        Object.keys(dayLayers).forEach(name => {
            const match = name.match(/Day\s*(\d+)/i);
            if (match) {
                const dayNum = match[1];
                html += `<button class="btn btn-sm btn-outline-primary" data-day="${dayNum}">Day ${dayNum}</button>`;
            }
        });
        
        html += '</div>';
        container.innerHTML = html;

        // Attach event listeners
        container.querySelectorAll('button').forEach(btn => {
            btn.addEventListener('click', () => {
                const day = btn.getAttribute('data-day');
                if (day === 'all') {
                    setDayFilter(null);
                } else {
                    setDayFilter(parseInt(day));
                }
            });
        });
    }

    function setDayFilter(dayNum) {
        selectedDay = dayNum;

        // Update button states
        const container = document.getElementById('map-controls');
        if (container) {
            container.querySelectorAll('button').forEach(btn => {
                const btnDay = btn.getAttribute('data-day');
                if (dayNum === null && btnDay === 'all') {
                    btn.classList.remove('btn-outline-primary');
                    btn.classList.add('btn-primary');
                } else if (btnDay === String(dayNum)) {
                    btn.classList.remove('btn-outline-primary');
                    btn.classList.add('btn-primary');
                } else {
                    btn.classList.remove('btn-primary');
                    btn.classList.add('btn-outline-primary');
                }
            });
        }

        // Show/hide layers
        Object.keys(dayLayers).forEach(name => {
            const layer = dayLayers[name];
            const match = name.match(/Day\s*(\d+)/i);
            const layerDay = match ? parseInt(match[1]) : null;

            if (dayNum === null || layerDay === dayNum) {
                layerManager.setVisible(name, true);
            } else {
                layerManager.setVisible(name, false);
            }
        });

        console.log('✅ Day filter set to:', dayNum === null ? 'All Days' : `Day ${dayNum}`);
    }

    function setupInit() {
        console.log('🗺️ setupInit called');
        
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', init);
        } else {
            setTimeout(init, 100);
        }
    }

    // Listen for tab visibility changes
    document.addEventListener('tab-visible', (e) => {
        if (e.detail && e.detail.tab === 'map') {
            console.log('🗺️ Map tab became visible');
            if (!__initDone && !__initStarted) {
                init();
            } else if (__initDone && gmap) {
                // Force map refresh
                setTimeout(() => {
                    if (gmap && gmap.invalidateSize) {
                        gmap.invalidateSize();
                    }
                }, 100);
            }
        }
    });

    setupInit();
})();
