async function loadServiceWorker()
{
    if ('serviceWorker' in navigator)
    {
        navigator.serviceWorker.register('service-worker.js');
        console.log('Service Worker Registered');
    }
}