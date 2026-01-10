(function () {
    console.log('🗺️ [leaflet-maps.js] Initializing OpenStreetMap wrapper (Leaflet)...');
    const state = { loaded: false, waiters: [] };

    function _onLoaded() {
        console.log('✅ [leaflet-maps.js] Leaflet API loaded successfully');
        state.loaded = true;
        while (state.waiters.length) {
            const w = state.waiters.shift();
            try { w && w(); } catch { }
        }
    }

    function ready(timeoutMs = 15000) {
        if (state.loaded && window.L) return Promise.resolve();
        return new Promise((resolve, reject) => {
            const t = setTimeout(() => reject(new Error("Leaflet failed to load (timeout).")), timeoutMs);
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
        if (el.__leafletMapInstance) return el.__leafletMapInstance;
        console.log('📍 [leaflet-maps.js] Creating map with options:', options);
        
        const defaults = {
            center: [0, 0], // [lat, lng]
            zoom: 2,
            layers: [L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                attribution: '© OpenStreetMap contributors',
                maxZoom: 19
            })],
            attributionControl: true,
            zoomControl: true
        };
        
        const finalOptions = Object.assign({}, defaults, options || {});
        
        // Ensure we have center as array [lat, lng]
        if (finalOptions.center && finalOptions.center.lat !== undefined) {
            finalOptions.center = [finalOptions.center.lat, finalOptions.center.lng];
        }
        
        console.log('🗺️ [leaflet-maps.js] Final map options:', JSON.stringify({
            center: finalOptions.center,
            zoom: finalOptions.zoom
        }));
        
        const map = L.map(el, finalOptions);
        console.log('✅ [leaflet-maps.js] Map created');
        
        // Store reference
        el.__leafletMapInstance = map;
        return map;
    }

    function addMarker(map, point) {
        const lat = point.lat;
        const lng = point.lng;
        const title = point.title || undefined;
        
        // Build marker options
        const markerOpts = {};
        
        // Handle icon
        if (point.icon) {
            // For custom icon with path (Google Maps SymbolPath equivalent)
            if (point.icon.path === 'CIRCLE') {
                // Create a circle marker with color
                const marker = L.circleMarker([lat, lng], {
                    radius: point.icon.scale || 8,
                    fillColor: point.icon.fillColor || '#2b7cff',
                    color: point.icon.strokeColor || '#ffffff',
                    weight: point.icon.strokeWeight || 2,
                    opacity: 1,
                    fillOpacity: point.icon.fillOpacity || 1,
                    title: title
                });
                
                // Add label if provided
                if (point.label) {
                    const labelText = typeof point.label === 'string' 
                        ? point.label 
                        : point.label.text || '';
                    marker.bindTooltip(labelText, { permanent: true, direction: 'center', className: 'marker-label' });
                }
                
                marker.addTo(map);
                return marker;
            }
        }
        
        // Default marker
        const marker = L.marker([lat, lng], { title: title });
        
        // Add label if provided
        if (point.label) {
            const labelText = typeof point.label === 'string' 
                ? point.label 
                : point.label.text || '';
            marker.bindPopup(labelText);
        }
        
        // Add popup (info window)
        if (point.infoHtml && !point._skipAutoInfo) {
            marker.bindPopup(point.infoHtml);
        }
        
        marker.addTo(map);
        return marker;
    }

    function addInteractiveMarker(map, point, actions) {
        // Mark point to skip auto popup creation
        const modifiedPoint = { ...point, _skipAutoInfo: true };
        const marker = addMarker(map, modifiedPoint);
        
        if (point.infoHtml || actions) {
            const content = document.createElement('div');
            content.style.cssText = 'max-width: 350px;';
            
            if (point.infoHtml) {
                const htmlDiv = document.createElement('div');
                htmlDiv.innerHTML = point.infoHtml;
                content.appendChild(htmlDiv);
            }
            
            if (actions && typeof actions === 'object') {
                const btnWrap = document.createElement('div');
                btnWrap.style.marginTop = '6px';
                Object.keys(actions).forEach(k => {
                    const b = document.createElement('button');
                    b.textContent = k;
                    b.className = 'btn btn-sm btn-outline-primary me-1';
                    b.style.cssText = 'padding: 4px 8px; font-size: 12px;';
                    b.addEventListener('click', (ev) => { ev.preventDefault(); actions[k](marker); });
                    btnWrap.appendChild(b);
                });
                content.appendChild(btnWrap);
            }
            
            marker.bindPopup(content);
        }
        
        return marker;
    }

    function addMarkers(map, points) { 
        return (points || []).map(p => addMarker(map, p)); 
    }

    function addInteractiveMarkers(map, points, actions) { 
        return (points || []).map(p => addInteractiveMarker(map, p, actions)); 
    }

    function fitToMarkers(map, markers, padding = 40) {
        if (!markers || !markers.length) return;
        const group = new L.featureGroup(markers);
        map.fitBounds(group.getBounds(), { padding: [padding, padding] });
    }

    function invalidateSize(map) {
        if (map && map.invalidateSize) {
            map.invalidateSize(true);
        }
    }

    // POLYLINE HELPERS
    function drawPolyline(map, pathCoords, opts) {
        const latlngs = pathCoords.map(p => [p.lat, p.lng]);
        const poly = L.polyline(latlngs, Object.assign({
            color: '#2b7cff',
            weight: 4,
            opacity: 0.9,
            lineCap: 'round',
            lineJoin: 'round'
        }, opts || {}));
        poly.addTo(map);
        return poly;
    }

    function clearPolyline(poly) { 
        if (poly && poly.remove) poly.remove(); 
    }

    // LAYER MANAGER: simple on/off layers of markers/polylines
    function createLayerManager(map) {
        const layers = {};
        return {
            addLayer(name) { 
                layers[name] = { markers: [], polylines: [], visible: true }; 
            },
            addMarkerTo(name, marker) { 
                if (!layers[name]) this.addLayer(name); 
                layers[name].markers.push(marker); 
            },
            addPolylineTo(name, poly) { 
                if (!layers[name]) this.addLayer(name); 
                layers[name].polylines.push(poly); 
            },
            setVisible(name, visible) {
                const l = layers[name]; 
                if (!l) return;
                l.visible = visible;
                l.markers.forEach(m => {
                    if (visible) m.addTo(map);
                    else m.remove();
                });
                l.polylines.forEach(p => {
                    if (visible) p.addTo(map);
                    else p.remove();
                });
            },
            setMarkersVisible(name, visible) {
                const l = layers[name]; 
                if (!l) return;
                l.markers.forEach(m => {
                    if (visible) m.addTo(map);
                    else m.remove();
                });
            },
            setPolylinesVisible(name, visible) {
                const l = layers[name]; 
                if (!l) return;
                l.polylines.forEach(p => {
                    if (visible) p.addTo(map);
                    else p.remove();
                });
            },
            clearLayer(name) { 
                const l = layers[name]; 
                if (!l) return; 
                l.markers.forEach(m => m.remove()); 
                l.polylines.forEach(p => p.remove()); 
                layers[name] = { markers: [], polylines: [], visible: false }; 
            },
            getLayerNames() { return Object.keys(layers); },
            getLayer(name) { return layers[name]; }
        };
    }

    // Styling for marker labels
    const style = document.createElement('style');
    style.textContent = `
        .marker-label {
            background: transparent !important;
            border: none !important;
            font-weight: bold;
            color: white;
            font-size: 12px;
            text-shadow: 
                -1px -1px 0 #000,
                1px -1px 0 #000,
                -1px 1px 0 #000,
                1px 1px 0 #000;
        }
    `;
    document.head.appendChild(style);

    // Export API - matches Google Maps interface for compatibility
    window.LeafletMap = { _onLoaded, ready, createMap, addMarker, addMarkers, fitToMarkers, invalidateSize };
    window.LeafletMapExt = { addInteractiveMarker, addInteractiveMarkers, drawPolyline, clearPolyline, createLayerManager };
    
    // Also expose as GoogleMap for backward compatibility during transition
    window.GoogleMap = window.LeafletMap;
    window.GoogleMapExt = window.LeafletMapExt;
    
    // Call when Leaflet is ready
    if (window.L) {
        _onLoaded();
    } else {
        // Set up listener for when Leaflet loads
        window.__leafletOnLoad = _onLoaded;
    }
})();
