import http from 'k6/http';

export const options = {
    vus: 10,
    duration: '20s'
}

// todo: make acquiring this easier, for now, debug a client app and extract
const access_token = "eyJhbGciOiJSUzI1NiIsInR5cCI6ImF0K2p3dCIsImtpZCI6IkNKT19ILU91cEZ2QmoyMlFEWFBGcCJ9.eyJpc3MiOiJodHRwczovL2xvZ2luLm1pa2VhbmR3YW4udXMvIiwic3ViIjoiZ29vZ2xlLW9hdXRoMnwxMTgwMTY0MDM3Nzg0Mzc2NzQ4ODMiLCJhdWQiOlsiaHR0cHM6Ly9kZXYtbWVkaWEubWlrZWFuZHdhbi51cyIsImh0dHBzOi8vbWlrZWFuZHdhbi51cy5hdXRoMC5jb20vdXNlcmluZm8iXSwiaWF0IjoxNzU0NzQxMjkyLCJleHAiOjE3NTQ4Mjc2OTIsInNjb3BlIjoib3BlbmlkIHByb2ZpbGUgZW1haWwgaHR0cHM6Ly9kZXYtbWVkaWEubWlrZWFuZHdhbi51cy9tZWRpYTpyZWFkIGh0dHBzOi8vZGV2LW1lZGlhLm1pa2VhbmR3YW4udXMvbWVkaWE6d3JpdGUgaHR0cHM6Ly9kZXYtbWVkaWEubWlrZWFuZHdhbi51cy9jb21tZW50czpyZWFkIGh0dHBzOi8vZGV2LW1lZGlhLm1pa2VhbmR3YW4udXMvY29tbWVudHM6d3JpdGUiLCJqdGkiOiJraVAyaVhqdzgyWmthYm5mVzU0ek1XIiwiY2xpZW50X2lkIjoiRFB0MGxXMEk1NHh2bTNFd1ZOdk5DaG5oR3o0OGNzcTYifQ.393q3cybQq5Wwwq_VQRlPToQQ8kxOyO8DoX8SirEsnQkGFgkNcjgPYyLsFkRR08BTF9JCMYW6Li5DVYu4eueT6F8XdoP136QjqtGvArXBtFq5AWC4UQwoqoVH3L73TkAoiDxZTxWyEwIfCgS_e-5-RdMI53ewx1PBWR6qn32pYM90EFhkEtlBR224nExT85RIFGEx-Q_tMSdsh6RhVYAc0O61QaFjZqzFAaWrOxtjmfQVxpsJHKuwKrADBMk0ZYV3Qd7JTu7WBKa-YNbcjCRAFACDi5tBm5CWyBj0KebvVtH6rCAcQ5zInOqa7LP_keM_IzBz2h2xiuUBohiO7eG6A";

export default function () {
    const params = {
        headers: {
            "Authorization": `Bearer ${access_token}`
        }
    };

    var mediaRes = http.get(
        'https://dev-media.mikeandwan.us:8081/categories/0197e02e-7c87-7109-a261-6d8c3cefec04/media',
        params
    );

    if(mediaRes.body == null) {
        throw "expected response value with category media";
    }

    var media = JSON.parse(mediaRes.body.toString());

    for(const item of media) {
        for(const file of item.files) {
            if(file.scale === "qvg") {
                http.get(file.path, params);
            }
        }
    }
}
