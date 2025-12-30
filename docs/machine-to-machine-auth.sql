--
-- we use machine to machine auth via Auth0 for backend processing jobs to interface with our API.  An example
-- of this is the reverse geocode update tool, which will query for locations that need to be reverse geocoded
-- and then to set metadata for locations after it receives a response from google.
--
-- the onboarding auth logic in MawMedia.Authorization.MediaIdentityClaimsTransformation is not able to query
-- user info for the machine to machine application - because, well, it is an app, not a person.  given this,
-- we manually create the external identity and user to represent this interaction with our API.
--
-- the SQL below is a template of this for the reverse geocoder:
--
-- note: be sure to replace the CLIENT_ID below if you use this template in the future as well as provide new
-- identifiers for user and external identity...
--

INSERT INTO media.user
(
    id,
    created,
    modified,
    name,
    email,
    email_verified,
    given_name,
    surname
)
VALUES
(
    '019b6f14-6d2f-7adc-a19f-394c92441dbc',
    NOW(),
    NOW(),
    'Reverse Geocode',
    'reverse-geocode@mikeandwan.us',
    False,
    'Reverse Geocode',
    'Application'
);

INSERT INTO media.user_role
(
    user_id,
    role_id,
    created,
    created_by
)
VALUES
(
    '019b6f14-6d2f-7adc-a19f-394c92441dbc',
    '0199f6b6-7c04-7fd9-a01d-fe69d07064bb',
    NOW(),
    '0199f6b6-7c04-7e7f-9236-1c609d90086c'
);

-- sub / external id from auth0 = '<client_id>@clients'
INSERT INTO media.external_identity
(
    external_id,
    user_id,
    created,
    modified,
    name,
    email,
    email_verified,
    given_name,
    surname
)
VALUES
(
    'CLIENT_ID_GOES_HERE@clients',
    '019b6f14-6d2f-7adc-a19f-394c92441dbc',
    NOW(),
    NOW(),
    'Reverse Geocode',
    'reverse-geocode@mikeandwan.us',
    False,
    'Reverse Geocode',
    'Application'
);
