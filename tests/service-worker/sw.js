self.addEventListener('install', function (event) {
    // Skip the 'waiting' lifecycle phase, to go directly from 'installed' to 'activated', even if
    // there are still previous incarnations of this service worker registration active.
    console.log('install');
    event.waitUntil(self.skipWaiting());
});

self.addEventListener('activate', async function (event) {
    console.log('activate');
    // Claim any clients immediately, so that the page will be under SW control without reloading.
    event.waitUntil(self.clients.claim());
});

self.addEventListener('fetch', function (event) {
    if (event.request.url.startsWith('http://localhost:8081/assets/')) {
        console.log("trying to force access token for asset");

        const accessToken = 'xyz';

        const newHeaders = new Headers(event.request.headers);
        newHeaders.append('Authorization', `Bearer ${accessToken}`);

        const newRequest = new Request(
            event.request, {
                headers: newHeaders,
                mode: 'cors', // Essential for cross-origin requests
                credentials: 'include' // If you need to send cookies or HTTP authentication
            });

        event.respondWith(fetch(newRequest));
    } else {
        event.respondWith(fetch(event.request));
    }
});
