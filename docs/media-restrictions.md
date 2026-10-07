# Media Restrictions

Status: **complete.** Known gaps are listed in section 5.
Last updated: 2026-10-06

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

Roles are identified by **name**. The list is the whole restriction, not a delta,
so a repeated call is harmless. An empty list is refused rather than read as
"visible to nobody"; use `DELETE` to remove a restriction.

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
