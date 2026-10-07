# Media Restrictions

Status: **complete.** Known gaps are listed in section 5.
Last updated: 2026-10-07 (category roles added)

Lets an admin hide individual photos in a category from some of the roles that
can see the category - "these few are for admin, not friend" - without moving them
into a category of their own.

---

## 1. Semantics

A restriction is a list of roles that may see a media, stored in
`media.media_role`.

- **No rows: the media follows its category.** That is every media until someone
  restricts one.
- **With rows: visible only through those roles**, and only where the role is
  *also* granted the media's category. A restriction narrows a category; it can
  never widen it.
- **Per role, not per user.** A user holding both `admin` and `friend` sees a photo
  restricted to `admin`. A per-user deny list would have hidden it from them for
  also being a friend.
- **An allow list, so it fails closed.** A role created later sees no restricted
  photo until it is added.

The rule is applied in exactly one place, `media.user_media`, and every read path
reaches media through it: media, category pages, random media, places, faces and
persons, and the `/assets` file check.

## 2. API

All routes require the `media:write` scope, and only an admin may use them. A
signed-in non-admin gets `403`.

| | |
|---|---|
| `GET /api/v1/roles` | every role name, for a picker: `["admin", "demo", "friend"]` |
| `GET /api/v1/media/{id}/roles` | the media's restriction; `[]` when it has none |
| `PUT /api/v1/media/{id}/roles` | `{"roles": ["admin"]}` - restrict, replacing any existing list. Answers with the stored list |
| `DELETE /api/v1/media/{id}/roles` | remove the restriction |
| `POST /api/v1/media/bulk-roles` | `{"mediaIds": [...], "roles": [...]}` |
| `POST /api/v1/media/bulk-roles/clear` | `{"mediaIds": [...]}` |
| `GET /api/v1/categories/{id}/restrictions` | the restricted media in one category, with their roles and `isVisibleToYou` - see below |
| `GET /api/v1/media/restricted` | the same across the whole library, optionally `?categoryId=` |

Roles are identified by **name**. The list is the whole restriction, not a delta,
so a repeated call is harmless. An empty list is refused rather than read as
"visible to nobody"; use `DELETE` to remove a restriction.

### Badging and filtering restricted photos

Media payloads say nothing about restrictions - not even to admins. Restrictions
are rare and admin-only, so a flag on every media would carry an admin concept to
every client, `null` for nearly all of them, and every query returning media
would have to remember to fill it in.

Instead, restrictions have their own call, the way GPS does:
`GET /api/v1/categories/{id}/restrictions` returns **only the restricted media**
in the category - usually none or a few - and the client joins it to the
category's media on `mediaId`. That gives a bulk-edit "restricted photos" filter
and grid badges for two requests per category, not one per photo. Outside a
category view (random, places, persons), fetch `GET /api/v1/media/restricted`
once - it is small - and match locally.

```json
[
  {
    "mediaId": "0199…", "mediaSlug": "img-0042", "mediaType": "photo",
    "categoryId": "0199…", "categoryName": "November", "categoryYear": 2024,
    "categorySlug": "november", "roles": ["friend"], "isVisibleToYou": false
  }
]
```

### Photos an admin has hidden from themselves

Restrict a photo to roles you do not hold - `["friend"]`, as an admin who is not a
friend - and it drops out of every listing you can make, because visibility
applies to admins too. Both restriction calls still list it: they read
restrictions directly rather than through your own visibility, and
`isVisibleToYou: false` marks it.

They return metadata only, no files: `/assets` refuses you the files of a photo
you cannot see, so paths would only be refused. To view one, lift or widen its
restriction - `DELETE /api/v1/media/{id}/roles` works on it whether or not you
can see it.

### Refusals

Setting a restriction is **all or nothing**. If any media in the request cannot be
restricted, nothing changes, and the response is a `400` listing every problem:

```json
[
  { "mediaId": "0199…", "reason": "teaser",           "detail": "November" },
  { "mediaId": "0199…", "reason": "role_not_granted", "detail": "demo" },
  { "mediaId": null,    "reason": "unknown_role",     "detail": "nope" }
]
```

| reason | meaning |
|---|---|
| `unknown_role` | `detail` names a role that does not exist |
| `not_found` | no such media (a single-media `PUT` answers `404` instead) |
| `role_not_granted` | `detail` names a role the media's category does not grant - it could never see the photo |
| `teaser` | the media is the teaser of the category named in `detail`; change the teaser first |
| `place_cover` | the media is the cover of the place named in `detail`; change or clear the cover first |
| `no_roles` / `no_media` | the request named none |

## 2a. Category roles

Who may see a category at all - the level a photo restriction narrows.

| | |
|---|---|
| `GET /api/v1/categories/{id}/roles` | the roles the category is granted to |
| `PUT /api/v1/categories/{id}/roles` | `{"roles": ["admin", "friend"]}` - grant exactly these, replacing the rest. Answers with the stored list |

Admin only, under `media:write`, like the rest of role management. Read and written
directly rather than through your own visibility, so an admin can always see and
fix a category's roles. Roles that stay keep their original grant date.

Changing roles bumps the category's `modified` time, so clients syncing through
`/categories/updates/{date}` pick it up - without that, a user newly granted an
older category would never receive it. A user who *loses* a category simply stops
receiving it in updates; a client that already cached it keeps showing it until
it resyncs from scratch. Its media and files are refused from then on, within the
30 seconds of the server's access cache - except files their browser already holds,
which can outlive the change by up to a week (section 4).

All or nothing, with every problem listed in a `400`, as for photos:

| reason | meaning |
|---|---|
| `unknown_role` | `detail` names a role that does not exist |
| `restriction_depends` | `mediaId` is a restricted photo in this category whose restriction lists `detail`, a role being removed - change that photo's restriction first, or it would name a role that grants nothing |
| `would_hide_from_you` | you hold none of the new roles, so the change would hide the category from you - and unlike a photo, there is no listing to find a hidden category again |
| `no_roles` | an empty list; a category granted to nobody is hidden from everyone |

A missing category answers `404`.

## 3. Routes with a wider audience

A teaser, a place cover and a person's preferred face are each shown to more people
than the media itself, so a restricted media must not reach them:

- **Teaser:** refused both ways. A restricted media cannot be restricted while it is
  a teaser, and `PUT /categories/{id}/teaser` answers `400` for a restricted media.
- **Place cover:** refused both ways. A restricted media is refused as a cover
  *before* its file is published to the public covers directory.
- **Preferred face:** not refused, because it is published from maw-media-ai and
  cannot be changed here. Instead, person and clan listings return a preferred face
  only to callers who can see it, and `null` otherwise. `preferredFaceId` and
  `preferredFaceUrl` were already nullable in the API; clients should show a
  placeholder when they are.

Category tiles' `mediaTypes` are computed from the caller's visible media, so a tile
does not say "video" when its only video is hidden.

## 4. Caching

- **Server:** the `/assets` access cache is keyed per file and lives 30 seconds, so a
  new restriction is enforced within half a minute.
- **Browser:** assets are sent with `Cache-Control: private, max-age=604800`. A user
  who already viewed a photo can keep it in their browser cache for up to a week
  after it is restricted. **Restrict before sharing a category, not after.**

## 5. Known gaps

These reveal *that* hidden media exist - counts and words, never images:

- Stats (`/stats`) count restricted media in their totals.
- Category search (`media.category_search`) indexes comments, person names and
  location text from restricted media, so a category can match a term that only
  appears on a hidden photo.

A category can never be emptied by restrictions for someone who can see it: its
teaser cannot be restricted, so at least that one media always remains.

## 6. Performance

The rule is shaped to keep `media.user_media` a single, parallel-safe `SELECT`. Two
simpler shapes were measured and rejected; the reasons are recorded in
`views/media.user_media.sql`. Measured against the previous view on the dev
restore: place pages and city pages flat, the asset check +0.3 ms, the face check
+0.6 ms, random media +8-10 ms, and no change between 0 and 50 restricted media.
