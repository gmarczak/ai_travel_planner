// Travel Planner Results Map - Leaflet Version with Fallback Locations
(function () {
    'use strict';

    let gmap = null;
    let layerManager = null;
    let planMarkers = [];
    let dayLayers = {};
    let selectedDay = null;

    let __initDone = false;
    let __initStarted = false;

    // Default coordinates for major cities
    const fallbackCoords = {
        'paris': { lat: 48.8566, lng: 2.3522 },
        'tokyo': { lat: 35.6762, lng: 139.6503 },
        'new york': { lat: 40.7128, lng: -74.0060 },
        'london': { lat: 51.5074, lng: -0.1278 },
        'barcelona': { lat: 41.3851, lng: 2.1734 },
        'rome': { lat: 41.9028, lng: 12.4964 },
        'shanghai': { lat: 31.2304, lng: 121.4737 },
        'sydney': { lat: -33.8688, lng: 151.2093 }
    };

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

    // Extract location names from day lines
    function extractLocations(lines) {
        const locations = [];
        if (!lines || !Array.isArray(lines)) return locations;

        for (const line of lines) {
            if (!line || typeof line !== 'string') continue;

            // Look for lines with 📍 emoji
            if (line.includes('📍')) {
                let cleaned = line.replace(/📍/g, '').trim();
                cleaned = cleaned.replace(/^[\d\.\)\-\*#•\s]+/, '').trim();
                cleaned = cleaned.replace(/[,:;\.!?]+$/, '').trim();
                
                if (cleaned.includes('-')) {
                    cleaned = cleaned.split('-')[0].trim();
                }
                if (cleaned.includes(':')) {
                    cleaned = cleaned.split(':')[0].trim();
                }

                if (cleaned.length > 2 && cleaned.length < 100) {
                    locations.push(cleaned);
                }
            }
        }

        return locations.slice(0, 8);
    }

    // Geocode with fallback to simple offset algorithm
    async function geocodeLocation(placeName, destination, dayNum, itemIndex) {
        try {
            const query = `${placeName}, ${destination}`;
            const url = `https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(query)}&limit=1`;
            
            const response = await fetch(url, {
                headers: { 'User-Agent': 'TravelPlannerApp/1.0' }
            });

            if (response.ok) {
                const data = await response.json();
                if (data && data.length > 0) {
                    console.log(`✅ Geocoded ${placeName} to ${data[0].lat}, ${data[0].lon}`);
                    return {
                        lat: parseFloat(data[0].lat),
                        lng: parseFloat(data[0].lon)
                    };
                }
            }
        } catch (error) {
            console.warn(`Geocoding error for ${placeName}:`, error.message);
        }

        // Fallback: use destination's known coordinates + offset
        let baseLat = 48.8566, baseLng = 2.3522; // Default to Paris
        
        // Check if destination matches known city
        const destLower = destination.toLowerCase();
        for (const [city, coords] of Object.entries(fallbackCoords)) {
            if (destLower.includes(city)) {
                baseLat = coords.lat;
                baseLng = coords.lng;
                break;
            }
        }

        // Add small random offset so points spread out
        const offset = 0.01 * (dayNum + itemIndex);
        const lat = baseLat + offset;
        const lng = baseLng + offset;

        console.log(`⚠️ Using fallback coords for ${placeName}: ${lat.toFixed(4)}, ${lng.toFixed(4)}`);
        return { lat, lng };
    }

    function delay(ms) {
        return new Promise(resolve => setTimeout(resolve, ms));
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

        // Create map with larger zoom for Paris
        const destination = plan.destination || 'Paris, France';
        let defaultCenter = { lat: 48.8566, lng: 2.3522 }; // Paris
        
        // Check destination to use better default
        const destLower = destination.toLowerCase();
        for (const [city, coords] of Object.entries(fallbackCoords)) {
            if (destLower.includes(city)) {
                defaultCenter = coords;
                break;
            }
        }

        gmap = LeafletMap.createMap('plan-map', { center: defaultCenter, zoom: 12 });
        window.__travelPlannerMap = gmap;
        mapEl.__leafletMapInstance = gmap;
        console.log('✅ Map created and stored in window.__travelPlannerMap');

        // Parse and render plan data
        const parsed = plan.parsedDays || [];
        if (!parsed || parsed.length === 0) {
            console.warn('No parsed days in plan');
            __initDone = true;
            return;
        }

        console.log(`📍 Processing ${parsed.length} days for ${destination}`);
        layerManager = LeafletMapExt.createLayerManager(gmap);

        const colors = ['#FF6B6B', '#4ECDC4', '#45B7D1', '#FFA07A', '#98D8C8', '#F7DC6F', '#BB8FCE', '#85C1E2'];

        // Remove loading message if exists
        const loadingMsg = document.querySelector('.alert-info');
        if (loadingMsg && loadingMsg.id !== 'map-loading') {
            loadingMsg.remove();
        }

        // Process each day
        let totalMarkers = 0;
        for (let dayIndex = 0; dayIndex < parsed.length; dayIndex++) {
            const day = parsed[dayIndex];
            const dayNum = dayIndex + 1;
            const dayName = `Day ${dayNum}`;
            const dayColor = colors[dayIndex % colors.length];

            console.log(`🎨 Day ${dayNum} color: ${dayColor}`);

            dayLayers[dayName] = {
                items: [],
                markers: [],
                path: [],
                polyline: null,
                color: dayColor,
                dayNumber: dayNum
            };

            layerManager.addLayer(dayName);

            // Extract locations from day lines
            const locations = extractLocations(day.lines);
            console.log(`Day ${dayNum}: Found ${locations.length} locations:`, locations);

            if (locations.length === 0) {
                console.log(`Day ${dayNum}: No locations found, skipping`);
                continue;
            }

            // Geocode each location with rate limiting
            for (let idx = 0; idx < locations.length; idx++) {
                const placeName = locations[idx];
                
                // Rate limiting: 1 request per second for Nominatim
                if (totalMarkers > 0) {
                    await delay(1100);
                }

                const coords = await geocodeLocation(placeName, destination, dayNum, idx);
                
                if (!coords) {
                    console.warn(`Skipping ${placeName} - no coordinates found`);
                    continue;
                }

                const itemIndex = idx + 1;

                const infoHtml = `
                    <div style='min-width: 200px; max-width: 300px;'>
                        <div style='display: flex; align-items: center; margin-bottom: 8px;'>
                            <span style='background: ${dayColor}; color: white; padding: 4px 8px; border-radius: 4px; margin-right: 8px; font-weight: bold;'>${itemIndex}</span>
                            <strong>${placeName}</strong>
                        </div>
                        <div style='font-size: 12px; color: #666;'>Day ${dayNum} - Stop #${itemIndex}</div>
                        <div style='font-size: 11px; color: #999; margin-top: 4px;'>${coords.lat.toFixed(4)}, ${coords.lng.toFixed(4)}</div>
                    </div>
                `;

                const marker = LeafletMapExt.addInteractiveMarker(gmap, {
                    lat: coords.lat,
                    lng: coords.lng,
                    title: placeName,
                    label: {
                        text: String(itemIndex),
                        color: '#ffffff',
                        fontSize: '12px',
                        fontWeight: 'bold'
                    },
                    icon: {
                        path: 'CIRCLE',
                        scale: 14,
                        fillColor: dayColor,
                        fillOpacity: 1,
                        strokeColor: '#ffffff',
                        strokeWeight: 2
                    },
                    infoHtml: infoHtml
                }, {});

                dayLayers[dayName].items.push({
                    text: placeName,
                    marker: marker,
                    lat: coords.lat,
                    lng: coords.lng,
                    index: itemIndex
                });

                dayLayers[dayName].path.push({ lat: coords.lat, lng: coords.lng });
                
                // Add marker to layer
                layerManager.addMarkerTo(dayName, marker);
                planMarkers.push(marker);
                totalMarkers++;

                console.log(`✅ Marker ${totalMarkers} added: ${placeName} (Day ${dayNum}, Stop ${itemIndex})`);
            }

            // Draw polyline connecting places in the day
            if (dayLayers[dayName].path.length > 1) {
                const polyline = LeafletMapExt.drawPolyline(gmap, dayLayers[dayName].path, {
                    color: dayColor,
                    weight: 3,
                    opacity: 0.7
                });
                dayLayers[dayName].polyline = polyline;
                layerManager.addPolylineTo(dayName, polyline);
                console.log(`✅ Polyline added for Day ${dayNum} (${dayLayers[dayName].path.length} points)`);
            }
        }

        console.log(`✅ Total ${totalMarkers} markers created across ${Object.keys(dayLayers).length} days`);

        // Fit map to show all markers
        if (planMarkers.length > 0) {
            console.log(`📍 Fitting map to ${planMarkers.length} markers`);
            LeafletMap.fitToMarkers(gmap, planMarkers);
        } else {
            console.warn('No markers to fit');
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

        // Clear any existing content
        container.innerHTML = '';

        // Add "All Days" button
        const allBtn = document.createElement('button');
        allBtn.className = 'btn btn-sm btn-primary';
        allBtn.textContent = 'All Days';
        allBtn.setAttribute('data-day', 'all');
        container.appendChild(allBtn);

        // Add button for each day
        for (let i = 1; i <= numDays; i++) {
            const btn = document.createElement('button');
            btn.className = 'btn btn-sm btn-outline-primary';
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

        console.log(`✅ Created ${numDays + 1} filter buttons`);
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
                console.log(`👁️ Showing ${name}`);
            } else {
                layerManager.setVisible(name, false);
                console.log(`👁️ Hiding ${name}`);
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
