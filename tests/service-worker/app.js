// https://felixgerschau.com/service-workers-explained-introduction-javascript-api/
// https://www.clurgo.com/en/blog/service-worker-and-static-content-authorization
if ('serviceWorker' in navigator) {
    window.addEventListener('load', () => {
        navigator.serviceWorker
            .register('sw.js')
            .then((registration) => {
                console.info(
                    'ServiceWorker registration successful with scope: ',
                    registration.scope,
                );
                return registration;
            })
            .catch((error) => {
                console.info('ServiceWorker registration failed: ', error);
            });
    });
}

setTimeout(
    () => {
        var img = document.createElement("img");
        img.src = "http://localhost:8081/assets/2007/dusty/qqvg/dsc_0216.avif";

        document.getElementById("#body").appendChild(img);
    },
    500
);
