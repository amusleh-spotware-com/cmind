// App-shell service worker. Blazor Server needs a live SignalR circuit, so this cannot make the app
// work fully offline — it makes the app installable and shows a friendly offline page when the network
// is gone. It deliberately never touches the Blazor circuit/framework.
//
// Static assets are served NETWORK-FIRST: CSS/JS/icon updates are always fresh when online, and the cache
// is only a fallback for offline. This avoids serving a stale stylesheet after a deploy. Bump CACHE when
// changing the precached shell so old caches are cleared on activate.
const CACHE = 'cmind-shell-v2';
const SHELL = [
    '/offline.html',
    '/css/site.css',
    '/manifest.webmanifest',
    '/icons/icon-192.png',
    '/icons/icon-512.png',
];

self.addEventListener('install', (event) => {
    event.waitUntil(
        caches.open(CACHE).then((cache) => cache.addAll(SHELL)).then(() => self.skipWaiting())
    );
});

self.addEventListener('activate', (event) => {
    event.waitUntil(
        caches.keys()
            .then((keys) => Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k))))
            .then(() => self.clients.claim())
    );
});

self.addEventListener('fetch', (event) => {
    const req = event.request;
    if (req.method !== 'GET') return;

    const url = new URL(req.url);
    if (url.origin !== self.location.origin) return;

    // Never intercept the Blazor circuit, framework negotiation, or SignalR hubs.
    if (url.pathname.startsWith('/_blazor') ||
        url.pathname.startsWith('/_framework') ||
        url.pathname.startsWith('/hubs')) {
        return;
    }

    // Navigations: network-first, fall back to the offline page.
    if (req.mode === 'navigate') {
        event.respondWith(fetch(req).catch(() => caches.match('/offline.html')));
        return;
    }

    // Static assets: network-first so updates are never served stale; cache is the offline fallback.
    if (url.pathname.startsWith('/css') ||
        url.pathname.startsWith('/icons') ||
        url.pathname.startsWith('/_content')) {
        event.respondWith(
            fetch(req)
                .then((res) => {
                    const copy = res.clone();
                    caches.open(CACHE).then((cache) => cache.put(req, copy));
                    return res;
                })
                .catch(() => caches.match(req))
        );
    }
});
