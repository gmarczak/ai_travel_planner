# Google Places API Migration Guide

## ⚠️ Deprecation Notice

As of **March 1, 2025**, `google.maps.places.Autocomplete` is deprecated for new customers.
While it will continue to receive bug fixes for major regressions, **existing bugs will not be addressed**.

Google recommends migrating to **`google.maps.places.PlaceAutocompleteElement`** (Web Component).

## 📚 Resources

- **Migration Guide**: https://developers.google.com/maps/documentation/javascript/places-migration-overview
- **Legacy Notice**: https://developers.google.com/maps/legacy
- **New API Reference**: https://developers.google.com/maps/documentation/javascript/reference/places-widget

## 🔧 Current Usage (Deprecated)

The following files currently use the deprecated API:

1. **`wwwroot/js/destination-autocomplete.js`**
   - Home page destination input
   - Travel planner destination input
   - Departure location input

2. **`wwwroot/js/travelplanner.result.js`**
   - Result page activity autocomplete

3. **`wwwroot/js/results-map.js`**
   - Map search autocomplete

## 🚀 Migration Steps

### 1. Update HTML to use Web Component

**OLD (deprecated):**
```html
<input type="text" id="destinationInput" placeholder="Enter destination..." />
<script>
  const autocomplete = new google.maps.places.Autocomplete(input);
</script>
```

**NEW (recommended):**
```html
<gmp-place-autocomplete 
  id="destinationInput" 
  placeholder="Enter destination..."
  type="cities">
</gmp-place-autocomplete>

<script>
  const autocomplete = document.getElementById('destinationInput');
  autocomplete.addEventListener('gmp-placeselect', (event) => {
    const place = event.place;
    console.log('Selected:', place.displayName);
  });
</script>
```

### 2. Update JavaScript Event Listeners

**OLD:**
```javascript
autocomplete.addListener('place_changed', function() {
  const place = autocomplete.getPlace();
  // ...
});
```

**NEW:**
```javascript
element.addEventListener('gmp-placeselect', async (event) => {
  const place = event.place;
  await place.fetchFields({ fields: ['displayName', 'formattedAddress'] });
  // ...
});
```

### 3. Update Pages Affected

- **`Pages/Index.cshtml`**: Home search input
- **`Pages/TravelPlanner/Index.cshtml`**: Destination & departure inputs
- **`Pages/TravelPlanner/Result.cshtml`**: Activity search inputs

### 4. Test Thoroughly

- [ ] Home page destination search
- [ ] Travel planner destination input
- [ ] Departure location input
- [ ] Result page activity autocomplete
- [ ] Map search functionality

## ⏰ Timeline

- **Now**: Deprecated API still works, but shows console warnings
- **12+ months notice**: Will be given before discontinuation
- **No specific end date yet**: Google will provide advance notice

## 💡 Recommendation

While the old API still works, plan migration within the next 6-12 months to:
1. Avoid future breaking changes
2. Access new features and improvements
3. Ensure long-term support

## 🔗 Additional Notes

- The new API uses **Web Components** (custom HTML elements)
- Requires modern browser support (ES6+)
- Simpler API with better TypeScript support
- Improved accessibility and mobile experience
