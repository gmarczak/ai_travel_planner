// Initializes Google Places Autocomplete on destination inputs when Maps API is ready.
// Targets home page search bar and travel planner destination input.
// NOTE: google.maps.places.Autocomplete is deprecated as of March 2025.
// TODO: Migrate to google.maps.places.PlaceAutocompleteElement (Web Component)
// See: https://developers.google.com/maps/documentation/javascript/places-migration-overview
(function () {
    function init() {
        if (!window.google || !google.maps || !google.maps.places) { return; }
        const homeInput = document.getElementById('homeDestinationInput');
        const plannerInput = document.getElementById('destinationInput');
        const departureInput = document.getElementById('departureInput');
        const options = { types: ['(cities)'] }; // Suggest cities primarily
        try {
            if (homeInput && !homeInput.__gm_autocomplete) {
                const homeAutocomplete = new google.maps.places.Autocomplete(homeInput, options);
                homeInput.__gm_autocomplete = homeAutocomplete;
                // Submit form on place selection
                homeAutocomplete.addListener('place_changed', function () {
                    const place = homeAutocomplete.getPlace();
                    if (place) {
                        // Use formatted_address (e.g., "Paris, France") instead of name (e.g., "Paris")
                        homeInput.value = place.formatted_address || place.name || homeInput.value;
                        // Submit parent form
                        const form = homeInput.closest('form');
                        if (form) form.submit();
                    }
                });
            }
            if (plannerInput && !plannerInput.__gm_autocomplete) {
                const plannerAutocomplete = new google.maps.places.Autocomplete(plannerInput, options);
                plannerInput.__gm_autocomplete = plannerAutocomplete;
                // Update input value on selection - keep full formatted address
                plannerAutocomplete.addListener('place_changed', function () {
                    const place = plannerAutocomplete.getPlace();
                    if (place) {
                        // Use formatted_address if available, otherwise use name
                        plannerInput.value = place.formatted_address || place.name || plannerInput.value;
                    }
                });
            }
            if (departureInput && !departureInput.__gm_autocomplete) {
                const departureAutocomplete = new google.maps.places.Autocomplete(departureInput, options);
                departureInput.__gm_autocomplete = departureAutocomplete;
                // Update input value on selection - keep full formatted address
                departureAutocomplete.addListener('place_changed', function () {
                    const place = departureAutocomplete.getPlace();
                    if (place) {
                        // Use formatted_address if available, otherwise use name
                        departureInput.value = place.formatted_address || place.name || departureInput.value;
                    }
                });
            }
        } catch (e) { console.warn('Places Autocomplete init failed', e); }
    }
    // If API already loaded
    if (window.google && window.google.maps) { init(); }
    // Listen for custom event dispatched in _Layout after maps load
    document.addEventListener('google-maps-loaded', init);
})();
