// Travel Planner Results Map - Leaflet Version with Polyline Decoding
(function () {
    'use strict';

    let gmap = null;
    let layerManager = null;
    let planMarkers = [];
    let dayLayers = {};
    let selectedDay = null;

    let __initDone = false;
    let __initStarted = false;

    function parsePlan() {
        const el = document.getElementById('travel-plan-data');
        if (!el || !el.textContent) {
            console.error('No travel plan data found');
            return null;
        }
        try {
            return JSON.parse(el.textContent);
        } catch (e) {
            console.error('Failed to parse travel plan data:', e);
            return null;
        }
    }

    // Decode Google polyline format to lat/lng coordinates
    function decodePolyline(encoded) {
        if (!encoded) return [];
        const poly = [];
        let index = 0, len = encoded.length;
        let lat = 0, lng = 0;

        while (index < len) {
            let b, shift = 0, result = 0;
            do {
                b = encoded.charCodeAt(index++) - 63;
                result |= (b & 0x1f) << shift;
                shift += 5;
            } while (b >= 0x20);
            const dlat = ((result & 1) ? ~(result >> 1) : (result >> 1));
            lat += dlat;

            shift = 0;
            result = 0;
            do {
                b = encoded.charCodeAt(index++) - 63;
                result |= (b & 0x1f) << shift;
                shift += 5;
            } while (b >= 0x20);
            const dlng = ((result & 1) ? ~(result >> 1) : (result >> 1));
            lng += dlng;

            poly.push({ lat: lat / 1e5, lng: lng / 1e5 });
        }
        return poly;
    }

    async function init() {
        if (__initStarted) {
            console.log('🗺️ Init already started');
            return;
        }
        __initStarted = true;

        console.log('🗺️ Starting results-map init');

        const mapEl = document.getElementById('plan-map');
        if (!mapEl) {
            console.error('Map element not found');
            return;
        }

        const plan = parsePlan();
        if (!plan) {
            console.error('No plan data');
            __initDone = true;
            return;
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

        console.log(`📍 Processing ${parsed.length} days on map`);
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

            // Decode route polylines to get coordinates
            const routePolylines = day.routePolylines || [];
            
            if (routePolylines.length === 0) {
                console.log(`Day ${dayNum}: No route polylines available`);
                return;
            }

            const allPoints = [];
            routePolylines.forEach(encoded => {
                const points = decodePolyline(encoded);
                allPoints.push(...points);
            });

            console.log(`Day ${dayNum}: Decoded ${allPoints.length} points from ${routePolylines.length} polylines`);

            if (allPoints.length === 0) return;

            // Sample points from the route to create markers (start, end, and some intermediate)
            const markerPoints = [];
            markerPoints.push(allPoints[0]); // Start
            
            // Add intermediate points - but not too many
            const maxMarkers = 8;
            if (allPoints.length > 2) {
                const step = Math.max(1, Math.floor(allPoints.length / maxMarkers));
                for (let i = step; i < allPoints.length - 1; i += step) {
                    if (markerPoints.length < maxMarkers - 1) {
                        markerPoints.push(allPoints[i]);
                    }
                }
            }
            
            if (allPoints.length > 1 && markerPoints.length < maxMarkers) {
                markerPoints.push(allPoints[allPoints.length - 1]); // End
            }

            console.log(`Day ${dayNum}: Creating ${markerPoints.length} markers`);

            // Add markers for sampled points
            markerPoints.forEach((point, idx) => {
                const itemIndex = idx + 1;
                const name = `Stop ${itemIndex}`;

                const infoHtml = `
                    <div style='min-width: 200px; max-width: 300px;'>
                        <div style='display: flex; align-items: center; margin-bottom: 8px;'>
                            <span style='background: ${dayColor}; color: white; padding: 4px 8px; border-radius: 4px; margin-right: 8px;'>${itemIndex}</span>
                            <strong>${name}</strong>
                        </div>
                        <div style='font-size: 12px; color: #666;'>Day ${dayNum} - Stop #${itemIndex}</div>
                        <div style='font-size: 11px; color: #999; margin-top: 4px;'>${point.lat.toFixed(4)}, ${point.lng.toFixed(4)}</div>
                    </div>
                `;

                const marker = LeafletMapExt.addInteractiveMarker(gmap, {
                    lat: point.lat,
                    lng: point.lng,
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
                    lat: point.lat,
                    lng: point.lng,
                    index: itemIndex
                });

                layerManager.addMarkerTo(dayName, marker);
                planMarkers.push(marker);
            });

            // Draw full polyline using all points
            if (allPoints.length > 1) {
                const polyline = LeafletMapExt.drawPolyline(gmap, allPoints, {
                    color: dayColor,
                    weight: 3,
                    opacity: 0.7
                });
                dayLayers[dayName].polyline = polyline;
                layerManager.addPolylineTo(dayName, polyline);
            }
        });

        console.log(`✅ Created ${planMarkers.length} markers across ${Object.keys(dayLayers).length} days`);

        // Fit map to show all markers
        if (planMarkers.length > 0) {
            LeafletMap.fitToMarkers(gmap, planMarkers);
        }

        // Setup day filter controls
        setupDayFilters(Object.keys(dayLayers).length);

        __initDone = true;
        console.log('✅ Map initialization complete');
    }

    function setupDayFilters(numDays) {
        const container = document.getElementById('map-controls');
        if (!container) {
            console.warn('Map controls container not found');
            return;
        }

        container.innerHTML = '';

        // Add "All Days" button
        const allBtn = document.createElement('button');
        allBtn.className = 'btn btn-sm btn-primary me-2 mb-2';
        allBtn.textContent = 'All Days';
        allBtn.setAttribute('data-day', 'all');
        container.appendChild(allBtn);

        // Add button for each day
        for (let i = 1; i <= numDays; i++) {
            const btn = document.createElement('button');
            btn.className = 'btn btn-sm btn-outline-primary me-2 mb-2';
            btn.textContent = `Day ${i}`;
            btn.setAttribute('data-day', i);
            container.appendChild(btn);
        }

        // Attach click handlers
        container.querySelectorAll('button').forEach(btn => {
            btn.addEventListener('click', function () {
                const day = this.getAttribute('data-day');
                if (day === 'all') {
                    setDayFilter(null);
                } else {
                    setDayFilter(parseInt(day));
                }
            });
        });

        console.log(`✅ Created filter buttons for ${numDays} days`);
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
