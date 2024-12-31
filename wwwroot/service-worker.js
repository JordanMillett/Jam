self.addEventListener('install', event => {
    console.log('Service Worker Installed');
    self.skipWaiting();
});

self.addEventListener('activate', event => {
    console.log('Service Worker Activated');
    event.waitUntil(self.clients.claim());
});

const CACHEABLE_ENDPOINTS =
[
    '/api/get/audio/',
    '/api/get/image/'
];

self.addEventListener('fetch', event => {
    
    const url = new URL(event.request.url);
    const isCacheable = CACHEABLE_ENDPOINTS.some(endpoint => url.pathname.includes(endpoint));
    
    if (isCacheable)
    {
        event.respondWith(
            (async () =>
            {
                const cache = await caches.open('dynamic-cache');

                const requestKey = new Request(event.request.url, {
                    method: event.request.method,
                    headers: new Headers([...event.request.headers].filter(([key]) => key !== 'authorization')),
                });

                const cachedResponse = await cache.match(requestKey);

                if (cachedResponse)
                {
                    //console.log('Serving cached: ', event.request.url);
                    return cachedResponse;
                }

                try
                {
                    const response = await fetch(event.request);
                    const responseToCache = response.clone();
                    cache.put(requestKey, responseToCache);

                    //console.log('Fetched new: ', event.request.url);
                    return response;
                }
                catch (error)
                {
                    console.error('Fetch failed: ', error);
                    return new Response('Fetching Error', {
                        status: 500,
                        statusText: 'Internal Server Error'
                    });
                }
            })()
        );
    }
    else
    {
        event.respondWith(fetch(event.request));
    }
});