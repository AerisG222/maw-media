# Browse by Location

Status: **all phases complete.** Remaining optional work: `rename_place`, and the judgement calls in section 11
Last updated: 2026-09-03

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
between two existing ones - a region between country and state, say - without
renumbering rows `media.place` already references.

City is deliberately the floor: a neighborhood level was considered and rejected
as too granular for this browse (open question 5). `media.location`'s
`neighborhood` and `sub_locality_level_1` columns feed the *city* fallback
instead.

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

The `media.user_face` analogue, but composed from `media.user_category` and
`media.category_media` **directly** rather than through `media.user_media`:

```sql
SELECT DISTINCT uc.user_id, l.place_id, cm.media_id
FROM media.user_category uc
INNER JOIN media.category_media cm ON cm.category_id = uc.category_id
INNER JOIN media.media_location ml ON ml.media_id = cm.media_id
INNER JOIN media.location l ON l.id = ml.location_id
WHERE l.place_id IS NOT NULL;
```

This is not a shortcut around the access rule. `media.user_media` *is* those same
two relations with a `DISTINCT` over `(category_id, media_id, media_slug,
user_id)`, and this view uses neither `category_id` nor `media_slug` -
`media.user_category` remains the one home of `category_role -> user_role`.
Going through `user_media` would buy a dedupe of columns that are then projected
away, at the cost of an optimization barrier: that `DISTINCT` materializes all
167,202 rows and spills ~7.5MB to disk before any place predicate can prune it.

Measured on the dev restore, and the two forms verified set-identical across all
2,937,290 rows:

| query | via `user_media` | composed directly |
|---|---|---|
| city page (50) | 297ms | **72ms** |
| country page (50, recursive) | 239ms | **66ms** |
| media_count for all countries | 403ms | **203ms** |

`category_id` is **deliberately absent**, and needs the same pointed comment
`media.user_face.sql` carries: a media sitting in two visible categories would
arrive twice, and carrying that through would double-count every media in an
aggregate. Callers needing the category join `media.user_media` themselves.

The `DISTINCT` is **defensive, not currently load-bearing** - worth stating so
nobody removes it after measuring that it changes nothing. Every media in the
library belongs to exactly one category today (167,202 `category_media` rows over
167,202 distinct media), so there is no duplication to absorb yet. The other
fan-out source, a category reachable via two of a user's roles, is already
absorbed by `media.user_category`'s own `DISTINCT` - which matters, because
`media.category_role` averages two roles per category.

---

## 7. Read functions

| function | mirrors | notes |
|---|---|---|
| `media.get_place_descendants` | `media.get_visible_person_count` (shared-definition helper) | `WITH RECURSIVE`; depth is 3 and the tree is small, so no closure table |
| `media.get_place_ancestors` | (counterpart of the above) | the breadcrumb chain, root first, with a 1-based `depth`; sub-millisecond |
| `media.get_places` | `media.get_persons` | returns the whole set, not a page; per-caller `media_count` via `COUNT(DISTINCT ul.media_id)` over the subtree; inner join makes invisible places vanish |
| `media.get_place_media` | `media.get_person_media` | person predicate swapped for `place_id IN (get_place_descendants(...))` |
| `media.get_place_categories` | `media.get_person_categories` | same `media_count` semantics, same favorites-both-ways rule |

`get_places` signature: `(_user_id, _parent_id, _kind, _place_id)` - `_place_id`
is the same single-row narrowing trick `get_persons` uses, so one function
serves both list and fetch. With no `_parent_id` it lists the countries; with one
it lists that place's children.

Two details worth knowing:

- **A listing can mix kinds.** Macao and Hong Kong have no state level, so their
  cities parent straight to the country. That is why `kind` is returned rather
  than inferred from the depth of the request.
- **`_parent_id` uses `IS NOT DISTINCT FROM`.** A null `_parent_id` means "the
  root", and every country's `parent_id` is null, so plain equality would match
  nothing and the top level of the browse would come back empty.

### Phase 4 results (verified against the dev restore)

| check | result |
|---|---|
| a page is N distinct media, not N (media, file) rows | 500 rows = 50 media x 10 scales |
| pages 1 and 2 do not overlap | 0 |
| same seed returns an identical page | 50/50 |
| different seed reorders | 0 overlap |
| seeded pages do not overlap | 0 |
| a city's media are a subset of its country's | 0 violations |
| a city's per-category count never exceeds its state's | 0 violations |
| country `media_count` equals its subtree's distinct union | 83,927 = 83,927 |
| restricted user sees extra places | 0 |
| restricted user sees more media anywhere | 0 |
| zero-count tiles | 0 |
| unknown place id | empty, not an error |

Access enforcement was checked against a place where the two users genuinely
differ (Worcester: admin 911, restricted 895) rather than one where both hit the
page cap - the tail page returns 111 against 95, and nothing the restricted user
sees is invisible to the admin.

Timings: root listing 220ms, drill-down 217ms, breadcrumb 0.3-0.7ms, media page
442-510ms, categories page 607ms. For reference the existing person equivalents
on the same data are 614ms, 873ms and 4,759ms (`get_categories`), so these sit
below the feature they mirror.

**Paging.** Browsing is always parent-scoped drill-down, so a result set is one
country's states or one state's cities - never "all cities globally". That is
why `get_places` can return whole sets the way `get_persons` does.

Everything else transfers verbatim from the person functions: `DISTINCT ON
(media_id)` collapse, the seeded `hashtextextended` shuffle, page-over-media-
then-fan-out-files, the `_favorites_only` `EXISTS`, and the
`BOOL_OR(_favorites_only AND EXISTS ...)` short circuit.

### Indexes

Only one is needed: **`media.location(place_id)`**, partial on `IS NOT NULL`
(added in phase 1). It is used heavily - 542 scans during phase 3 verification
alone.

Indexes on `media.media(location_id)` and `media.media(location_override_id)`
were planned, built, and then **removed after measurement disproved the
justification for them**. Two reasons:

1. `media.media_location` joins on `COALESCE(location_override_id, location_id)`,
   which is **not sargable** against either single-column index - the planner
   seq-scans `media.media` regardless. An expression index on the coalesce itself
   *can* be built, but the planner still preferred the seq scan under `LIMIT`,
   and it moved the end-to-end timings by less than noise (250/239/443ms against
   218/231/437ms).
2. `pg_stat_user_indexes` showed each used exactly once or twice across the whole
   verification run, and reading nearly the entire index when it did.

The real cost was never the media-to-location join; it was `media.user_media`'s
`DISTINCT`, which composing the view directly removes. Worth re-testing in phase
4 if `get_place_media` ends up with a different query shape, but an index whose
stated reason the measurement contradicts should not ship.

---

## 8. C# layer

- `src/MawMedia.Models/Place.cs` - `Id, ParentId, Kind, Name, Slug, MediaCount`.
  Deliberately flat: the browse is a drill-down, so a client asks for one level at
  a time and never needs the whole tree nested in one response.
- `src/MawMedia.Models/PlaceAncestor.cs` - the breadcrumb rung. Not a `Place`,
  because a breadcrumb has no use for `MediaCount` and computing one per rung
  would mean a subtree aggregate per level for a number nothing renders.
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
| `GET /places?parent={id}&kind=&q=` | `MediaReader` |
| `GET /places/{id:guid}` | `MediaReader` |
| `GET /places/{id:guid}/ancestors` | `MediaReader` |
| `GET /places/{id:guid}/media?o=&f=&seed=` | `MediaReader` |
| `GET /places/{id:guid}/categories?o=&f=` | `MediaReader` |

The listing returns an **empty array**, not 404, when a place has no visible
children - a place whose children the caller cannot see is legitimately a leaf to
them, and 404 would make the last level of every browse look broken. The
drill-ins keep the person routes' 404 rule.

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

### Phase 5 results

`dotnet build` clean, and 12 new tests in
`tests/MawMedia.Services.Tests/Api/PlaceRoutesTests.cs` bring the suite to **262
passing, 0 failures**. They cover: `MediaReader` enforcement (a `LocationRead`
token is rejected), the derived hierarchy and its parentage, counts rolling up,
the `kind` filter, single-place fetch, the breadcrumb order and depths, absolute
file urls, per-place `MediaCount` on categories, the 404 rules, negative-offset
rejection, an ungeocoded location staying unbrowsable, and per-caller scoping.

The tests resolve the tree by drilling from the root rather than hardcoding ids,
so they assert the drill-down works as a side effect of setting themselves up.

Two things the build could not have caught, verified directly against the dev
restore: every `SELECT * FROM media.get_place_*(...)` in `PlaceRepository`
executes with its parameters in **positional** order matching the function
signature, and every returned column name maps onto its Dapper row class under
`MatchNamesWithUnderscores`.

---

## 10. Tests

The seeder now derives this tree, where before every media pointed at
`LOCATION_NY`:

```
USA             -> NY      -> New York   (MEDIA_TRAVEL_1 ...)
                -> MA      -> Boston     (MEDIA_PLACE_MA, MEDIA_PLACE_OVERRIDE)
United Kingdom  -> England -> London     (MEDIA_PLACE_UK, admin only)
```

- `LOCATION_MA`'s columns were shifted - `Locality` held `"Massachusetts"` and
  `Neighborhood` held `"Boston"`. Fixed, with `Suffolk County` and `North End` in
  their proper slots.
- `DatabaseSeeder` calls `media.assign_all_location_places()` after the locations
  land, since the deploy's own pass runs long before the fixtures exist. Same
  idempotent function the deploy calls, not a test-only path.
- Three media, three files and `LOCATION_UK` added. They **reuse the existing
  categories** rather than adding new ones: `CATEGORY_TRAVEL` is already shared
  with `ROLE_FRIEND` and `CATEGORY_FOOD` is already admin-only, which is exactly
  the pair of visibility rules these fixtures need. An earlier attempt added two
  new categories and broke nine unrelated assertions across `GetCategories`,
  `GetYears`, `SearchCategories` and `GetStats` - including one where a category
  named "Places Private" started matching a search for `"Private"`.
- `LOCATION_UNK` gives the null-metadata / not-browsable case for free.
- Two pre-existing theory tests legitimately needed new expectations, since the
  fixture set grew: `GetRandomMedia` (admin 3 -> 6, johndoe 1 -> 3) and
  `GetCategoryMedia` (travel 1 -> 3, food 0 -> 1).

**Watch out for static initialization order in `Constants.cs`.** Fields
initialize top to bottom, and a forward reference to `USER_ADMIN` reads
`Guid.Empty` rather than failing to compile - it surfaces much later as a foreign
key violation during seeding. The place fixtures are declared below everything
they depend on, with a note saying so.

### Phase 6 results

**265 tests passing, 0 failures** - 15 of them in `PlaceRoutesTests`.

Each fixture added in this phase buys a specific test:

| fixture | proves |
|---|---|
| `MEDIA_PLACE_MA` in a shared category | the tree branches, and a country's count is the sum of its states rather than only coordinates filed directly against it |
| `MEDIA_PLACE_OVERRIDE` (recorded NY, overridden MA) | `COALESCE` precedence end to end - it browses under Boston and is **absent** from New York |
| `MEDIA_PLACE_UK` in an admin-only category | a place with nothing visible is absent from the listing entirely, not merely zero, and its drill-ins 404 |

That last one is the sharper form of the access rule: reporting "United Kingdom:
0 photos" would leak that the country exists at all.

Then `tests/MawMedia.Services.Tests/Api/PlaceRoutesTests.cs` modeled on
`PersonRoutesTests`: scope enforcement, visibility filtering, per-caller
`media_count`, drill-down, paging/has-more, the 404 rule, favorites-both-ways on
categories. Plus repository-level tests for the derivation itself (idempotency,
alias reuse, null handling).

---

## 11. Admin surface

The derived tree is only as good as the geocoder, and the audit found places it
got wrong in ways no normalizer can fix by guessing. Two corrections are built,
both admin-gated behind `LocationWriter`, both surviving re-derivation because
`media.place_alias` records the correction rather than the place's name.

| route | what it fixes |
|---|---|
| `POST /places/{id}/merge` body `{ sourceId }` | a place the geocoder spelled two ways, or filed twice |
| `PUT /places/{id}/parent` body `{ parentId }` | a place filed in the wrong branch |

**Merge is the more useful of the two, including where re-parent looks like the
answer.** `United States -> Acton` appeared to need re-parenting, but there was
already a `Massachusetts -> Acton` holding 3,675 locations - one town the geocoder
filed twice, once with a state and once without. Merging consolidates to 3,677;
re-parenting would have left two Actons under Massachusetts with a suffixed slug.
So merge deliberately does **not** require a shared parent, only a shared kind.

**Re-parenting leaves the aliases alone**, and that is the subtle part.
`media.place_alias` records what the *geocoder* produces - `('city', United
States, 'acton')`, because that coordinate really has no state - while
`media.place.parent_id` records where an admin decided it belongs. After a move
the two disagree on purpose: the next derivation pass still looks up the old
tuple, still finds the alias, and still resolves to the place, which now hangs
under its new parent. Repointing the alias would make that lookup miss and quietly
recreate the place under the parent it was just moved out of.

### There is no hidden flag

`media.place` briefly carried `is_hidden`. It was removed before ever being
written, because the case it was meant for turned out to be better served by the
other two corrections.

Worked through on the real `Macao -> Guangdong -> Zhuhai` branch, where three
coordinates whose `formatted_address` reads *"Skyline, 24 Estrada de Dom João
Paulino, Macao"* were assigned a Chinese province and the mainland city across the
border:

| | result |
|---|---|
| hide the bogus state | tile gone, but the parent claims 28 while its visible children sum to 23, and the photos are unreachable by drilling |
| merge Zhuhai into Macau | `Macau 22 · Taipa 2`, counts reconcile, photos stay browsable - **and the bogus state vanishes from the listing on its own** |

That last point is why the flag was unnecessary: `media.get_places` inner joins to
visible media, so a place emptied by a merge or a re-parent stops being listed
without anything having to mark it. Hiding instead papers over the wrong data,
strands the hidden node's children, and widens a parent-versus-children count gap
a client may well display.

Reinstate it only if a place ever needs to be kept out of the listing *while still
holding media somebody can see*. No such place exists in the library.

### Finding the second place

Merging means finding a duplicate, and drilling to it requires already knowing
where it is - which is exactly what is unknown when hunting duplicates. So
`get_places` takes a `_search` term (`?q=`) that matches a name **anywhere in the
tree**, ignoring `parent`, because the duplicates worth finding sit in *different*
branches. `kind` still applies, so "every city called Zhuhai" is one call.

Matching uses the same normalized form derivation uses, via `STRPOS` rather than
`LIKE`, so a term containing `%` or `_` is a literal rather than a pattern. A term
that normalizes to nothing falls through to the country listing instead of
matching everything.

It earned itself immediately: searching `zhuhai` turned up a **third** duplicate
neither the audit nor the phase 0 queries had found - `Zhuhai Shi` under
`Guangdong Sheng`, alongside the two known `Zhuhai` rows.

`Place` also carries **`ancestorNames`** (root first, excluding itself), because
search results are otherwise ambiguous in exactly the case that motivated the
feature: two cities called Zhuhai, both under a parent called Guangdong, separated
only by the grandparent. And **`coverMediaId`**, so an admin picker can show the
current choice as selected - `coverUrl` names the published copy, not the
original.

### Still unbuilt

`media.rename_place` - trivial once these exist, and the audit found no case
needing it: names arrive consistently long-form.

---

## 12. Phases

| phase | contents | status |
|---|---|---|
| 0 | data audit - tunes the normalization rules | **complete** (section 2) |
| 1 | schema: `place_kind`, `place`, `place_alias`, `location.place_id`, seed, deploy.sh | **complete** |
| 2 | derivation: normalize, slugify, assign, backfill, call-site wiring | **complete** |
| 3 | views: `media_location`, `user_location`, indexes | **complete** |
| 4 | read functions: descendants, ancestors, `get_places`, `get_place_media`, `get_place_categories` | **complete** |
| 5 | C#: model, repository, routes, DI | **complete** |
| 6 | tests + seeder work | **complete** |
| 7 | admin surface: merge + re-parent | **complete** (section 11) |
| 8 | admin picked cover images | **complete** (section 14) |
| 9 | the reverse lookup: which places one media was taken at | **complete** (section 15) |
| 10 | `child_count`, so a browse never offers a drill-in that leads nowhere | **complete** (section 16) |

---

## 13. Open questions

1. ~~**Place tiles - image or not?**~~ - **resolved: an admin hand picks one, from
   the library.** Three approaches were tried and two rejected:
   - a teaser drawn from the caller's own media - rejected on cost, since it must
     be a photo *that caller* can see, needing a `LATERAL` per tile (city listing
     144ms -> 991ms)
   - curated stock, via Wikidata `P948`/`P18` - prototyped against all 271 real
     places and rejected on quality. Only **47%** resolved to something usable
     unreviewed; coordinate matching returned the nearest "human settlement",
     which put a Buddhist temple on Bangkok and a racetrack on Arcadia.
   - Places365 scene classification in maw-media-ai - tried and rejected: it did
     not pick well enough for this library.

   What shipped instead is **phase 8** below.

2. ~~**`unaccent` extension**~~ - **resolved by the phase 0 audit: not adding
   it.** One non-ASCII value exists in the whole table and `unaccent` would not
   help it.
3. **Cloning vs. generalizing** - this makes a third near-copy of the "page over
   media, then fan out files" pattern. Current plan is to clone for v1 (matching
   how clans extended the person functions) and revisit if a fourth subject type
   appears.
4. ~~**`GET /places/kinds`**~~ - **resolved: not building it.** Exposing the kind
   codes would be nearly free, but nothing needs them: `kind` comes back on every
   place, and a client rendering a mixed listing can read it there rather than
   fetching a vocabulary first.

5. ~~**Neighborhood as a real level?**~~ - **resolved: no.** Too granular for what
   this browse is for. `media.location`'s `neighborhood` and
   `sub_locality_level_1` stay where they are, feeding the city fallback in
   `media.assign_location_place`, and city remains the deepest level.

All open questions are now closed. What remains is phase 7, the deferred admin
surface - see section 11.

---

## 14. Phase 8 - admin picked cover images

An admin browses a place's photographs, chooses one, and a **copy** of it is
published to a directory any signed in caller may read **without the per file
access check** the rest of `/assets` applies.

Browsing needs no new endpoint - `GET /places/{id}/media` already lists exactly
the candidates.

| route | authorization |
|---|---|
| `PUT /places/{id:guid}/cover` (body `{ mediaId }`) | `LocationWriter` **and** admin |
| `DELETE /places/{id:guid}/cover` | `LocationWriter` **and** admin |
| `GET /assets/covers/{guid}.avif` | signed in + `media:read`, **no per-file check** |

### The authorization rule

| branch | requires | per-file check |
|---|---|---|
| `/assets/...` (media) | signed in | **yes** - `AllowAccessToAsset` per path |
| `/assets/faces/...` | signed in + `face-recognition:read` | yes - `CanViewFace` |
| `/assets/covers/...` | signed in + `media:read` | **no** |

Skipping the per-file check is the point: an admin chose the photograph to
represent a place, and the tile has to render for anyone browsing - including a
caller who cannot reach the category it came from. The control is in the choosing,
not the serving.

It is **not** public. A signed out caller is challenged like anywhere else under
`/assets`, and the response is `Cache-Control: private` so a shared cache cannot
hand it to one.

Two places exclude the prefix by hand, exactly as they already exclude
`/assets/faces`: the `UseWhen` predicate in `StaticFilesExtensions`, and
`MediaStaticAssetAuthorizationHandler`, which declines rather than applying the
media rule to a file it knows nothing about. The cover files also live outside the
asset root, and `PlaceCoverStore` refuses to construct if the two overlap.

### Which rendition is published

Always **`qvg-fill`** (320x240, cropped to fill), never the largest that fits. The
scale is fixed rather than ranked because `qvg-fill` crops to a constant aspect, so
every tile is the same shape whatever the source photograph's orientation. Ranking
by size, which this first did, gave a portrait cover for a portrait original and
the grid looked broken.

A media without that rendition is **refused** rather than falling back - 16 of the
167,202 media are in that state, and a differently shaped image is worse than none.

### Why a copy, and why never `src`

- **`src` originals carry GPS and camera identity.** A 2023 phone photo embeds
  `51 deg 30' 4.63" N` (London); a 2022 one `42 deg 35' 51.30" N` (Massachusetts).
- **Derived renditions carry none.** 27 purely structural AVIF tags, zero sensitive
  tags across 40 sampled files spanning years and scales.

So `media.get_place_cover_candidate` **never returns `src`**, and the copy is
byte-for-byte - no re-encode, no imaging dependency. Publishing an original would
have handed those coordinates to every signed in caller, including ones who cannot
see the photo itself.

### The stored path is a url, not a filesystem path

`media.file.path` holds `/assets/2007/hk-macau/qvg-fill/x.avif`, and
`AssetPathBuilder.Build` turns it into a url by plain concatenation - which is why
it is stored that way. Reading the file means undoing that: the asset root on disk
*is* what `/assets` resolves to, so the prefix comes off before the join. Joining
it whole yields `<root>/assets/2007/...` and a `FileNotFoundException`.

`CategoryZipWriter` already knew this and had a private helper; it is now
`AssetPathBuilder.ToRelativeFilePath`, shared by both readers, since it is one rule
feeding two file reads.

The fixtures used `/media/nature1.jpg` - neither prefixed nor laid out like
production - and because no test read a file off disk, nothing noticed until
publishing a cover did, in production. They now store production-shaped paths and
the harness resolves them through the same helper.

### Naming, and what a failure leaves behind

The file is always **`{place_id}.avif`** - derivable from the row, with no stored
name, the same choice `media.face` makes for its crops. Replacing a cover therefore
overwrites one file rather than orphaning a previous name, so there is no
displaced-file bookkeeping at all. Freshness comes from the url: `cover_created` is
appended as `?v=`, which is what keeps `Cache-Control: immutable` honest.

- **Set:** publish the file, then write the row. A row claiming a cover must never
  precede the file, because a tile cannot recover from a broken image. If the
  database refuses, the file is removed again.
- **Clear:** clear the row, then delete the file.
- Worst case either way is an orphaned `{place_id}.avif` no row points at. The one
  cost of overwriting in place: a refused *replacement* deletes the existing cover,
  so a place that had one loses it. Row and disk stay consistent, which is the
  property that matters.

### Constraint on the choice

The media must sit at that place **or beneath it**, so a city photograph may
represent its country while an unrelated one is refused. The check runs against the
admin's own `media.user_location`, so it obeys the same visibility rule as every
other read.

### Phase 8 results

The defining test is `ACoverSkipsThePerFileCheckThatGovernsTheRestOfAssets`: an
admin sets New York's cover from a photograph in a category `ROLE_FRIEND` cannot
reach, and the restricted caller is then refused the original at
`/assets/media/...` but served the cover at `/assets/covers/...`, and sees its url
in the listing.

Two tests pin the rendition choice: the published bytes are the `qvg-fill` file and
*not* the full-hd beside it, and a media lacking `qvg-fill` is refused with nothing
published on the way to failing. Every fixture rendition has a distinct body so the
bytes identify which file was copied.

**A pre-existing harness race surfaced here.** `ApiFactory` passes root directories
as process-global environment variables, so at 32-wide parallelism one factory can
overwrite one between another writing it and that other's host reading it. Every
read path composes urls without opening a file, so it had never mattered.
Publishing a cover is the first operation that opens one. The asset, cover **and
face** roots are now shared across factories, which removes the race rather than
narrowing it - the face root was missed on the first pass and produced an
intermittent `PutImageStoresBytesThatCanBeReadBack` failure until it moved across
too.

---

## 15. Phase 9 - the reverse lookup

Every read above goes place -> media. The cover picker in the photo app wanted the
other direction: an admin looking at one photograph, deciding it should represent
where it was taken, and needing the *place ids* to say so - which a client holding
only a media id had no way to obtain.

```
GET /media/{id:guid}/places      MediaReader
```

Returns the chain holding the media, **country first**: `[USA, MA, Boston]`.

### Whole places, not the breadcrumb

`PlaceAncestor` would have been the obvious shape and is the wrong one. The screen
offering to replace a cover has to show the cover currently in force at each rung,
and which photograph it came from, or an admin cannot tell what they are about to
overwrite. So each rung is a full `Place`, and `media.get_media_places` gets it by
**calling `media.get_places` once per rung** rather than restating its aggregate -
covers, counts and ancestor names here can therefore never disagree with the ones
the browse shows. The tree is three deep, so that is at most three single-place
aggregates.

### Where the access rule comes from

Nowhere new. `media.user_location` yields nothing for a media the caller cannot
see, and `media.get_places` drops a rung the caller may see nothing at - so the
route cannot be used to discover that a place exists, nor that a media does.

It answers **`200` with an empty array**, never `404`, matching
`GET /media/{id}/faces`: a media with no location, one never geocoded, and one the
caller cannot see are all "nothing to offer here", and the media route itself
already tells those apart. The override column is honoured, because
`media.user_location` composes `media.media_location` - a photograph corrected into
another state offers that state, not the one its file claimed.

### Scope

`media:read`, not `location:read` or `location:write`. Naming where a photograph
was taken is browsing; the location scopes are for maintaining the geocode and
administering the tree. Choosing one of the returned places as a cover is still
`LocationWriter` plus the database's own admin check.

---

## 16. Phase 10 - no dead ends

The browse offered a drill-in on every tile, and a city has nothing inside it -
so clicking one landed on a page whose only content was "nothing sits inside this
place". The photographs were one more click away, up in the summary, which is the
wrong way round: at the bottom of the tree the photographs *are* the answer.

`media.get_places` therefore returns **`child_count`** - how many places sit
directly inside this one **that the caller can see** - and a client sends a tile
with none straight to the media.

### Why it cannot be done in the client

A city having no children follows from `kind`; there is no level below it. That
covers 239 of the ~280 nodes and tempts a client-side rule.

It is wrong for the other 41. A state whose media all geocoded to the state level
has no cities, and **a state whose only cities sit in categories this caller
cannot reach is just as much a leaf to them** - the listing hides those children
by the same rule that hides the place itself. Nothing else in `Place` says so, and
a client that guessed from `kind` would leave exactly the restricted users staring
at the dead end this removes.

### Why it is free

The count comes out of the join that was already there. `subtree` now carries
**`via_child`** - which of the candidate's own children a descendant hangs from,
null for the candidate itself - and the aggregate counts
`COUNT(DISTINCT s.via_child)`. Since the query already inner joins
`media.user_location`, only children with something visible beneath them survive
to be counted, which is the listing rule restated in the one place it was already
being applied. No second pass over the tree, no per-tile subtree probe, and no
possibility of the number disagreeing with what a drill-in would show - a
property one of the tests asserts directly, by walking the whole tree and
comparing `child_count` against the listing at every level.

`_kind` is deliberately **not** applied to it. It answers "does drilling in show
anything", and the kind filter is something the user can clear; counting through
it would grey out a tile because of a filter rather than because of the tree.

### The one shape rule this exposed

`media.get_media_places` returns `media.get_places` rows verbatim, so its
`RETURNS TABLE` restates that function's shape - and postgres refuses the
mismatch at call time rather than at deploy time. Adding a column here means
adding it there too; three tests failed with `42804` until it was.
