# Browse by Location

Status: **phases 0-2 complete - next is phase 3 (views)**
Last updated: 2026-08-30

Lets a user pick a country, state, or city and see the media and categories from
it - the location equivalent of the person/clan browse added in `c9bf927`.

---

## 1. Why this is not just "get_person_media with a different id"

The person feature had an entity to hang everything on: `media.person` has a
UUID, a slug, and a `media.face` join table. Locations have none of that.

`media.location` is **one row per unique (latitude, longitude)** carrying
free-text reverse-geocode columns. There is no country entity, no state entity,
no city entity - just `country`, `administrative_area_level_1` and `locality`
repeated across every coordinate row. So the first question is not about routes
or paging, it is: *what does a user click on, and what is its id?*

Three further wrinkles fall out of the existing schema:

1. **Two location columns.** `media.media` has both `location_id` and
   `location_override_id` (`media.fix_inaccurate_location` writes the latter).
   Every location read path must resolve `COALESCE(location_override_id,
   location_id)`, and nothing in the database defines that today -
   `media.media_gps` returns both side by side and leaves the choice to callers.
2. **NULLs at every level.** The test seed's `LOCATION_UNK` has every metadata
   column null, and real data has plenty of coordinates with a country but no
   `locality` (rural US, and most non-US addresses, where the city lands in
   `administrative_area_level_2` or `sub_locality_level_1` instead).
3. **Dirty text.** Google returns long and short names; the schema stores one,
   and there is no home for normalization rules. The test seed is inconsistent -
   `LOCATION_NY` has `administrative_area_level_1 = 'NY'` while `LOCATION_MA`
   has `'MA'` with `'Massachusetts'` in the `locality` slot and `'Boston'` in
   `neighborhood`. *The phase 0 audit (section 2) found real data is much
   cleaner than this suggests* - consistently long-form and collision-free - so
   this is future-proofing rather than a present defect.

### Options considered

| | approach | verdict |
|---|---|---|
| A | group by text at query time, slugs in the URL, no schema change | cheapest, but no stable ids, slug collisions, `GROUP BY` over text per request, dirty data leaks straight to the UI, and route shapes stop mirroring persons |
| B | a `media.place` dimension table derived from location metadata | **chosen** |
| C | deterministic ids hashed from the normalized triple, exposed via a materialized view | ids change whenever normalization rules change, so bookmarks and client caches lie |

**Chosen: B.** It gives the person feature's ergonomics (`/places/{id}/media`,
`/places/{id}/categories`), an indexed join instead of text grouping, one home
for normalization rules, and - the reason called out explicitly during design -
the ability to *administer places separately from locations*.

---

## 2. Data audit findings (phase 0, 2026-08-30)

Run against the dev restore of prod (`tools/maw_media.dev.dump`, 2026-08-18).
**Two of these changed the design; they are marked.**

### Scale

| | count |
|---|---|
| `media.location` rows | 27,870 |
| never geocoded (`lookup_date IS NULL`) | 2,171 (7.8%) |
| `media.media` rows | 167,202 |
| media with an effective location | 92,790 (55%) |
| media whose effective location is never-geocoded | 6,346 |
| **browsable media** | **~86,400** |

Derived place nodes, if the rules in section 3 were applied today: **8 countries,
31 states, 239 cities** - about 280 nodes total. The tree is small enough that
the recursive descendant walk is free and `get_places` can return whole sets the
way `get_persons` does. Confirms the plan.

### The text is far cleaner than feared

Country names are consistently long-form (`United States`, not `USA`), state
names likewise (`Massachusetts`, not `MA`). Grouping by
`lower(btrim(...))` produces **zero** collisions across raw variants for either
column - there is no case or whitespace noise. There are also **no empty
strings** in `country`, `administrative_area_level_1` or `locality`; NULL is the
only "missing" marker, so the null rules in section 3 need no `= ''` guard.

The `LOCATION_MA` column-shift in the test seed is a defect in the *seed*, not a
sample of real data.

Genuine dirt is limited to a handful of rows, and is exactly what
`media.place_alias` exists to fix later by hand:

- `China / Guangdong` and `China / Guangdong Sheng` - the same real place
- `Macao / Guangdong` - a mis-geocode
- `Macao` and `Hong Kong` with a NULL state (48 rows) - correct, they have no
  state level, so the city hangs off the country per the rules in section 3

**No urgent normalization work.** The alias indirection is still worth building
now, because it costs almost nothing at creation time and is what makes the
deferred admin merge/rename possible without a migration - but it is
future-proofing, not a fix for a present problem.

### CHANGED - drop the `unaccent` extension

Exactly **one** non-ASCII value exists in the whole table: the Thai state
`กรุงเทพมหานคร` (Bangkok). There is not a single accented Latin character
anywhere, and `unaccent` does not transliterate Thai, so the extension would buy
nothing on current data and nothing on that one row either.

`media.slugify` therefore does `[^a-z0-9]+ -> -` with the id-prefix fallback for
names that slugify to empty, and `media.normalize_place_name` is just
`lower(btrim(regexp_replace(v, '\s+', ' ', 'g')))`. One fewer extension
dependency. (Resolves open question 2.)

### CHANGED - the override column is the majority case, not an edge case

| | media |
|---|---|
| `location_override_id` only | 60,472 |
| `location_id` only | 30,847 |
| both set (and always differing) | 1,471 |

`COALESCE(location_override_id, location_id)` is not defensive - **two thirds of
located media are reachable only through the override column**, and every one of
the 1,471 rows carrying both has a genuine conflict for the precedence rule to
resolve.

This raises `media.media_location` from a tidiness view to the single most
load-bearing object in the feature, and it means the index on
`media.media(location_override_id)` matters more than the one on `location_id`.
It also makes the override test case in section 10 mandatory rather than
nice-to-have.

### Media per country, as the browse would show it

`United States` 83,927 · `United Kingdom` 1,988 · `Thailand` 208 ·
`Canada` 117 · `Hong Kong` 99 · `The Bahamas` 67 · `Macao` 28 · `China` 10

Heavily skewed. Worth remembering when judging whether country is a useful top
level for this library, or whether the state list is the more natural entry
point.

---

## 3. Design

`media.place` is a self-referencing tree (country -> state -> city) **derived
from** but **not owned by** `media.location`.

A second table, `media.place_alias`, maps normalized geocode text to a place.
This is what makes the derivation idempotent *and* admin edits durable: an admin
can rename "USA" to "United States" or merge two places, and the next
`media.set_location_metadata` call matches through the alias rather than
resurrecting the old node.

`media.location.place_id` points at the **deepest resolved node**; ancestors come
from the parent chain.

### Hierarchy and NULL rules

| level | source column(s) | when null |
|---|---|---|
| country | `country` | `place_id` stays NULL - the location is not browsable (the `LOCATION_UNK` case) |
| state | `administrative_area_level_1` | level is skipped; the city hangs directly off the country |
| city | `COALESCE(locality, sub_locality_level_1, administrative_area_level_2)` | the deepest node is the state (or the country) |

### Access rule

Identical to people: a place appears only if the caller can see at least one
media there (inner join), and `media_count` is per caller. A restricted user
cannot infer how much exists behind a permission they lack.

---

## 4. Schema

### `tables/media.place_kind.sql` (new)

A lookup table, not a `CHECK`. Two reasons:

- Every closed set in this schema is a table (`media.type`, `media.scale`,
  `media.person_status`, `media.role`). The only two `CHECK` constraints in
  `tables/` are a length check and a cross-column invariant - there is no
  CHECK-as-enum anywhere, and `media.person_status.sql` argues the point in its
  own header.
- More importantly, kind carries **payload**: a `level`. Without it, the
  country/state/city ordering gets hardcoded as a `CASE` in
  `assign_location_place`, in the parent/child validity rule, in breadcrumb
  assembly, and later in `set_place_parent` - four copies that drift. The other
  lookup tables all carry payload too (`media.scale` has width/height,
  `media.person_status` has label/sort_order); none is a bare code list.

```sql
media.place_kind (
    code  TEXT PRIMARY KEY,   -- country | state | city
    label TEXT NOT NULL,      -- "Country" / "State or Region" / "City"
    level SMALLINT NOT NULL   -- 10, 20, 30 - unique
)
```

Levels are **spaced by 10** rather than numbered 1-2-3, so a level can be slotted
between two existing ones - neighborhood below city, or region between country
and state - without renumbering rows `media.place` already references. Given
open question 5, that is a live possibility rather than a hypothetical.

`code TEXT` as the primary key, **not** a UUID surrogate. `media.type` and
`media.scale` use `id UUID` + `code`, and it costs them - `media.get_categories`
joins `media.type` purely to reach `xt.code`. `media.person_status` is the newer
table and references by code directly. That means `media.place.kind` is
human-readable and **no read path needs the join**; only functions that need
`level` join.

Seeded, not synced - nothing publishes these, we own them. Follows
`seed/media.type.sql`.

### `tables/media.place.sql` (new)

```sql
media.place (
    id        UUID PRIMARY KEY,
    parent_id UUID NULL REFERENCES media.place(id),   -- null for countries
    kind      TEXT NOT NULL REFERENCES media.place_kind(code),
    name      TEXT NOT NULL,      -- display; admin editable
    slug      TEXT NOT NULL,
    is_hidden BOOLEAN NOT NULL DEFAULT FALSE,
    created   TIMESTAMPTZ NOT NULL,
    modified  TIMESTAMPTZ NOT NULL
)
```

Indexes (using the `pg_catalog.pg_indexes` guard idiom from `media.media.sql`):

- `ix_media_place$parent_id$kind`
- unique on `(parent_id, slug)` `NULLS NOT DISTINCT` - slug uniqueness is *per
  parent*, so `/united-states/new-york` and `/australia/new-york` can coexist

The `parent.level < child.level` invariant spans rows, so it cannot be a plain
`CHECK`. It is enforced in `media.assign_location_place` and the future
`media.set_place_parent` rather than by introducing this schema's first trigger.

### `tables/media.place_alias.sql` (new)

```sql
media.place_alias (
    place_id        UUID NOT NULL REFERENCES media.place(id),
    kind            TEXT NOT NULL REFERENCES media.place_kind(code),
    parent_place_id UUID NULL REFERENCES media.place(id),
    match_key       TEXT NOT NULL   -- normalized raw geocode text
)
```

Unique index on `(kind, parent_place_id, match_key)` **`NULLS NOT DISTINCT`** -
a plain unique constraint will not do, because `parent_place_id` is NULL for
every country and NULLs do not compare equal by default, so two rows for
`united states` at the country level would both be accepted and derivation would
pick between them arbitrarily. `NULLS NOT DISTINCT` (PG15+; the deploy image is
PG18) expresses this directly and reads better than the `COALESCE(parent, nil)`
trick originally planned. The same applies to `media.place`'s per-parent slug
index.

Every place gets an identity alias at creation. Merges and renames add rows here
rather than mutating the derivation, which is what stops a merged-away name from
coming back on the next geocode.

### `tables/media.location.sql` (modified)

Add `place_id UUID` via the dated `ADD COLUMN IF NOT EXISTS` idiom already used
in `media.media.sql`, with an FK to `media.place` and an index.

---

## 5. Derivation

| function | purpose |
|---|---|
| `media.normalize_place_name` | `lower(btrim(regexp_replace(v, '\s+', ' ', 'g')))` |
| `media.build_place_slug` | periods and apostrophes dropped, then `[^a-z0-9]+` -> `-`, trimmed; falls back to the id's first 8 chars when the name slugifies to empty. Named for the house `verb_noun` convention rather than `slugify`, and place-scoped because of that fallback |
| `media.resolve_place` | finds or creates the place one level resolves to - the single point at which a `media.place` row comes into existence |
| `media.assign_location_place` | resolves one location to its deepest place |
| `media.assign_all_location_places` | loops every location; idempotent, so it is both the backfill and the repair tool |

No `unaccent` extension - the phase 0 audit found exactly one non-ASCII value
in the entire table (a Thai state name, which `unaccent` would not help anyway)
and zero accented Latin characters, so it would earn nothing.

Slugs are cosmetic. Routes key on `{id:guid}`, exactly like persons.

`assign_location_place` walks country -> state -> city, delegating each level to
`resolve_place`: normalize -> look up `place_alias` by `(kind, parent,
match_key)` -> a hit returns the place, a miss creates the place plus its
identity alias. Then set `location.place_id` to the deepest node.

Three details that turned out to be load-bearing during implementation:

- **`IS NOT DISTINCT FROM` on the parent lookup.** Every country has a null
  parent, and `parent_place_id = NULL` matches nothing - plain equality would
  miss the existing row on every call and try to insert a duplicate every time.
- **A retry loop around the insert.** Two sessions racing to create the same
  place (the backfill running while the correction worker publishes) means the
  loser catches the unique violation on the alias and goes round again, where
  the winner's row is now visible.
- **The `UPDATE` is guarded with `place_id IS DISTINCT FROM`.** Without it the
  backfill rewrites all 25,699 rows on every deploy and leaves a dead tuple
  behind each, for no change in the answer.

Slug shaping has two rules beyond lowercasing. **Periods and apostrophes are
dropped rather than separated**, via a single `TRANSLATE` with an empty target,
so the word closes up the way a reader expects:

| name | slug |
|---|---|
| `U.S.A.` | `usa` |
| `Washington D.C.` | `washington-dc` |
| `St. John's` | `st-johns` |
| `Martha's Vineyard` | `marthas-vineyard` |
| `O'Fallon` | `ofallon` |

Any **other** run of non-alphanumerics **collapses to a single dash** with the
ends trimmed, so `  --Hong  Kong--  ` becomes `hong-kong` and `Wilkes-Barre`
stays `wilkes-barre`. The `+` on the character class does that in one pass -
there is no second collapse step. The drop has to happen first, or the pass would
have already turned both characters into dashes.

Two consequences worth knowing:

- **Slugs are assigned at creation and never recomputed.** Changing these rules
  does not retroactively re-slug existing places. It cost nothing for either rule
  (zero of the 271 derived places contain a period or an apostrophe), but a
  future change would need a re-slug pass or the deferred `media.rename_place`.
- **Only the ascii apostrophe is covered.** The geocoder has never returned the
  typographic one - the audit found a single non-ascii value in 27,870 rows, a
  Thai state name - and one would slug as a dash like any other separator.

`resolve_place` also owns slug collisions, since only it knows what is taken:
two distinct names under one parent can slugify alike (`St. John` / `St John`
normalize differently, so they are two places, but reduce to one slug), and the
loser gets the head of its uuid appended.

Ids come from PG18's native **`uuidv7()`**, matching the `Guid.CreateVersion7()`
the application uses everywhere else.

### Call sites

No triggers exist anywhere in this schema, so the calls stay explicit. Four
functions touch `media.location`, but only two ever write geocode text:

- `media.set_location_metadata` - called after the `UPDATE`, before `RETURN 0`
- `media.fix_inaccurate_location` - called on the new location in the `RETURN 13`
  branch, which copies metadata from a nearby location. The other branches need
  no call: `12` reuses an existing location that already has a place, and `14`
  creates a coordinate with no metadata, whose place is correctly the null it
  already holds.

`media.set_media_gps_override` and `media.bulk_set_media_gps_override` create
coordinate-only rows with no text; the correction worker fills them in later via
`set_location_metadata`, which is already hooked. No change needed there.

---

### Phase 2 results (verified against the dev restore)

The backfill derived places for **25,699** locations in **2.35s** - exactly the
27,870 minus the 2,171 never geocoded.

| | derived | expected |
|---|---|---|
| countries | 8 | 8 |
| states | 27 | 27 |
| cities | 236 | 236 |

The 27/236 differ from the 31/239 predicted in section 2 because that audit query
counted NULL levels as distinct tuples, whereas derivation collapses them; the
arithmetic reconciles exactly once the null-level tuples are subtracted. All 271
places carry exactly one identity alias, and every structural invariant holds
(no country with a parent, no non-country without one, no parent at or below its
child's level, no ungeocoded row with a place, no geocoded row without one).

Behaviour verified in rolled-back transactions:

- `set_location_metadata` derives a place end to end
- a re-geocode moves the location to the new place, reusing ancestors and leaving
  the vacated place in place rather than deleting it
- a slug collision resolves via the uuid suffix
- a location that loses its country has `place_id` cleared
- **an admin rename survives re-derivation** - renaming `Japan` to `Nippon` and
  re-running produced no second `Japan`, which is the entire justification for
  `place_alias`
- a second backfill writes **zero** rows and creates no places or aliases

A from-scratch `deploy.sh` run succeeds, and the full existing suite passes
(250 tests, 0 failures).

**Note on running the tests:** `dotnet test --project ...` reports "Zero tests
ran"; the xUnit v3 in-process runner is invoked with `dotnet run` from
`tests/MawMedia.Services.Tests` instead.

---

## 6. Views - where the access rule lives

### `views/media.media_location.sql` (new)

The single definition of effective location, and per the phase 0 audit the most
load-bearing object in the feature: **two thirds of located media are reachable
only through `location_override_id`**, and every media carrying both columns has
a genuine conflict for this `COALESCE` to resolve.

```sql
SELECT id AS media_id, COALESCE(location_override_id, location_id) AS location_id
FROM media.media
WHERE COALESCE(location_override_id, location_id) IS NOT NULL;
```

### `views/media.user_location.sql` (new)

The `media.user_face` analogue - access inherited from `media.user_media`:

```sql
SELECT DISTINCT um.user_id, l.place_id, ml.media_id
FROM media.user_media um
INNER JOIN media.media_location ml ON ml.media_id = um.media_id
INNER JOIN media.location l ON l.id = ml.location_id
WHERE l.place_id IS NOT NULL;
```

`category_id` is **deliberately absent**, and needs the same pointed comment
`media.user_face.sql` carries: `user_media` has a row per (category, media), so
carrying category through here would double-count every media sitting in two
visible categories. Callers needing the category join `media.user_media`
themselves.

---

## 7. Read functions

| function | mirrors | notes |
|---|---|---|
| `media.get_place_descendants` | `media.get_visible_person_count` (shared-definition helper) | `WITH RECURSIVE`; depth is 3 and the tree is small, so no closure table |
| `media.get_places` | `media.get_persons` | returns the whole set, not a page; per-caller `media_count` via `COUNT(DISTINCT ul.media_id)` over descendants; inner join makes invisible places vanish |
| `media.get_place_media` | `media.get_person_media` | person predicate swapped for `place_id IN (get_place_descendants(...))` |
| `media.get_place_categories` | `media.get_person_categories` | same `media_count` semantics, same favorites-both-ways rule |

`get_places` signature: `(_user_id, _parent_id, _kind, _place_id)` - `_place_id`
is the same single-row narrowing trick `get_persons` uses, so one function
serves both list and fetch.

**Paging.** Browsing is always parent-scoped drill-down, so a result set is one
country's states or one state's cities - never "all cities globally". That is
why `get_places` can return whole sets the way `get_persons` does.

Everything else transfers verbatim from the person functions: `DISTINCT ON
(media_id)` collapse, the seeded `hashtextextended` shuffle, page-over-media-
then-fan-out-files, the `_favorites_only` `EXISTS`, and the
`BOOL_OR(_favorites_only AND EXISTS ...)` short circuit.

### Indexes required

None of these exist today, and every query above starts with them:

- `media.media(location_override_id)` - partial, `WHERE ... IS NOT NULL`; per
  the audit this is the *more* important of the two
- `media.media(location_id)`
- `media.location(place_id)`

---

## 8. C# layer

- `src/MawMedia.Models/Place.cs` - `Id, ParentId, Kind, Name, Slug, MediaCount`
  plus `CountryName` / `StateName` for breadcrumbs
- `src/MawMedia.Services.Abstractions/IPlaceRepository.cs` and
  `src/MawMedia.Services/PlaceRepository.cs` - a **new** repository rather than
  extending `ILocationRepository`. That one is admin/geocode-flavored and its
  constructor takes only `log` + `conn`; the browse side needs
  `IAssetPathBuilder` and `HybridCache` to reuse `AssembleMedia` /
  `AssembleCategories`. Different consumer, different scope, different
  dependencies.
- `src/MawMedia.Services/Models/PlaceRow.cs` - the Dapper projection
- register in `IServiceCollectionExtensions` as
  `.AddScoped<IPlaceRepository, PlaceRepository>()`
- `src/MawMedia/Routes/PlaceRoutes.cs`, registered in `Program.cs` as
  `api.MapGroup("/places").MapPlaceRoutes();`

| route | scope |
|---|---|
| `GET /places?parent={id}&kind=` | `MediaReader` |
| `GET /places/{id:guid}` | `MediaReader` |
| `GET /places/{id:guid}/media?o=&f=&seed=` | `MediaReader` |
| `GET /places/{id:guid}/categories?o=&f=` | `MediaReader` |

**`MediaReader`, not `LocationReader`.** `location:read` currently means "read
locations lacking metadata" for the correction worker, and
`get_locations_without_metadata` hard-refuses non-admins. Browsing is plain
media browsing and must not be handed the correction worker's permissions.

Reuse the person routes' 404-vs-empty rule verbatim: 404 on an empty first page
with no filter; the favorites filter is exempt; `seed` is not exempt.

`Category.cs` - the `MediaCount` comment says "only the person/clan category
views populate it"; update it to include places.

---

## 9. deploy.sh ordering

- tables: `media.place_kind.sql`, then `media.place.sql`, then
  `media.place_alias.sql`, all **before** `media.location.sql` (the FK needs its
  target)
- seed: `media.place_kind.sql` alongside `media.type.sql` / `media.scale.sql`
- views: `media.media_location.sql`, then `media.user_location.sql` after
  `media.user_media.sql`
- funcs: alphabetical, as the file already is
- a new `post-deploy` stage, queued last: `post-deploy/media.assign_all_location_places.sql`.
  Idempotent, so it runs on every deploy rather than once - which is also what
  makes a change to the derivation rules take effect without a separate migration
  step to remember.

---

## 10. Tests

The seeder needs work before any of this is testable: **every** seeded media
currently points at `LOCATION_NY` (`tests/MawMedia.Services.Tests/Constants.cs`).

- move some media to `LOCATION_MA`, and fix that record - its columns are
  currently shifted
- one media with `location_override_id` contradicting `location_id`, proving
  `COALESCE` precedence - **mandatory**, not nice-to-have: 1,471 production rows
  carry both columns with differing values
- `LOCATION_UNK` gives the null-metadata / not-browsable case for free
- a place reachable only through a category `ROLE_FRIEND` cannot see, mirroring
  `PERSON_PRIVATE`

Then `tests/MawMedia.Services.Tests/Api/PlaceRoutesTests.cs` modeled on
`PersonRoutesTests`: scope enforcement, visibility filtering, per-caller
`media_count`, drill-down, paging/has-more, the 404 rule, favorites-both-ways on
categories. Plus repository-level tests for the derivation itself (idempotency,
alias reuse, null handling).

---

## 11. Admin surface (deferred)

The schema above already supports it, so this needs no migration later.

`media.rename_place`, `media.merge_places`, `media.set_place_parent`,
`media.set_place_hidden` - all `media.get_is_admin`-gated like
`set_location_metadata`, exposed under `LocationWriter` (that scope genuinely
fits location administration). Merge repoints aliases and locations, then
deletes the loser; because derivation goes through `place_alias`, the merged-away
name will not come back on the next geocode.

---

## 12. Phases

| phase | contents | status |
|---|---|---|
| 0 | data audit - tunes the normalization rules | **complete** (section 2) |
| 1 | schema: `place_kind`, `place`, `place_alias`, `location.place_id`, seed, deploy.sh | **complete** |
| 2 | derivation: normalize, slugify, assign, backfill, call-site wiring | **complete** |
| 3 | views: `media_location`, `user_location`, indexes | next |
| 4 | read functions: descendants, `get_places`, `get_place_media`, `get_place_categories` | |
| 5 | C#: model, repository, routes, DI | |
| 6 | tests + seeder work | |
| 7 | admin surface (deferred) | |

---

## 13. Open questions

1. **Place tiles - image or not?** Persons have `preferred_face_id` for the
   tile; places have nothing. v1 could ship text+count tiles, or add a `LATERAL`
   teaser join picking the most recent visible media at that place. The teaser
   looks better but costs a `media_detail` lookup per tile and drags the file
   fan-out into `get_places`.
2. ~~**`unaccent` extension**~~ - **resolved by the phase 0 audit: not adding
   it.** One non-ASCII value exists in the whole table and `unaccent` would not
   help it.
3. **Cloning vs. generalizing** - this makes a third near-copy of the "page over
   media, then fan out files" pattern. Current plan is to clone for v1 (matching
   how clans extended the person functions) and revisit if a fourth subject type
   appears.
4. **`GET /places/kinds`** - exposing the valid kind codes is nearly free and is
   the "api can expose the valid values" benefit `person_status` cites. In v1 or
   not?
5. **Neighborhood as a real level?** `media.location` already stores
   `neighborhood` and `sub_locality_level_1`, which the plan currently collapses
   into the city fallback chain. The `place_kind` lookup makes promoting one to a
   real level a seed edit rather than a constraint migration.
