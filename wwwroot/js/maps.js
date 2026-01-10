(function () {
    console.log('🗺️ [maps.js] Initializing Google Maps wrapper...');
    const state = { loaded: false, waiters: [] };

    function _onLoaded() {
        console.log('✅ [maps.js] Google Maps API loaded successfully');
        state.loaded = true;
        while (state.waiters.length) {
            const w = state.waiters.shift();
            try { w && w(); } catch { }
        }
    }

    function ready(timeoutMs = 15000) {
        if (state.loaded && window.google && window.google.maps) return Promise.resolve();
        return new Promise((resolve, reject) => {
            const t = setTimeout(() => reject(new Error("Google Maps failed to load (timeout).")), timeoutMs);
            state.waiters.push(() => { clearTimeout(t); resolve(); });
        });
    }

    function getElement(target) {
        if (typeof target === "string") {
            const el = document.getElementById(target);
            if (!el) throw new Error("Element not found: " + target);
            return el;
        }
        return target;
    }

    function createMap(target, options) {
        const el = getElement(target);
        if (el.__googleMapInstance) return el.__googleMapInstance;
        console.log('📍 [maps.js] Creating map with options:', options);
        const defaults = {
            center: { lat: 0, lng: 0 },
            zoom: 2,
            mapTypeId: google.maps.MapTypeId.ROADMAP,
            mapTypeControl: true,
            mapTypeControlOptions: {
                style: google.maps.MapTypeControlStyle.DROPDOWN_MENU,
                position: google.maps.ControlPosition.TOP_RIGHT
            },
            streetViewControl: false,
            fullscreenControl: true
        };
        // Merge so that passed options override defaults, but force mapTypeId to ROADMAP
        const finalOptions = Object.assign({}, defaults, options || {});
        finalOptions.mapTypeId = google.maps.MapTypeId.ROADMAP; // FORCE ROADMAP using enum
        console.log('🗺️ [maps.js] Final map options:', JSON.stringify({
            mapTypeId: finalOptions.mapTypeId,
            zoom: finalOptions.zoom,
            center: finalOptions.center,
            mapTypeControl: finalOptions.mapTypeControl
        }));
        const map = new google.maps.Map(el, finalOptions);
        console.log('✅ [maps.js] Map created, mapTypeId:', map.getMapTypeId());
        
        // Diagnostic: Check if map is in "lite mode" (happens when billing disabled or API restricted)
        setTimeout(() => {
            const div = el.querySelector('div');
            if (div) {
                const style = window.getComputedStyle(div);
                console.log('🔍 [maps.js] Map container background:', style.backgroundColor);
                
                // Check for tile layers - log ACTUAL tile image URLs
                const allImages = el.querySelectorAll('img');
                console.log('🔍 [maps.js] All images in map container:', allImages.length);
                
                const tileImages = [];
                console.log('🔍 [maps.js] ===== IMAGE DETAILS =====');
                allImages.forEach((img, idx) => {
                    const src = img.src || '(no src)';
                    console.log(`  Image #${idx}: ${src}`);
                    
                    if (src.includes('googleapis.com') || src.includes('google.com/maps') || src.includes('khms') || src.includes('mts')) {
                        tileImages.push(src);
                    }
                });
                console.log('🔍 [maps.js] ===== END IMAGE DETAILS =====');
                
                console.log('🔍 [maps.js] Tile images (mts/khms/googleapis):', tileImages.length);
                
                if (tileImages.length === 0) {
                    console.error('❌ [maps.js] NO MAP TILES LOADED!');
                    console.error('Background color is:', style.backgroundColor, '(should be white with roads)');
                    console.error('Common causes:');
                    console.error('1. Billing not enabled in Google Cloud Console');
                    console.error('2. API key referrer restrictions blocking localhost');
                    console.error('3. Maps JavaScript API not enabled');
                    console.error('4. Exceeded API quota');
                    console.error('');
                    console.error('FIX: Go to https://console.cloud.google.com/');
                    console.error('  → Navigation menu → Billing → Link billing account');
                    console.error('  → APIs & Services → Credentials → Edit API key');
                    console.error('  → Application restrictions → Add: http://localhost:5000/*');
                }
            }
            
            if (map.getMapTypeId() !== google.maps.MapTypeId.ROADMAP) {
                console.warn('⚠️ [maps.js] Map type changed! Forcing back to ROADMAP...');
                map.setMapTypeId(google.maps.MapTypeId.ROADMAP);
            }
        }, 2000); // Wait 2s for tiles to load
        
        el.__googleMapInstance = map;
        return map;
    }

    function addMarker(map, point) {
        const marker = new google.maps.Marker({
            position: { lat: point.lat, lng: point.lng },
            map,
            title: point.title || undefined,
            icon: point.icon || undefined,
            label: point.label || undefined
        });
        // Only add InfoWindow if NOT being called from addInteractiveMarker
        // (addInteractiveMarker will handle its own InfoWindow)
        if (point.infoHtml && !point._skipAutoInfo) {
            const info = new google.maps.InfoWindow({ content: point.infoHtml });
            marker.addListener("click", () => info.open({ anchor: marker, map }));
        }
        return marker;
    }

    // Create a marker and attach a custom infowindow with action callbacks
    function addInteractiveMarker(map, point, actions) {
        // Mark point to skip auto InfoWindow creation in addMarker
        const modifiedPoint = { ...point, _skipAutoInfo: true };
        const marker = addMarker(map, modifiedPoint);
        if (point.infoHtml || actions) {
            const content = document.createElement('div');
            if (point.infoHtml) content.innerHTML = point.infoHtml;
            if (actions && typeof actions === 'object') {
                const btnWrap = document.createElement('div');
                btnWrap.style.marginTop = '6px';
                Object.keys(actions).forEach(k => {
                    const b = document.createElement('button');
                    b.textContent = k;
                    b.className = 'btn btn-sm btn-outline-primary me-1';
                    b.addEventListener('click', (ev) => { ev.preventDefault(); actions[k](marker); });
                    btnWrap.appendChild(b);
                });
                content.appendChild(btnWrap);
            }
            const info = new google.maps.InfoWindow({ content });
            marker.addListener('click', () => info.open({ anchor: marker, map }));
        }
        return marker;
    }

    function addMarkers(map, points) { return (points || []).map(p => addMarker(map, p)); }

    function addInteractiveMarkers(map, points, actions) { return (points || []).map(p => addInteractiveMarker(map, p, actions)); }

    function fitToMarkers(map, markers, padding = 40) {
        if (!markers || !markers.length) return;
        const bounds = new google.maps.LatLngBounds();
        markers.forEach(m => bounds.extend(m.getPosition()));
        map.fitBounds(bounds, padding);
    }

    // POLYLINE HELPERS
    function drawPolyline(map, pathCoords, opts) {
        const poly = new google.maps.Polyline(Object.assign({
            path: pathCoords.map(p => ({ lat: p.lat, lng: p.lng })),
            geodesic: true,
            strokeOpacity: 0.9,
            strokeWeight: 4
        }, opts || {}));
        poly.setMap(map);
        return poly;
    }

    function clearPolyline(poly) { if (poly) poly.setMap(null); }

    // LAYER MANAGER: simple on/off layers of markers/polylines
    function createLayerManager(map) {
        const layers = {};
        return {
            addLayer(name) { layers[name] = { markers: [], polylines: [], visible: true }; },
            addMarkerTo(name, marker) { if (!layers[name]) this.addLayer(name); layers[name].markers.push(marker); },
            addPolylineTo(name, poly) { if (!layers[name]) this.addLayer(name); layers[name].polylines.push(poly); },
            setVisible(name, visible) {
                const l = layers[name]; if (!l) return;
                l.visible = visible;
                l.markers.forEach(m => m.setMap(visible ? map : null));
                l.polylines.forEach(p => p.setMap(visible ? map : null));
            },
            setMarkersVisible(name, visible) {
                const l = layers[name]; if (!l) return;
                l.markers.forEach(m => m.setMap(visible ? map : null));
            },
            setPolylinesVisible(name, visible) {
                const l = layers[name]; if (!l) return;
                l.polylines.forEach(p => p.setMap(visible ? map : null));
            },
            clearLayer(name) { const l = layers[name]; if (!l) return; l.markers.forEach(m => m.setMap(null)); l.polylines.forEach(p => p.setMap(null)); layers[name] = { markers: [], polylines: [], visible: false }; },
            getLayerNames() { return Object.keys(layers); },
            getLayer(name) { return layers[name]; }
        };
    }

    window.gm_authFailure = function () {
        console.error("Google Maps authentication failure.");
    };

    window.GoogleMap = { _onLoaded, ready, createMap, addMarker, addMarkers, fitToMarkers };
    // expose extended API
    window.GoogleMapExt = { addInteractiveMarker, addInteractiveMarkers, drawPolyline, clearPolyline, createLayerManager };
    window.__gmapsOnLoad = _onLoaded;
})();
