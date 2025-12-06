(function () {
    const state = { loaded: false, waiters: [] };

    function _onLoaded() {
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
        const map = new google.maps.Map(el, Object.assign({
            center: { lat: 0, lng: 0 },
            zoom: 2,
            mapTypeControl: false,
            streetViewControl: false,
            fullscreenControl: true
        }, options || {}));
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
