// Travel Planner Results Map - Leaflet Version with Nominatim Geocoding
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

    // Extract location names from day lines (look for 📍 markers or numbered items)
    function extractLocations(lines) {
        const locations = [];
        if (!lines || !Array.isArray(lines)) return locations;

        for (const line of lines) {
            if (!line || typeof line !== 'string') continue;

            // Look for lines with 📍 emoji
            if (line.includes('📍')) {
                let cleaned = line.replace(/📍/g, '').trim();
                // Remove leading numbers, bullets, and formatting
                cleaned = cleaned.replace(/^[\d\.\)\-\*#•\s]+/, '').trim();
                // Remove trailing punctuation
                cleaned = cleaned.replace(/[,:;\.!?]+$/, '').trim();
                
                // Take only the first part before hyphen or colon (usually the place name)
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
            // Also check for numbered items at start of line
            else if (/^\d+[\.\)]\s+[A-Z]/.test(line)) {
                let cleaned = line.replace(/^\d+[\.\)]\s+/, '').trim();
                // Take first part before hyphen/colon
                if (cleaned.includes('-')) {
                    cleaned = cleaned.split('-')[0].trim();
                }
                if (cleaned.includes(':')) {
                    cleaned = cleaned.split(':')[0].trim();
                }
                // Remove any remaining markers
                cleaned = cleaned.replace(/[📍🏛️🎨🍽️🌳⛪🏰🖼️🛍️🎭✨]+/g, '').trim();
                
                if (cleaned.length > 2 && cleaned.length < 100) {
                    locations.push(cleaned);
                }
            }
        }

        // Limit to reasonable number of locations per day
        return locations.slice(0, 8);
    }

    // Geocode a location using Nominatim (OpenStreetMap)
    async function geocodeLocation(placeName, destination) {
        try {
            const query = `${placeName}, ${destination}`;
            const url = `https://nominatim.openstreetmap.org/search?format=json&q=${encodeURIComponent(query)}&limit=1`;
            
            const response = await fetch(url, {
                headers: {
                    'User-Agent': 'TravelPlannerApp/1.0'
                }
            });

            if (!response.ok) {
                console.warn(`Geocoding failed for ${placeName}: HTTP ${response.status}`);
                return null;
            }

            const data = await response.json();
            if (data && data.length > 0) {
                return {
                    lat: parseFloat(data[0].lat),
                    lng: parseFloat(data[0].lon),
                    displayName: data[0].display_name
                };
            }

            console.warn(`No geocoding results for: ${query}`);
            return null;
        } catch (error) {
            console.error(`Geocoding error for ${placeName}:`, error);
            return null;
        }
    }

    // Add delay to respect Nominatim rate limits (1 request per second)
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

        // Create map
        gmap = LeafletMap.createMap('plan-map', { center: { lat: 48.8566, lng: 2.3522 }, zoom: 12 });
        window.__travelPlannerMap = gmap;
        mapEl.__leafletMapInstance = gmap;

        // Parse and render plan data
        const parsed = plan.parsedDays || [];
        if (!parsed || parsed.length === 0) {
            console.warn('No parsed days in plan');
            __initDone = true;
            return;
        }

        const destination = plan.destination || 'Paris, France';
        console.log(`📍 Processing ${parsed.length} days for ${destination}`);
        layerManager = LeafletMapExt.createLayerManager(gmap);

        const colors = ['#2b7cff', '#34c759', '#ff9500', '#ff3b30', '#af52de', '#ffcc00'];

        // Show loading message
        const controlsContainer = document.getElementById('map-controls');
        if (controlsContainer) {
            const loadingMsg = document.createElement('div');
            loadingMsg.className = 'alert alert-info';
            loadingMsg.textContent = '🔄 Loading map locations...';
            loadingMsg.id = 'map-loading';
            controlsContainer.appendChild(loadingMsg);
        }

        // Process each day
        let totalMarkers = 0;
        for (let dayIndex = 0; dayIndex < parsed.length; dayIndex++) {
            const day = parsed[dayIndex];
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

            // Extract locations from day lines
            const locations = extractLocations(day.lines);
            console.log(`Day ${dayNum}: Found ${locations.length} locations:`, locations);

            if (locations.length === 0) continue;

            // Geocode each location with rate limiting
            for (let idx = 0; idx < locations.length; idx++) {
                const placeName = locations[idx];
                
                // Rate limiting: 1 request per second for Nominatim
                if (totalMarkers > 0) {
                    await delay(1100); // 1.1 seconds between requests
                }

                const coords = await geocodeLocation(placeName, destination);
                
                if (!coords) {
                    console.warn(`Skipping ${placeName} - no coordinates found`);
                    continue;
                }

                const itemIndex = idx + 1;

                const infoHtml = `
                    <div style='min-width: 200px; max-width: 300px;'>
                        <div style='display: flex; align-items: center; margin-bottom: 8px;'>
                            <span style='background: ${dayColor}; color: white; padding: 4px 8px; border-radius: 4px; margin-right: 8px;'>${itemIndex}</span>
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
                        scale: 12,
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
                layerManager.addMarkerTo(dayName, marker);
                planMarkers.push(marker);
                totalMarkers++;

                console.log(`✅ Added marker ${totalMarkers}: ${placeName}`);
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
            }
        }

        // Remove loading message
        const loadingMsg = document.getElementById('map-loading');
        if (loadingMsg) {
            loadingMsg.remove();
        }

        console.log(`✅ Created ${totalMarkers} markers across ${Object.keys(dayLayers).length} days`);

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

        // Clear any existing content
        container.innerHTML = '';

        // Create scrollable container for day buttons
        const scrollContainer = document.createElement('div');
        scrollContainer.style.cssText = 'display: flex; flex-wrap: wrap; gap: 8px; max-height: 150px; overflow-y: auto;';

        // Add "All Days" button
        const allBtn = document.createElement('button');
        allBtn.className = 'btn btn-sm btn-primary';
        allBtn.textContent = 'All Days';
        allBtn.setAttribute('data-day', 'all');
        scrollContainer.appendChild(allBtn);

        // Add button for each day
        for (let i = 1; i <= numDays; i++) {
            const btn = document.createElement('button');
            btn.className = 'btn btn-sm btn-outline-primary';
            btn.textContent = `Day ${i}`;
            btn.setAttribute('data-day', i);
            scrollContainer.appendChild(btn);
        }

        container.appendChild(scrollContainer);

        // Attach click handlers
        scrollContainer.querySelectorAll('button').forEach(btn => {
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
