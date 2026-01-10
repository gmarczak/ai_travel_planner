(function () {
    console.log('🗺️ [results-map.js] Map initialization script loaded');
    let gmap = null;
    let gcenter = null;
    let geocoder = null;
    const planMarkers = [];
    const dayLayers = {};
    let layerManager = null;
    let markerClusterer = null;
    const polylines = [];
    let selectedDay = null;
    let __directionsService = null;
    let __travelMode = 'DRIVING'; // DRIVING, WALKING, TRANSIT
    let __avoidHighways = false;
    let __initStarted = false;
    let __initDone = false;

    function showLoadingSpinner(show) {
        const mapEl = document.getElementById('plan-map');
        if (!mapEl) return;

        let spinner = document.getElementById('map-loading-spinner');
        if (show) {
            if (!spinner) {
                spinner = document.createElement('div');
                spinner.id = 'map-loading-spinner';
                spinner.className = 'd-flex justify-content-center align-items-center';
                spinner.style.cssText = 'position: absolute; top: 0; left: 0; right: 0; bottom: 0; background: rgba(255,255,255,0.9); z-index: 1000;';
                spinner.innerHTML = `
                    <div class="text-center">
                        <div class="spinner-border text-primary mb-2" role="status" style="width: 3rem; height: 3rem;">
                            <span class="visually-hidden">Loading...</span>
                        </div>
                        <div class="text-muted">Loading map markers...</div>
                    </div>
                `;
                mapEl.parentElement.style.position = 'relative';
                mapEl.parentElement.appendChild(spinner);
            }
        } else {
            if (spinner && spinner.parentElement) {
                spinner.parentElement.removeChild(spinner);
            }
        }
    }

    function parsePlan() {
        const el = document.getElementById('travel-plan-data');
        if (!el) return null;
        try { return JSON.parse(el.textContent); } catch { return null; }
    }

    function geocode(geocoder, address) {
        return new Promise(resolve => {
            geocoder.geocode({ address }, (results, status) => {
                resolve(status === 'OK' && results && results[0] ? results[0] : null);
            });
        });
    }

    async function init() {
        if (__initStarted || __initDone) return;
        __initStarted = true;

        const plan = parsePlan();
        if (!plan) { __initStarted = false; return; }
        const mapEl = document.getElementById('plan-map');
        if (!mapEl) { __initStarted = false; return; }

        // CRITICAL: Check if tab is still hidden with display:none = 0x0 size
        // If hidden, WAIT for it to become visible before initializing
        const mapPanel = mapEl.closest('[data-tab-panel]');
        if (mapPanel && mapPanel.hasAttribute('hidden')) {
            console.warn('⚠️ [results-map.js] Map panel is hidden, waiting for visibility...');
            __initStarted = false; // Reset so we can retry
            
            // Use MutationObserver to wait for hidden attribute removal
            const observer = new MutationObserver(() => {
                if (!mapPanel.hasAttribute('hidden')) {
                    console.log('✅ [results-map.js] Map panel now visible, initializing...');
                    observer.disconnect();
                    // Reset and retry init
                    __initStarted = false;
                    init();
                }
            });
            observer.observe(mapPanel, { attributes: true, attributeFilter: ['hidden'] });
            return;
        }

        // If hidden at load, ensure a reasonable height before init
        if (mapEl.clientHeight === 0) {
            mapEl.style.minHeight = mapEl.style.minHeight || '350px';
            mapEl.style.height = mapEl.style.height || '350px';
        }

        try {
            console.log('🗺️ [results-map.js] Waiting for Leaflet API...');
            await LeafletMap.ready(20000);
            console.log('✅ [results-map.js] Leaflet API is ready');
        } catch (e) {
            console.error('❌ [results-map.js] Leaflet not ready', e);
            // Hide loading indicator on error
            const loadingEl = document.getElementById('map-loading');
            if (loadingEl) loadingEl.style.display = 'none';
            // Show a lightweight inline message for troubleshooting
            const errMsg = document.createElement('div');
            errMsg.className = 'alert alert-warning mt-2';
            errMsg.textContent = 'Map failed to load. Check console for details.';
            mapEl.parentElement && mapEl.parentElement.appendChild(errMsg);
            return;
        }

        // Leaflet-based map creation
        gmap = LeafletMap.createMap('plan-map', { center: { lat: 52.0, lng: 19.0 }, zoom: 5 });
        
        // Make map globally available for tabs.js to find
        window.__travelPlannerMap = gmap;
        mapEl.__googleMapInstance = gmap;
        
        try {
            const sz = { w: mapEl.clientWidth, h: mapEl.clientHeight };
            console.log('📐 [results-map.js] Map element size after creation:', sz);
        } catch {}
        layerManager = LeafletMapExt.createLayerManager(gmap);

        // Hide initial loading indicator now that map is created
        const loadingEl = document.getElementById('map-loading');
        if (loadingEl) loadingEl.style.display = 'none';

        // Center on destination
        const destRes = await geocode(geocoder, plan.destination);
        console.log('🌍 [results-map.js] Geocoding destination:', plan.destination);
        if (destRes && destRes.geometry && destRes.geometry.location) {
            gcenter = destRes.geometry.location;
            const lat = gcenter.lat();
            const lng = gcenter.lng();
            console.log(`📍 [results-map.js] Setting center to: lat=${lat}, lng=${lng}`);
            console.log(`🗺️ [results-map.js] Current mapTypeId: ${gmap.getMapTypeId()}`);
            
            gmap.setView([gcenter.lat(), gcenter.lng()], 12);
            console.log('✅ [results-map.js] Map centered. Zoom level: 12');
        } else {
            console.warn('⚠️ [results-map.js] Geocoding failed for destination');
        }

        // Build per-day markers from parsedDays if available, else fallback to simple list
        const parsed = plan.parsedDays || [];

        // Colors for days
        const colors = ['#2b7cff', '#34c759', '#ff9500', '#ff3b30', '#af52de', '#ffcc00'];

        // Create UI controls for layer toggles and day filters
        createLayerControls(parsed.length);

        for (let i = 0; i < parsed.length; i++) {
            const day = parsed[i];
            const dayName = `Day ${day.day}`;
            const dayColor = colors[i % colors.length];
            dayLayers[dayName] = { markers: [], path: [], items: [], polyline: null, color: dayColor, dayNumber: day.day };
            layerManager.addLayer(dayName);

            // Try geocoding each line (limited)
            let itemIndex = 0;
            for (let lineIdx = 0; lineIdx < day.lines.length && lineIdx < 25; lineIdx++) {
                const line = day.lines[lineIdx];
                const text = line.trim();
                if (!text) continue;

                // Only geocode lines that start with 📍 (actual place names)
                if (!text.startsWith('📍')) continue;

                // Remove the 📍 emoji for cleaner display
                const cleanText = text.replace(/^📍\s*/, '').trim();

                // Collect additional info from next lines (location, details, tips)
                let locationInfo = '';
                let details = [];
                let timeOfDay = '';

                // Look back for time of day marker
                for (let i = lineIdx - 1; i >= Math.max(0, lineIdx - 3); i--) {
                    const prevLine = day.lines[i].trim();
                    if (prevLine.match(/^(🌅|☀️|🌆)/)) {
                        timeOfDay = prevLine.replace(':', '').trim();
                        break;
                    }
                }

                // Look ahead for location and details
                for (let i = lineIdx + 1; i < Math.min(day.lines.length, lineIdx + 5); i++) {
                    const nextLine = day.lines[i].trim();
                    if (!nextLine) break;

                    if (nextLine.startsWith('📌')) {
                        locationInfo = nextLine.replace(/^📌\s*Location:\s*/i, '').trim();
                    } else if (nextLine.startsWith('ℹ️')) {
                        details.push(nextLine.replace(/^ℹ️\s*/, '').trim());
                    } else if (nextLine.startsWith('📍')) {
                        // Next place, stop looking
                        break;
                    }
                }

                const q = plan.destination ? `${cleanText}, ${plan.destination}` : cleanText;
                const r = await geocode(geocoder, q);
                if (r && r.geometry && r.geometry.location) {
                    const loc = r.geometry.location;
                    itemIndex++;

                    // Build detailed info HTML
                    let detailsHtml = '';
                    if (details.length > 0) {
                        detailsHtml = '<div class="mt-2 pt-2 border-top">';
                        details.forEach(detail => {
                            detailsHtml += `<div class="small mb-1" style="line-height: 1.4;"><span style="color: #666;">ℹ️</span> ${detail}</div>`;
                        });
                        detailsHtml += '</div>';
                    }

                    // Create marker with number label and day color
                    const marker = LeafletMapExt.addInteractiveMarker(gmap, {
                        lat: loc.lat(),
                        lng: loc.lng(),
                        title: cleanText,
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
                        infoHtml: `
                            <div class='p-3' style='min-width: 250px; max-width: 350px;'>
                                <div class='d-flex align-items-start mb-2'>
                                    <span class='badge' style='background-color: ${dayColor}; color: white; font-size: 13px; padding: 8px 12px; margin-right: 12px; border-radius: 20px;'>${itemIndex}</span>
                                    <div style='flex: 1;'>
                                        <strong style='font-size: 16px; color: #1a1a1a; display: block; margin-bottom: 4px;'>${cleanText}</strong>
                                        ${timeOfDay ? `<div class='small' style='color: #666; margin-bottom: 4px;'>${timeOfDay}</div>` : ''}
                                    </div>
                                </div>
                                
                                ${locationInfo ? `
                                <div class='small mb-2 p-2' style='background-color: #f8f9fa; border-radius: 6px; border-left: 3px solid ${dayColor};'>
                                    <svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" fill="currentColor" class="bi bi-geo-alt-fill" viewBox="0 0 16 16" style="margin-right: 5px; color: ${dayColor};">
                                        <path d="M8 16s6-5.686 6-10A6 6 0 0 0 2 6c0 4.314 6 10 6 10zm0-7a3 3 0 1 1 0-6 3 3 0 0 1 0 6z"/>
                                    </svg>
                                    <span style='color: #495057;'>${locationInfo}</span>
                                </div>
                                ` : ''}
                                
                                <div class='d-flex gap-3 small text-muted mb-2'>
                                    <div>
                                        <svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" fill="currentColor" class="bi bi-calendar-check" viewBox="0 0 16 16" style="margin-right: 4px;">
                                            <path d="M10.854 7.146a.5.5 0 0 1 0 .708l-3 3a.5.5 0 0 1-.708 0l-1.5-1.5a.5.5 0 1 1 .708-.708L7.5 9.793l2.646-2.647a.5.5 0 0 1 .708 0z"/>
                                            <path d="M3.5 0a.5.5 0 0 1 .5.5V1h8V.5a.5.5 0 0 1 1 0V1h1a2 2 0 0 1 2 2v11a2 2 0 0 1-2 2H2a2 2 0 0 1-2-2V3a2 2 0 0 1 2-2h1V.5a.5.5 0 0 1 .5-.5zM1 4v10a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1V4H1z"/>
                                        </svg>
                                        Day ${day.day}${day.date ? ': ' + day.date : ''}
                                    </div>
                                </div>
                                
                                ${detailsHtml}
                                
                                <div class='mt-2 pt-2 border-top d-flex justify-content-between align-items-center'>
                                    <a href='https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(cleanText + ', ' + plan.destination)}' target='_blank' class='btn btn-sm btn-outline-primary' style='font-size: 12px; padding: 4px 10px;'>
                                        <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" fill="currentColor" class="bi bi-map" viewBox="0 0 16 16" style="margin-right: 4px;">
                                            <path fill-rule="evenodd" d="M15.817.113A.5.5 0 0 1 16 .5v14a.5.5 0 0 1-.402.49l-5 1a.502.502 0 0 1-.196 0L5.5 15.01l-4.902.98A.5.5 0 0 1 0 15.5v-14a.5.5 0 0 1 .402-.49l5-1a.5.5 0 0 1 .196 0L10.5.99l4.902-.98a.5.5 0 0 1 .415.103zM10 1.91l-4-.8v12.98l4 .8V1.91zm1 12.98 4-.8V1.11l-4 .8v12.98zm-6-.8V1.11l-4 .8v12.98l4-.8z"/>
                                        </svg>
                                        Directions
                                    </a>
                                    <span class='badge bg-secondary' style='font-size: 11px;'>Stop #${itemIndex}</span>
                                </div>
                            </div>
                        `
                    }, {
                        'Add to Plan': (m) => addMarkerToPlan(day.day, m),
                        'Move to Day': (m) => promptMoveToDay(m, day.day)
                    });
                    // register marker with layer manager so visibility toggles work
                    try { layerManager.addMarkerTo(dayName, marker); } catch (e) { /* ignore */ }
                    // store item with marker and coordinates for reorder
                    dayLayers[dayName].items.push({ text: cleanText, marker, lat: loc.lat(), lng: loc.lng(), index: itemIndex });
                    dayLayers[dayName].markers.push(marker);
                    dayLayers[dayName].path.push({ lat: loc.lat(), lng: loc.lng() });
                    planMarkers.push(marker);
                }
            }

            // draw polyline for the day using Google Directions encoded polylines if available
            if (dayLayers[dayName].path.length > 1) {
                let poly = null;

                // Debug: Log route polylines data
                console.log(`Day ${day.day} route data:`, {
                    hasRoutePolylines: !!(day.routePolylines),
                    polylineCount: day.routePolylines?.length || 0,
                    pathPoints: dayLayers[dayName].path.length
                });

                // Check if we have route polylines from backend (Google Directions API)
                if (day.routePolylines && day.routePolylines.length > 0) {
                    console.log(`Decoding ${day.routePolylines.length} polylines for Day ${day.day}`);
                    // Simple polyline drawing - Leaflet doesn't decode Google polylines
                    // Skip encoded polylines for now
                    console.log('  Skipping encoded polylines (not supported in Leaflet)');
                    const allPaths = [];

                    if (allPaths.length > 0) {
                        poly = LeafletMapExt.drawPolyline(gmap, allPaths, {
                            color: dayColor,
                            weight: 3,
                            opacity: 0.8
                        });
                    }
                }

                // If no backend polyline, draw straight line first, then try client-side DirectionsService
                if (!poly) {
                    // temporary straight polyline for instant feedback
                    const temp = LeafletMapExt.drawPolyline(gmap, dayLayers[dayName].path, { strokeColor: dayColor });
                    dayLayers[dayName].polyline = temp;

                    try {
                        const routed = await buildClientDirectionsPolyline(dayName, dayColor);
                        if (routed) {
                            try { if (temp) LeafletMapExt.clearPolyline(temp); } catch { }
                            // clear any existing polylines registered in layer manager for this day
                            try {
                                const lyr = layerManager.getLayer ? layerManager.getLayer(dayName) : null;
                                if (lyr && Array.isArray(lyr.polylines)) {
                                    lyr.polylines.forEach(p => { try { LeafletMapExt.clearPolyline(p); } catch { } });
                                    lyr.polylines = [];
                                }
                            } catch { }
                            dayLayers[dayName].polyline = routed;
                            layerManager.addPolylineTo(dayName, routed);
                            polylines.push(routed);
                        } else {
                            console.warn(`Client DirectionsService returned no route for ${dayName}`);
                        }
                    } catch (e) {
                        console.warn('Client DirectionsService failed', e);
                    }
                } else {
                    // clear any existing polylines registered in layer manager for this day
                    try {
                        const lyr = layerManager.getLayer ? layerManager.getLayer(dayName) : null;
                        if (lyr && Array.isArray(lyr.polylines)) {
                            lyr.polylines.forEach(p => { try { LeafletMapExt.clearPolyline(p); } catch { } });
                            lyr.polylines = [];
                        }
                    } catch { }
                    dayLayers[dayName].polyline = poly;
                    layerManager.addPolylineTo(dayName, poly);
                    polylines.push(poly);
                }
            }
        }

        // Hide spinner and show map after all markers are loaded
        showLoadingSpinner(false);
        if (mapEl) mapEl.style.visibility = 'visible';

        // Optionally cluster markers if MarkerClusterer is available

        // initialize/update clusterer (supports different global names)
        updateCluster();

        if (planMarkers.length) LeafletMap.fitToMarkers(gmap, planMarkers);

        // Set default view to "All Days" instead of Day 1
        if (parsed.length) {
            setDayFilter(null); // null = show all days
        }

        if (planMarkers.length) LeafletMap.fitToMarkers(gmap, planMarkers);
        __initDone = true;
        console.log('✅ [results-map.js] Map fully initialized and ready!');
    }

    function setupInit() {
        console.log(`🗺️ [results-map.js] setupInit called: initDone=${__initDone}, initStarted=${__initStarted}`);
        
        const mapPanel = document.getElementById('tab-map');
        const isVisible = mapPanel && !mapPanel.hasAttribute('hidden');
        
        // If map tab is already visible, initialize immediately
        if (isVisible && !__initStarted && !__initDone) {
            console.log('🗺️ [results-map.js] Map tab is visible, initializing now...');
            init();
        } 
        // If hidden, wait for it to become visible
        else if (!isVisible && !__initStarted && !__initDone) {
            console.log('🗺️ [results-map.js] Map tab is hidden, waiting for visibility...');
            const onTabSelected = (ev) => {
                try {
                    const name = (ev && ev.detail && ev.detail.name) ? String(ev.detail.name) : '';
                    console.log(`🗺️ [results-map.js] Tab selected: '${name}', initStarted=${__initStarted}`);
                    if (name.toLowerCase().includes('map') && !__initStarted && !__initDone) {
                        console.log('🗺️ [results-map.js] Map tab now visible, initializing...');
                        document.removeEventListener('tab-selected', onTabSelected);
                        init();
                    }
                } catch (e) { 
                    console.error('❌ [results-map.js] Error in tab-selected handler:', e);
                }
            };
            document.addEventListener('tab-selected', onTabSelected);
        }
        else if (__initStarted && !__initDone) {
            console.log('🗺️ [results-map.js] Map init already in progress...');
        }
        else {
            console.log('🗺️ [results-map.js] Map already initialized');
        }
    }
    if (document.readyState === 'loading') {
        console.log('🗺️ [results-map.js] DOM still loading, waiting for DOMContentLoaded...');
        document.addEventListener('DOMContentLoaded', setupInit);
    } else {
        console.log('🗺️ [results-map.js] DOM already loaded, running setupInit immediately...');
        setupInit();
    }

    // UI helpers
    function createLayerControls(dayCount) {
        const container = document.getElementById('map-controls');
        if (!container) return;
        // Clear
        container.innerHTML = '';

        // Travel mode selector
        const modeWrapper = document.createElement('div');
        modeWrapper.className = 'travel-mode-selector mb-2';
        modeWrapper.innerHTML = `
            <label class="small text-muted me-2">Travel mode:</label>
            <select id="travel-mode-select" class="form-select form-select-sm d-inline-block" style="width: auto;">
                <option value="DRIVING">Driving</option>
                <option value="WALKING">Walking</option>
                <option value="TRANSIT">Transit</option>
            </select>
            <div class="form-check form-check-inline ms-3">
                <input class="form-check-input" type="checkbox" id="avoid-highways-check">
                <label class="form-check-label small" for="avoid-highways-check">Scenic route</label>
            </div>
        `;
        container.appendChild(modeWrapper);

        // Attach event listeners
        const modeSelect = document.getElementById('travel-mode-select');
        const avoidCheck = document.getElementById('avoid-highways-check');
        if (modeSelect) {
            modeSelect.value = __travelMode;
            modeSelect.addEventListener('change', (e) => {
                __travelMode = e.target.value;
                if (selectedDay) refreshDayRoute(selectedDay);
            });
        }
        if (avoidCheck) {
            avoidCheck.checked = __avoidHighways;
            avoidCheck.addEventListener('change', (e) => {
                __avoidHighways = e.target.checked;
                if (selectedDay) refreshDayRoute(selectedDay);
            });
        }

        // ALL DAYS button
        const allBtn = document.createElement('button');
        allBtn.textContent = 'All Days';
        allBtn.setAttribute('data-day', 'all');
        allBtn.addEventListener('click', () => setDayFilter(null));
        container.appendChild(allBtn);

        // Individual day buttons
        for (let i = 1; i <= dayCount; i++) {
            const b = document.createElement('button');
            b.textContent = `Day ${i}`;
            b.setAttribute('data-day', i);
            b.addEventListener('click', () => setDayFilter(i));
            container.appendChild(b);
        }

        // Initialize Places Autocomplete for the sidebar search input
        // Note: Leaflet doesn't have built-in Places API - autocomplete disabled for now
        // Can integrate Nominatim or other geocoding services in the future
        let autocomplete = null;
        function initAutocomplete() {
            console.log('ℹ️ Places autocomplete not available with OpenStreetMap (coming soon)');
        }

        // Wire up Add button - disabled for now
        const addBtn = document.getElementById('add-location-btn');
        if (addBtn) {
            addBtn.disabled = true;
            addBtn.title = 'Place search coming soon with OpenStreetMap';
        }

        // Expose to global callback
            // In "All Days" view: hide polylines to avoid spaghetti
            if (!dayNum) {
                try { layerManager.setPolylinesVisible(name, false); } catch { }
            } else {
                // In single day view: ensure only the selected day's polylines are visible
                const isSelected = name.toLowerCase().includes(String(dayNum));
                try { layerManager.setPolylinesVisible(name, isSelected); } catch { }
            }

            // Update marker appearance based on filter
            if (layer.items) {
                layer.items.forEach((it, idx) => {
                    if (!it.marker) return;

                    if (dayNum) {
                        // Individual day view: show numbers in sequence
                        it.marker.setLabel({
                            text: String(idx + 1),
                            color: '#ffffff',
                            fontSize: '12px',
                            fontWeight: 'bold'
                        });
                    } else {
                        // All days view: show day color markers without numbers
                        it.marker.setLabel(null);
                        it.marker.setIcon({
                            path: google.maps.SymbolPath.CIRCLE,
                            scale: 10,
                            fillColor: layer.color,
                            fillOpacity: 1,
                            strokeColor: '#ffffff',
                            strokeWeight: 2
                        });
                    }
                });
            }
        }
        selectedDay = dayNum;

        // Update active state on day filter buttons
        const container = document.getElementById('map-controls');
        if (container) {
            const buttons = container.querySelectorAll('button');
            buttons.forEach(btn => {
                const btnDay = btn.getAttribute('data-day');
                if (btnDay === 'all' && !dayNum) {
                    btn.classList.add('active');
                } else if (btnDay && parseInt(btnDay) === dayNum) {
                    btn.classList.add('active');
                } else {
                    btn.classList.remove('active');
                }
            });
        }

        renderSidePanel(dayNum);
        // refresh clusters to reflect visibility change
        updateCluster();
    }

    function addMarkerToPlan(day, marker) {
        showNotification(`Added to plan: Day ${day}`, 'success');
    }

    function promptMoveToDay(marker, currentDay) {
        const d = prompt('Move this point to day number:', String(currentDay));
        const nd = parseInt(d);
        if (!isNaN(nd)) {
            showNotification(`Moved to Day ${nd}`, 'success');
            // Implementation: update internal plan structure or send to server
        }
    }

    // SIDE PANEL RENDERING, D&D and helpers
    function getSidePanelElements() {
        // Return both desktop and mobile side panels
        const desktop = document.getElementById('side-panel');
        const mobile = document.getElementById('side-panel-mobile');
        return [desktop, mobile].filter(el => el !== null);
    }

    function renderSidePanel(dayNum) {
        const panels = getSidePanelElements();
        if (panels.length === 0) return;

        const content = generateSidePanelContent(dayNum);
        panels.forEach(sp => sp.innerHTML = content);

        // Update bottom sheet title on mobile
        const titleEl = document.getElementById('bottom-sheet-day-title');
        if (titleEl && window.innerWidth < 992) {
            if (dayNum) {
                const name = `Day ${dayNum}`;
                const parsedDay = travelPlan?.parsedDays?.find(d => d.day === dayNum);
                const dateStr = parsedDay?.date || '';
                titleEl.textContent = dateStr ? `${name} - ${dateStr}` : name;
            } else {
                titleEl.textContent = 'All Days';
            }
        }

        // Hide placeholder when content loaded
        const placeholder = document.getElementById('bottom-sheet-placeholder');
        if (placeholder) {
            placeholder.style.display = dayNum ? 'none' : 'block';
        }

        // Attach events if showing specific day
        if (dayNum) {
            attachSidePanelEvents(dayNum);
        }
    }

    function generateSidePanelContent(dayNum) {
        if (!dayNum) {
            return `
                <div class="px-2 py-2">
                    <h6 class="text-muted mb-2">📍 All Days View</h6>
                    <p class="small text-muted">Showing all markers from all days. Each day has a unique color:</p>
                    <ul class="list-unstyled small">
                        ${Object.keys(dayLayers).map((name, idx) => {
                const layer = dayLayers[name];
                const colors = ['#2b7cff', '#34c759', '#ff9500', '#ff3b30', '#af52de', '#ffcc00'];
                const colorNames = ['Blue', 'Green', 'Orange', 'Red', 'Purple', 'Yellow'];
                return `<li class="mb-1">
                                <span style="display:inline-block;width:12px;height:12px;border-radius:50%;background:${layer.color || colors[idx % colors.length]};margin-right:6px;"></span>
                                <strong>${name}</strong> - ${layer.items ? layer.items.length : 0} stops
                            </li>`;
            }).join('')}
                    </ul>
                    <p class="small text-muted mt-2">Select a specific day to see detailed list and reorder stops.</p>
                </div>
            `;
        }
        const name = `Day ${dayNum}`;
        const layer = dayLayers[name];
        if (!layer) {
            return '<div class="px-2 py-2 text-muted">No points for this day.</div>';
        }

        // Build HTML for the list
        let html = '<ul class="list-group list-group-flush" id="day-items-list">';
        layer.items.forEach((it, idx) => {
            html += `
                <li class="list-group-item d-flex align-items-center" draggable="true" data-idx="${idx}">
                    <span class="badge bg-primary me-2">${idx + 1}</span>
                    <div class="flex-grow-1">${it.text}</div>
                    <button class="btn btn-sm btn-outline-danger ms-2 remove-item-btn" data-day="${dayNum}" data-idx="${idx}">Remove</button>
                </li>
            `;
        });
        html += '</ul>';
        return html;
    }

    function attachSidePanelEvents(dayNum) {
        // Attach drag & drop and remove events to all panels
        const panels = getSidePanelElements();
        panels.forEach(panel => {
            const list = panel.querySelector('#day-items-list');
            if (!list) return;

            // Remove button events
            list.querySelectorAll('.remove-item-btn').forEach(btn => {
                btn.addEventListener('click', (e) => {
                    const day = parseInt(btn.dataset.day);
                    const idx = parseInt(btn.dataset.idx);
                    removeItemFromDay(day, idx);
                });
            });

            // Drag & drop events
            list.querySelectorAll('li[draggable]').forEach(li => {
                li.addEventListener('dragstart', (e) => {
                    e.dataTransfer.setData('text/plain', li.dataset.idx);
                    li.classList.add('dragging');
                });
                li.addEventListener('dragover', (e) => {
                    e.preventDefault();
                    li.classList.add('dragover');
                });
                li.addEventListener('dragleave', () => {
                    li.classList.remove('dragover');
                });
                li.addEventListener('drop', (e) => {
                    e.preventDefault();
                    const from = parseInt(e.dataTransfer.getData('text/plain'));
                    const to = parseInt(li.dataset.idx);
                    li.classList.remove('dragover');
                    reorderDayItems(dayNum, from, to);
                });
                li.addEventListener('dragend', () => {
                    li.classList.remove('dragging');
                });
            });
        });
    }

    function reorderDayItems(dayNum, from, to) {
        const name = `Day ${dayNum}`; const layer = dayLayers[name]; if (!layer) return;
        const item = layer.items.splice(from, 1)[0]; layer.items.splice(to, 0, item);
        // update labels with new sequence numbers
        layer.items.forEach((it, idx) => {
            try {
                it.index = idx + 1;
                it.marker.setLabel({
                    text: String(idx + 1),
                    color: '#ffffff',
                    fontSize: '12px',
                    fontWeight: 'bold'
                });
            } catch { }
        });
        // update polyline
        const path = layer.items.map(it => ({ lat: it.lat, lng: it.lng }));
        if (layer.polyline) LeafletMapExt.clearPolyline(layer.polyline);
        // remove existing polylines from layer manager to avoid duplicates on toggles
        try {
            const lyr = layerManager.getLayer ? layerManager.getLayer(name) : null;
            if (lyr && Array.isArray(lyr.polylines)) {
                lyr.polylines.forEach(p => { try { LeafletMapExt.clearPolyline(p); } catch { } });
                lyr.polylines = [];
            }
        } catch { }
        const poly = LeafletMapExt.drawPolyline(gmap, path, { strokeColor: layer.color || '#2b7cff' });
        layer.polyline = poly; layerManager.addPolylineTo(name, poly);
        updateCluster(); renderSidePanel(dayNum);
        persistOrder();
    }

    function removeItemFromDay(dayNum, idx) {
        const name = `Day ${dayNum}`; const layer = dayLayers[name]; if (!layer) return;
        const it = layer.items.splice(idx, 1)[0]; if (it && it.marker) { it.marker.setMap(null); }
        // update markers array and labels with new sequence numbers
        layer.items.forEach((it, i) => {
            try {
                it.index = i + 1;
                it.marker.setLabel({
                    text: String(i + 1),
                    color: '#ffffff',
                    fontSize: '12px',
                    fontWeight: 'bold'
                });
            } catch { }
        });
        // update polyline
        const path = layer.items.map(it => ({ lat: it.lat, lng: it.lng }));
        if (layer.polyline) LeafletMapExt.clearPolyline(layer.polyline);
        // remove existing polylines from layer manager to avoid duplicates on toggles
        try {
            const lyr = layerManager.getLayer ? layerManager.getLayer(name) : null;
            if (lyr && Array.isArray(lyr.polylines)) {
                lyr.polylines.forEach(p => { try { LeafletMapExt.clearPolyline(p); } catch { } });
                lyr.polylines = [];
            }
        } catch { }
        if (path.length > 1) { const poly = LeafletMapExt.drawPolyline(gmap, path, { strokeColor: layer.color || '#2b7cff' }); layer.polyline = poly; layerManager.addPolylineTo(name, poly); }
        updateCluster(); renderSidePanel(dayNum);
        persistOrder();
    }

    function updateCluster() {
        // collect only markers that are currently visible via the layer manager
        const all = [];
        try {
            const names = (layerManager && layerManager.getLayerNames) ? layerManager.getLayerNames() : Object.keys(dayLayers);
            names.forEach(n => {
                try {
                    const L = layerManager.getLayer ? layerManager.getLayer(n) : dayLayers[n];
                    if (!L) return;
                    if (L.visible === false) return; // skip hidden layers
                    // layer.items may contain markers
                    if (L.markers && L.markers.length) L.markers.forEach(m => m && all.push(m));
                    // fallback to items array used elsewhere
                    if (L.items && L.items.length) L.items.forEach(it => it.marker && all.push(it.marker));
                } catch (e) { /* ignore per-layer errors */ }
            });
        } catch (e) { console.warn('collect markers failed', e); }

        try {
            const clusterOptions = {
                gridSize: 60,
                maxZoom: 15,
                // styles may be adjusted to match theme
                styles: [
                    { textColor: '#fff', url: '', height: 40, width: 40, anchorText: [0, 0], backgroundPosition: '0 0', textSize: 12, className: 'marker-cluster-small', },
                    { textColor: '#fff', url: '', height: 50, width: 50, anchorText: [0, 0], backgroundPosition: '0 0', textSize: 13, className: 'marker-cluster-medium', },
                    { textColor: '#fff', url: '', height: 60, width: 60, anchorText: [0, 0], backgroundPosition: '0 0', textSize: 14, className: 'marker-cluster-large', }
                ]
            };

            if (markerClusterer) {
                // MarkerClusterer v2 API
                if (markerClusterer.clearMarkers) markerClusterer.clearMarkers();
                if (markerClusterer.addMarkers) markerClusterer.addMarkers(all);
                else if (markerClusterer.repaint) markerClusterer.repaint();
            } else if (window.MarkerClusterer) {
                markerClusterer = new MarkerClusterer({ map: gmap, markers: all, ...clusterOptions });
            } else if (window.MarkerClustererPlus) {
                // older API
                markerClusterer = new MarkerClustererPlus(gmap, all, clusterOptions);
            }
        } catch (e) { console.warn('Cluster update failed', e); }
    }

    // Build routed polyline client-side using Google Maps DirectionsService (avoids server key)
    async function buildClientDirectionsPolyline(dayName, strokeColor) {
        try {
            const layer = dayLayers[dayName];
            if (!layer || !layer.path || layer.path.length < 2) return null;
            if (!__directionsService) __directionsService = new google.maps.DirectionsService();

            // Use single request with waypoints for better routing through all points
            const origin = layer.path[0];
    // Build routed polyline using simple line connecting all points
    // For better routing, can integrate OSRM (Open Source Routing Machine) in the future
    async function buildClientDirectionsPolyline(dayName, strokeColor) {
        try {
            const layer = dayLayers[dayName];
            if (!layer || !layer.path || layer.path.length < 2) return null;

            // Create simple polyline connecting all waypoints
            const path = layer.path.map(p => ({ lat: p.lat || p[0], lng: p.lng || p[1] }));
            
            if (path.length > 0) {
                const routed = LeafletMapExt.drawPolyline(gmap, path, {
                    color: strokeColor,
                    weight: 3,
                    opacity: 0.9
                });
                return routed;
            }
        } catch (e) {
            console.warn('buildClientDirectionsPolyline error', e);
        }
        return null;
    }

    function addPlaceResultToMap(place) {
        if (!place || !place.geometry || !place.geometry.location) return;
        const lat = place.geometry.location.lat(); const lng = place.geometry.location.lng();
        const dayNum = selectedDay || 1; const name = `Day ${dayNum}`;
        layerManager.addLayer(name);
        if (!dayLayers[name]) {
            const colors = ['#2b7cff', '#34c759', '#ff9500', '#ff3b30', '#af52de', '#ffcc00'];
            dayLayers[name] = { items: [], markers: [], path: [], polyline: null, color: colors[(dayNum - 1) % colors.length], dayNumber: dayNum };
        }

        const layer = dayLayers[name];
        const itemIndex = layer.items.length + 1;

        // build enriched infowindow html
        let photoHtml = '';
        try {
            if (place.photos && place.photos.length) {
                const p = place.photos[0];
                const url = p.getUrl({ maxWidth: 300, maxHeight: 200 });
                photoHtml = `<div class="mb-2"><img src="${url}" style="max-width:100%;height:auto;border-radius:6px;box-shadow: 0 2px 4px rgba(0,0,0,0.1);"/></div>`;
            }
        } catch (e) { /* ignore photo retrieval errors */ }

        const ratingHtml = place.rating
            ? `<div class='small mb-1'>
                <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="#ffc107" class="bi bi-star-fill" viewBox="0 0 16 16" style="margin-right: 3px;">
                    <path d="M3.612 15.443c-.386.198-.824-.149-.746-.592l.83-4.73L.173 6.765c-.329-.314-.158-.888.283-.95l4.898-.696L7.538.792c.197-.39.73-.39.927 0l2.184 4.327 4.898.696c.441.062.612.636.282.95l-3.522 3.356.83 4.73c.078.443-.36.79-.746.592L8 13.187l-4.389 2.256z"/>
                </svg>
                <strong>${place.rating}</strong>/5
               </div>`
            : '';

        const addressHtml = place.formatted_address
            ? `<div class='small text-muted mb-1'>
                <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="currentColor" class="bi bi-geo-alt" viewBox="0 0 16 16" style="margin-right: 5px;">
                    <path d="M12.166 8.94c-.524 1.062-1.234 2.12-1.96 3.07A31.493 31.493 0 0 1 8 14.58a31.481 31.481 0 0 1-2.206-2.57c-.726-.95-1.436-2.008-1.96-3.07C3.304 7.867 3 6.862 3 6a5 5 0 0 1 10 0c0 .862-.305 1.867-.834 2.94zM8 16s6-5.686 6-10A6 6 0 0 0 2 6c0 4.314 6 10 6 10z"/>
                    <path d="M8 8a2 2 0 1 1 0-4 2 2 0 0 1 0 4zm0 1a3 3 0 1 0 0-6 3 3 0 0 0 0 6z"/>
                </svg>
                ${place.formatted_address}
               </div>`
            : '';

        const infoHtml = `
            <div class='p-2' style='min-width: 250px; max-width: 350px;'>
                ${photoHtml}
                <div class='d-flex align-items-center mb-2'>
                    <span class='badge' style='background-color: ${layer.color}; color: white; font-size: 14px; padding: 6px 10px; margin-right: 10px;'>#${itemIndex}</span>
                    <strong style='font-size: 16px;'>${place.name}</strong>
                </div>
                ${ratingHtml}
                ${addressHtml}
                <div class='small text-muted mt-2'>
                    <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="currentColor" class="bi bi-calendar3" viewBox="0 0 16 16" style="margin-right: 5px;">
                        <path d="M14 0H2a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V2a2 2 0 0 0-2-2zM1 3.857C1 3.384 1.448 3 2 3h12c.552 0 1 .384 1 .857v10.286c0 .473-.448.857-1 .857H2c-.552 0-1-.384-1-.857V3.857z"/>
                    </svg>
                    Day ${dayNum} - Stop #${itemIndex}
                </div>
            </div>
        `;

        const marker = LeafletMapExt.addInteractiveMarker(gmap, {
            lat,
            lng,
            title: place.name,
            label: {
                text: String(itemIndex),
                color: '#ffffff',
                fontSize: '12px',
                fontWeight: 'bold'
            },
            icon: {
                path: 'CIRCLE',
                scale: 12,
                fillColor: layer.color,
                fillOpacity: 1,
                strokeColor: '#ffffff',
                strokeWeight: 2
            },
            infoHtml
        }, { 'Remove': (m) => { /* no-op */ } });

        const it = { text: place.name, marker, lat, lng, index: itemIndex };
        layer.items.push(it);
        updateCluster(); renderSidePanel(dayNum);

        // update polyline
        const path = layer.items.map(it => ({ lat: it.lat, lng: it.lng }));
        if (layer.polyline) LeafletMapExt.clearPolyline(layer.polyline);
        if (path.length > 1) { const poly = LeafletMapExt.drawPolyline(gmap, path, { strokeColor: layer.color }); layer.polyline = poly; layerManager.addPolylineTo(name, poly); }
        persistOrder();
    }

    // Persist current dayLayers -> server as ordered textual itinerary
    // Debounced persistence to avoid spamming the server during fast D&D
    let __persistTimer = null;
    const PERSIST_DEBOUNCE_MS = 900;
    let __isSaving = false;

    function showPersistIndicator(on) {
        try {
            const panels = getSidePanelElements();
            if (panels.length === 0) return;

            panels.forEach(sp => {
                let el = sp.querySelector('#persist-spinner');
                if (on) {
                    if (!el) {
                        el = document.createElement('div');
                        el.id = 'persist-spinner';
                        el.className = 'text-muted small ms-2';
                        el.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Saving...';
                        const header = sp.querySelector('.d-flex') || sp;
                        header.appendChild(el);
                    }
                } else {
                    if (el && el.parentNode) el.parentNode.removeChild(el);
                }
            });
        } catch { }
    }

    function persistOrder() {
        if (__persistTimer) clearTimeout(__persistTimer);
        __persistTimer = setTimeout(() => persistOrderImmediate().catch(e => console.warn(e)), PERSIST_DEBOUNCE_MS);
    }

    async function persistOrderImmediate() {
        try {
            const el = document.getElementById('travel-plan-data'); if (!el) return;
            const plan = JSON.parse(el.textContent || '{}'); const id = plan.id;
            if (!id) return;
            __isSaving = true; showPersistIndicator(true);
            const payload = [];
            for (const name in dayLayers) {
                const m = name.match(/Day\s*(\d+)/i);
                const dayNum = m ? parseInt(m[1]) : null;
                if (!dayNum) continue;
                const layer = dayLayers[name];
                const lines = (layer.items || []).map(it => it.text || '');
                payload.push({ day: dayNum, date: '', lines });
            }
            // try to read antiforgery token rendered by Razor: <input name="__RequestVerificationToken" type="hidden" value="..." />
            const tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
            const token = tokenEl ? tokenEl.value : null;
            const headers = { 'Content-Type': 'application/json' };
            if (token) headers['RequestVerificationToken'] = token;

            const res = await fetch(`?handler=SaveOrder&id=${encodeURIComponent(id)}`, {
                method: 'POST', headers, body: JSON.stringify(payload)
            });
            if (!res.ok) {
                const txt = await res.text();
                console.warn('Persist order failed', txt);
                if (window.Toast) window.Toast.error('Failed to save order');
            } else {
                try {
                    const j = await res.json();
                    if (j && j.ok && window.Toast) window.Toast.success('Plan order saved');
                }
                catch {
                    if (window.Toast) window.Toast.success('Plan order saved');
                }
            }
        } catch (e) {
            console.warn('persistOrder error', e);
            if (window.Toast) window.Toast.error('Failed to save order');
        }
        finally { __isSaving = false; showPersistIndicator(false); }
    }

    // Refresh route for specific day when settings change
    async function refreshDayRoute(dayNum) {
        const name = `Day ${dayNum}`;
        const layer = dayLayers[name];
        if (!layer || !layer.path || layer.path.length < 2) return;

        // Clear existing polyline
        if (layer.polyline) {
            try { LeafletMapExt.clearPolyline(layer.polyline); } catch { }
        }
        try {
            const lyr = layerManager.getLayer ? layerManager.getLayer(name) : null;
            if (lyr && Array.isArray(lyr.polylines)) {
                lyr.polylines.forEach(p => { try { LeafletMapExt.clearPolyline(p); } catch { } });
                lyr.polylines = [];
            }
        } catch { }

        // Build new route with current settings
        try {
            const routed = await buildClientDirectionsPolyline(name, layer.color);
            if (routed) {
                layer.polyline = routed;
                layerManager.addPolylineTo(name, routed);
            }
        } catch (e) {
            console.warn('refreshDayRoute failed', e);
        }
    }
})();

