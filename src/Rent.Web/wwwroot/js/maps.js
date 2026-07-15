(function () {
    'use strict';

    var mapsLoadPromise = null;
    var clustererLoadPromise = null;

    function getApiKey() {
        var meta = document.querySelector('meta[name="rentca-maps-key"]');
        return meta ? meta.getAttribute('content') : null;
    }

    function loadGoogleMaps() {
        if (mapsLoadPromise) return mapsLoadPromise;
        var key = getApiKey();
        if (!key) return Promise.reject(new Error('Maps API key not configured'));

        mapsLoadPromise = new Promise(function (resolve, reject) {
            window.__rentcaMapsCallback = function () {
                try { delete window.__rentcaMapsCallback; } catch (e) { window.__rentcaMapsCallback = undefined; }
                resolve();
            };
            var s = document.createElement('script');
            s.src = 'https://maps.googleapis.com/maps/api/js?key=' + encodeURIComponent(key) +
                    '&loading=async&callback=__rentcaMapsCallback&libraries=marker&v=weekly';
            s.async = true;
            s.defer = true;
            s.onerror = function () { reject(new Error('Google Maps script failed to load')); };
            document.head.appendChild(s);
        });
        return mapsLoadPromise;
    }

    function loadClusterer() {
        if (clustererLoadPromise) return clustererLoadPromise;
        if (window.markerClusterer) return Promise.resolve();

        clustererLoadPromise = new Promise(function (resolve, reject) {
            var s = document.createElement('script');
            s.src = 'https://unpkg.com/@googlemaps/markerclusterer@2.5.3/dist/index.min.js';
            s.async = true;
            s.defer = true;
            s.onload = function () { resolve(); };
            s.onerror = function () { reject(new Error('MarkerClusterer failed to load')); };
            document.head.appendChild(s);
        });
        return clustererLoadPromise;
    }

    function escapeHtml(value) {
        if (value === null || value === undefined) return '';
        return String(value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function safeInternalUrl(value) {
        if (!value) return '';
        var s = String(value);
        if (s.charAt(0) !== '/' || s.indexOf('//') === 0) return '';
        return s;
    }

    /* ---------------------------------------------------------------------- *
     *  Map theme (dark/light) — ported from the Next.js map-styles.ts.
     *  Applied via JS `styles` (which requires NO mapId), so the search map
     *  uses legacy markers instead of AdvancedMarkerElement.
     * ---------------------------------------------------------------------- */
    var DARK_MAP_STYLES = [
        { elementType: 'geometry', stylers: [{ color: '#1e293b' }] },
        { elementType: 'labels.text.stroke', stylers: [{ color: '#0f172a' }] },
        { elementType: 'labels.text.fill', stylers: [{ color: '#64748b' }] },
        { featureType: 'administrative.locality', elementType: 'labels.text.fill', stylers: [{ color: '#94a3b8' }] },
        { featureType: 'poi', elementType: 'labels.text.fill', stylers: [{ color: '#64748b' }] },
        { featureType: 'poi.park', elementType: 'geometry', stylers: [{ color: '#1a3a2a' }] },
        { featureType: 'poi.park', elementType: 'labels.text.fill', stylers: [{ color: '#4ade80' }] },
        { featureType: 'road', elementType: 'geometry', stylers: [{ color: '#334155' }] },
        { featureType: 'road', elementType: 'geometry.stroke', stylers: [{ color: '#1e293b' }] },
        { featureType: 'road.highway', elementType: 'geometry', stylers: [{ color: '#475569' }] },
        { featureType: 'road.highway', elementType: 'geometry.stroke', stylers: [{ color: '#334155' }] },
        { featureType: 'road.highway', elementType: 'labels.text.fill', stylers: [{ color: '#94a3b8' }] },
        { featureType: 'transit', elementType: 'geometry', stylers: [{ color: '#334155' }] },
        { featureType: 'transit.station', elementType: 'labels.text.fill', stylers: [{ color: '#64748b' }] },
        { featureType: 'water', elementType: 'geometry', stylers: [{ color: '#0c1a2e' }] },
        { featureType: 'water', elementType: 'labels.text.fill', stylers: [{ color: '#475569' }] }
    ];
    var LIGHT_MAP_STYLES = [
        { featureType: 'poi', elementType: 'labels', stylers: [{ visibility: 'off' }] },
        { featureType: 'transit', elementType: 'labels.icon', stylers: [{ visibility: 'off' }] }
    ];

    function isDarkMode() {
        return document.documentElement.classList.contains('dark');
    }
    function currentMapStyles() {
        return isDarkMode() ? DARK_MAP_STYLES : LIGHT_MAP_STYLES;
    }
    function observeThemeChanges(cb) {
        if (typeof MutationObserver === 'undefined') return;
        var last = isDarkMode();
        var obs = new MutationObserver(function () {
            var now = isDarkMode();
            if (now !== last) { last = now; cb(); }
        });
        obs.observe(document.documentElement, { attributes: true, attributeFilter: ['class'] });
    }

    function showMessage(container, msg) {
        container.innerHTML = '<div class="flex items-center justify-center h-full text-sm text-slate-500 dark:text-white/60 p-6 text-center">' +
            escapeHtml(msg) + '</div>';
    }

    function initDetailMap() {
        var el = document.getElementById('rentca-detail-map');
        if (!el) return;
        var lat = parseFloat(el.getAttribute('data-lat'));
        var lng = parseFloat(el.getAttribute('data-lng'));
        if (isNaN(lat) || isNaN(lng)) return;

        var title = el.getAttribute('data-title') || '';

        loadGoogleMaps().then(function () {
            var map = new google.maps.Map(el, {
                center: { lat: lat, lng: lng },
                zoom: 15,
                mapId: 'RENTCA_DETAIL_MAP',
                gestureHandling: 'cooperative',
                clickableIcons: false,
                streetViewControl: false,
                mapTypeControl: false,
                fullscreenControl: true
            });
            var pin = new google.maps.marker.PinElement({
                background: '#338dff',
                borderColor: '#142857',
                glyphColor: '#ffffff',
                scale: 1.1
            });
            new google.maps.marker.AdvancedMarkerElement({
                map: map,
                position: { lat: lat, lng: lng },
                title: title,
                content: pin
            });
        }).catch(function () {
            showMessage(el, 'Map could not be loaded.');
        });
    }

    function buildPopupHtml(marker) {
        var url = safeInternalUrl('/' + marker.citySlug + '/' + marker.slug);
        var price = marker.fromPrice != null
            ? '$' + Number(marker.fromPrice).toLocaleString('en-US', { maximumFractionDigits: 0 }) + '<span class="text-xs text-slate-500">/mo</span>'
            : '';
        var beds = marker.minBedrooms === 0 ? 'Studio' : (marker.minBedrooms + '+ bed');
        var img = marker.primaryImageUrl
            ? '<img src="' + escapeHtml(marker.primaryImageUrl) + '" alt="" class="w-full h-28 object-cover rounded-md mb-2" loading="lazy" />'
            : '';
        return '<div class="rentca-popup" style="min-width:220px;max-width:240px;font-family:Inter,system-ui,sans-serif;">' +
            (url
                ? '<a href="' + escapeHtml(url) + '" style="text-decoration:none;color:inherit;display:block;">'
                : '<div>') +
                img +
                '<div style="font-weight:600;color:#0f172a;line-height:1.2;margin-bottom:4px;">' + escapeHtml(marker.title) + '</div>' +
                '<div style="font-size:18px;font-weight:700;color:#142857;line-height:1.1;">' + price + '</div>' +
                '<div style="font-size:12px;color:#64748b;margin-top:4px;">' + escapeHtml(beds) + ' &middot; ' + escapeHtml(marker.propertyType) + '</div>' +
            (url ? '</a>' : '</div>') +
            '</div>';
    }

    function initSearchMap() {
        var container = document.getElementById('rentca-search-map');
        if (!container) return;
        // Only initialize when the map view is the active one (server renders
        // the container without the `hidden` class when View=Map).
        if (container.classList.contains('hidden')) return;

        var citySlug = container.getAttribute('data-city-slug') || '';
        if (!citySlug) return;

        var cityLatRaw = container.getAttribute('data-city-lat');
        var cityLngRaw = container.getAttribute('data-city-lng');
        var cityLat = cityLatRaw ? parseFloat(cityLatRaw) : NaN;
        var cityLng = cityLngRaw ? parseFloat(cityLngRaw) : NaN;

        var mapInstance = null;
        var clusterer = null;
        var infoWindow = null;

        function initMap() {
            loadGoogleMaps().then(function () {
                var center = { lat: !isNaN(cityLat) ? cityLat : 56.1304, lng: !isNaN(cityLng) ? cityLng : -106.3468 };
                mapInstance = new google.maps.Map(container, {
                    center: center,
                    zoom: 11,
                    // No mapId on purpose: Google ignores JS `styles` when a mapId is set,
                    // and we theme the map (dark/light) via `styles`. Uses legacy markers.
                    styles: currentMapStyles(),
                    gestureHandling: 'greedy',
                    clickableIcons: false,
                    streetViewControl: false,
                    mapTypeControl: false,
                    fullscreenControl: true
                });
                infoWindow = new google.maps.InfoWindow({ maxWidth: 260 });

                // Re-theme the map when the site theme toggles.
                observeThemeChanges(function () {
                    if (mapInstance) mapInstance.setOptions({ styles: currentMapStyles() });
                });

                var url = '/api/maps/' + encodeURIComponent(citySlug) + window.location.search;
                fetch(url, { credentials: 'same-origin', headers: { 'Accept': 'application/json' } })
                    .then(function (r) { return r.ok ? r.json() : Promise.reject(new Error('http_' + r.status)); })
                    .then(function (data) {
                        var markers = (data && data.markers) || [];
                        if (markers.length === 0) return;

                        var bounds = new google.maps.LatLngBounds();
                        var gMarkers = markers.map(function (m) {
                            var pos = { lat: m.lat, lng: m.lng };
                            bounds.extend(pos);
                            var marker = new google.maps.Marker({
                                position: pos,
                                title: m.title,
                                icon: {
                                    // Teardrop pin, colored per tier (Featured = cyan, else brand).
                                    path: 'M12 0C7.03 0 3 4.03 3 9c0 6.75 9 15 9 15s9-8.25 9-15c0-4.97-4.03-9-9-9z',
                                    fillColor: m.tier === 'Featured' ? '#06b6d4' : '#338dff',
                                    fillOpacity: 1,
                                    strokeColor: '#ffffff',
                                    strokeWeight: 1.5,
                                    scale: 1.4,
                                    anchor: new google.maps.Point(12, 24)
                                }
                            });
                            marker.addListener('click', function () {
                                infoWindow.setContent(buildPopupHtml(m));
                                infoWindow.open({ anchor: marker, map: mapInstance });
                            });
                            return marker;
                        });

                        if (markers.length === 1) {
                            mapInstance.setCenter(gMarkers[0].getPosition());
                            mapInstance.setZoom(14);
                        } else {
                            mapInstance.fitBounds(bounds, 60);
                        }

                        loadClusterer().then(function () {
                            if (window.markerClusterer && window.markerClusterer.MarkerClusterer) {
                                clusterer = new window.markerClusterer.MarkerClusterer({
                                    map: mapInstance,
                                    markers: gMarkers
                                });
                            } else {
                                gMarkers.forEach(function (m) { m.setMap(mapInstance); });
                            }
                        }).catch(function () {
                            gMarkers.forEach(function (m) { m.setMap(mapInstance); });
                        });
                    })
                    .catch(function () {
                        // Markers fetch failed but the map itself is fine; just leave it empty.
                    });
            }).catch(function () {
                showMessage(container, 'Map could not be loaded.');
            });
        }

        initMap();
    }

    function init() {
        initDetailMap();
        initSearchMap();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
