(function () {
    let gmap = null;
    let gcenter = null;
    let geocoder = null;
    const planMarkers = [];
    const dayLayers = {};
    let layerManager = null;
    let markerClusterer = null;
    const polylines = [];
    let selectedDay = null;

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
        const plan = parsePlan();
        if (!plan) return;
        const mapEl = document.getElementById('plan-map');
        if (!mapEl) return;

        // Show loading spinner and hide map
        showLoadingSpinner(true);
        if (mapEl) mapEl.style.visibility = 'hidden';

        try {
            await GoogleMap.ready();
        } catch (e) {
            console.error('Google Maps not ready', e);
            showLoadingSpinner(false);
            if (mapEl) mapEl.style.visibility = 'visible';
            return;
        }

        geocoder = new google.maps.Geocoder();
        gmap = GoogleMap.createMap('plan-map', { center: { lat: 52.0, lng: 19.0 }, zoom: 5 });
        layerManager = GoogleMapExt.createLayerManager(gmap);

        // Center on destination
        const destRes = await geocode(geocoder, plan.destination);
        if (destRes && destRes.geometry && destRes.geometry.location) {
            gcenter = destRes.geometry.location;
            gmap.setCenter(gcenter);
            gmap.setZoom(12);
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
            for (const line of day.lines.slice(0, 25)) {
                const text = line.trim();
                if (!text) continue;
                const q = plan.destination ? `${text}, ${plan.destination}` : text;
                const r = await geocode(geocoder, q);
                if (r && r.geometry && r.geometry.location) {
                    const loc = r.geometry.location;
                    itemIndex++;

                    // Create marker with number label and day color
                    const marker = GoogleMapExt.addInteractiveMarker(gmap, {
                        lat: loc.lat(),
                        lng: loc.lng(),
                        title: text,
                        label: {
                            text: String(itemIndex),
                            color: '#ffffff',
                            fontSize: '12px',
                            fontWeight: 'bold'
                        },
                        icon: {
                            path: google.maps.SymbolPath.CIRCLE,
                            scale: 12,
                            fillColor: dayColor,
                            fillOpacity: 1,
                            strokeColor: '#ffffff',
                            strokeWeight: 2
                        },
                        infoHtml: `
                            <div class='p-2' style='min-width: 200px;'>
                                <div class='d-flex align-items-center mb-2'>
                                    <span class='badge' style='background-color: ${dayColor}; color: white; font-size: 14px; padding: 6px 10px; margin-right: 10px;'>#${itemIndex}</span>
                                    <strong style='font-size: 15px;'>${text}</strong>
                                </div>
                                <div class='small text-muted mb-1'>
                                    <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="currentColor" class="bi bi-calendar3" viewBox="0 0 16 16" style="margin-right: 5px;">
                                        <path d="M14 0H2a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V2a2 2 0 0 0-2-2zM1 3.857C1 3.384 1.448 3 2 3h12c.552 0 1 .384 1 .857v10.286c0 .473-.448.857-1 .857H2c-.552 0-1-.384-1-.857V3.857z"/>
                                        <path d="M6.5 7a1 1 0 1 0 0-2 1 1 0 0 0 0 2zm3 0a1 1 0 1 0 0-2 1 1 0 0 0 0 2zm3 0a1 1 0 1 0 0-2 1 1 0 0 0 0 2zm-9 3a1 1 0 1 0 0-2 1 1 0 0 0 0 2zm3 0a1 1 0 1 0 0-2 1 1 0 0 0 0 2zm3 0a1 1 0 1 0 0-2 1 1 0 0 0 0 2zm3 0a1 1 0 1 0 0-2 1 1 0 0 0 0 2zm-9 3a1 1 0 1 0 0-2 1 1 0 0 0 0 2zm3 0a1 1 0 1 0 0-2 1 1 0 0 0 0 2zm3 0a1 1 0 1 0 0-2 1 1 0 0 0 0 2z"/>
                                    </svg>
                                    Day ${day.day}${day.date ? ' - ' + day.date : ''}
                                </div>
                                <div class='small text-muted'>
                                    <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="currentColor" class="bi bi-geo-alt" viewBox="0 0 16 16" style="margin-right: 5px;">
                                        <path d="M12.166 8.94c-.524 1.062-1.234 2.12-1.96 3.07A31.493 31.493 0 0 1 8 14.58a31.481 31.481 0 0 1-2.206-2.57c-.726-.95-1.436-2.008-1.96-3.07C3.304 7.867 3 6.862 3 6a5 5 0 0 1 10 0c0 .862-.305 1.867-.834 2.94zM8 16s6-5.686 6-10A6 6 0 0 0 2 6c0 4.314 6 10 6 10z"/>
                                        <path d="M8 8a2 2 0 1 1 0-4 2 2 0 0 1 0 4zm0 1a3 3 0 1 0 0-6 3 3 0 0 0 0 6z"/>
                                    </svg>
                                    Stop #${itemIndex} of ${day.lines.length}
                                </div>
                                <div class='small text-muted mt-1'>
                                    📍 ${plan.destination}
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
                    dayLayers[dayName].items.push({ text, marker, lat: loc.lat(), lng: loc.lng(), index: itemIndex });
                    dayLayers[dayName].markers.push(marker);
                    dayLayers[dayName].path.push({ lat: loc.lat(), lng: loc.lng() });
                    planMarkers.push(marker);
                }
            }

            // draw polyline for the day
            if (dayLayers[dayName].path.length > 1) {
                const poly = GoogleMapExt.drawPolyline(gmap, dayLayers[dayName].path, { strokeColor: dayColor });
                dayLayers[dayName].polyline = poly;
                layerManager.addPolylineTo(dayName, poly);
                polylines.push(poly);
            }
        }

        // Hide spinner and show map after all markers are loaded
        showLoadingSpinner(false);
        if (mapEl) mapEl.style.visibility = 'visible';

        // Optionally cluster markers if MarkerClusterer is available

        // initialize/update clusterer (supports different global names)
        updateCluster();

        if (planMarkers.length) GoogleMap.fitToMarkers(gmap, planMarkers);

        // Set default view to "All Days" instead of Day 1
        if (parsed.length) {
            setDayFilter(null); // null = show all days
        }

        if (planMarkers.length) GoogleMap.fitToMarkers(gmap, planMarkers);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    // UI helpers
    function createLayerControls(dayCount) {
        const container = document.getElementById('map-controls');
        if (!container) return;
        // Clear
        container.innerHTML = '';

        // search box
        const searchWrap = document.createElement('div');
        searchWrap.className = 'd-flex mb-2 gap-2';
        const input = document.createElement('input');
        input.type = 'search'; input.id = 'place-search'; input.placeholder = 'Search places, restaurants...';
        input.className = 'form-control form-control-sm';
        const addBtn = document.createElement('button'); addBtn.className = 'btn btn-sm btn-primary'; addBtn.textContent = 'Add';
        searchWrap.appendChild(input); searchWrap.appendChild(addBtn);
        container.appendChild(searchWrap);

        // Master toggle for POI/Hotels/Restaurants could be added later
        const allBtn = document.createElement('button');
        allBtn.className = 'btn btn-sm btn-outline-primary me-2';
        allBtn.textContent = 'ALL DAYS';
        allBtn.addEventListener('click', () => setDayFilter(null));
        container.appendChild(allBtn);

        for (let i = 1; i <= dayCount; i++) {
            const b = document.createElement('button');
            b.className = 'btn btn-sm btn-outline-secondary me-1';
            b.textContent = `Day ${i}`;
            b.addEventListener('click', () => setDayFilter(i));
            container.appendChild(b);
        }

        // side panel toggle button
        const panelToggle = document.createElement('button');
        panelToggle.className = 'btn btn-sm btn-outline-info ms-2';
        panelToggle.textContent = 'Toggle Panel';
        panelToggle.addEventListener('click', () => {
            const sp = document.getElementById('side-panel'); if (!sp) return; sp.classList.toggle('d-none');
        });
        container.appendChild(panelToggle);

        // Initialize Places Autocomplete for the search input (if API loaded)
        let autocomplete = null;
        function initAutocomplete() {
            try {
                const inputEl = document.getElementById('place-search');
                if (!inputEl || !window.google || !google.maps || !google.maps.places) return;
                autocomplete = new google.maps.places.Autocomplete(inputEl, { types: ['establishment', 'geocode'] });
                autocomplete.setFields(['place_id', 'name', 'geometry', 'photos', 'rating', 'formatted_address', 'types']);
                autocomplete.addListener('place_changed', () => {
                    const place = autocomplete.getPlace();
                    if (!place || !place.place_id) return;
                    // fetch place details to ensure photos and full info are available
                    const svc = new google.maps.places.PlacesService(gmap);
                    svc.getDetails({ placeId: place.place_id, fields: ['place_id', 'name', 'geometry', 'photos', 'rating', 'formatted_address', 'types', 'formatted_phone_number', 'opening_hours'] }, (details, status) => {
                        if (status === google.maps.places.PlacesServiceStatus.OK && details) {
                            addPlaceResultToMap(details);
                        } else {
                            // fallback to whatever autocomplete returned
                            addPlaceResultToMap(place);
                        }
                    });
                });
            } catch (e) { console.warn('Autocomplete init failed', e); }
        }

        // Attach autocomplete when maps API is ready (or try now)
        if (window.google && window.google.maps && window.google.maps.places) initAutocomplete();
        // Also expose to global callback in case maps loads later
        window.__initTravelAutocomplete = initAutocomplete;
    }

    function setDayFilter(dayNum) {
        // show only markers and polylines for dayNum (null => all)
        for (const name in dayLayers) {
            const layer = dayLayers[name];
            const show = !dayNum || name.toLowerCase().includes(String(dayNum));
            layerManager.setVisible(name, show);

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
    function renderSidePanel(dayNum) {
        const sp = document.getElementById('side-panel');
        if (!sp) return;
        sp.innerHTML = '';
        if (!dayNum) {
            sp.innerHTML = `
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
            return;
        }
        const name = `Day ${dayNum}`;
        const layer = dayLayers[name];
        if (!layer) { sp.innerHTML = '<div class="px-2 py-2 text-muted">No points for this day.</div>'; return; }

        const list = document.createElement('ul'); list.className = 'list-group list-group-flush';
        layer.items.forEach((it, idx) => {
            const li = document.createElement('li'); li.className = 'list-group-item d-flex align-items-center'; li.draggable = true;
            li.dataset.idx = idx;
            li.innerHTML = `<span class="badge bg-primary me-2">${idx + 1}</span><div class="flex-grow-1">${it.text}</div>`;
            const rm = document.createElement('button'); rm.className = 'btn btn-sm btn-outline-danger ms-2'; rm.textContent = 'Remove';
            rm.addEventListener('click', () => { removeItemFromDay(dayNum, idx); });
            li.appendChild(rm);
            // drag events
            li.addEventListener('dragstart', (e) => { e.dataTransfer.setData('text/plain', String(idx)); li.classList.add('dragging'); });
            li.addEventListener('dragover', (e) => { e.preventDefault(); li.classList.add('dragover'); });
            li.addEventListener('dragleave', () => { li.classList.remove('dragover'); });
            li.addEventListener('drop', (e) => {
                e.preventDefault(); const from = parseInt(e.dataTransfer.getData('text/plain')); const to = parseInt(li.dataset.idx);
                li.classList.remove('dragover');
                reorderDayItems(dayNum, from, to);
            });
            list.appendChild(li);
        });
        sp.appendChild(list);
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
        if (layer.polyline) GoogleMapExt.clearPolyline(layer.polyline);
        const poly = GoogleMapExt.drawPolyline(gmap, path, { strokeColor: layer.color || '#2b7cff' });
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
        if (layer.polyline) GoogleMapExt.clearPolyline(layer.polyline);
        if (path.length > 1) { const poly = GoogleMapExt.drawPolyline(gmap, path, { strokeColor: layer.color || '#2b7cff' }); layer.polyline = poly; layerManager.addPolylineTo(name, poly); }
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

        const marker = GoogleMapExt.addInteractiveMarker(gmap, {
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
                path: google.maps.SymbolPath.CIRCLE,
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
        if (layer.polyline) GoogleMapExt.clearPolyline(layer.polyline);
        if (path.length > 1) { const poly = GoogleMapExt.drawPolyline(gmap, path, { strokeColor: layer.color }); layer.polyline = poly; layerManager.addPolylineTo(name, poly); }
        persistOrder();
    }

    // Persist current dayLayers -> server as ordered textual itinerary
    // Debounced persistence to avoid spamming the server during fast D&D
    let __persistTimer = null;
    const PERSIST_DEBOUNCE_MS = 900;
    let __isSaving = false;

    function showPersistIndicator(on) {
        try {
            const sp = document.getElementById('side-panel'); if (!sp) return;
            let el = document.getElementById('persist-spinner');
            if (on) {
                if (!el) {
                    el = document.createElement('div'); el.id = 'persist-spinner'; el.className = 'text-muted small ms-2';
                    el.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Saving...';
                    const header = sp.querySelector('.d-flex') || sp;
                    header.appendChild(el);
                }
            } else {
                if (el && el.parentNode) el.parentNode.removeChild(el);
            }
        } catch (e) { /* ignore */ }
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
                showNotification('Failed to save order', 'error');
            } else {
                try { const j = await res.json(); if (j && j.ok) showNotification('Plan order saved', 'success'); }
                catch { showNotification('Plan order saved', 'success'); }
            }
        } catch (e) { console.warn('persistOrder error', e); showNotification('Failed to save order', 'error'); }
        finally { __isSaving = false; showPersistIndicator(false); }
    }
})();
