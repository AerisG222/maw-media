import http from 'k6/http';

export const options = {
    vus: 10,
    duration: '20s'
}

// todo: make acquiring this easier, for now, debug a client app and extract
const access_token = "<ACCESS_TOKEN>";

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
