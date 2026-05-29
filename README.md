# AFH Location Service

## Service Ownership Split

### Location Service (this repo)
- Owns adviser discovery and search inputs.
- Uses SQL-cached adviser reference data on the hot path and keeps live adviser-source reads behind a sync path.
- Owns geocode and route caching for adviser search and ranking.
- Reads adviser availability from Calendar Service batch schedule endpoints using `PreferCached` freshness on search paths.
- Returns ranked advisers with coverage and travel outputs.

### Booking Service (separate repo)
- Owns booking lifecycle: create/confirm/cancel.
- Persists booking state and exposes booking APIs.
- Integrates with Calendar Service for appointment create/cancel when booking state changes.

### Calendar Service (separate repo)
- Owns all direct calendar-provider integration.
- Owns webhook/subscription flows.
- Exposes schedule and appointment APIs consumed by booking/location.

## Why This Split
- Avoids duplicated calendar integration logic.
- Keeps booking and event ownership clear.
- Reduces config/auth drift across services.
- Keeps live SharePoint and map-provider traffic out of the adviser search hot path.

## Cached Reference And Routing Model
- `AdviserReferenceCache` is the local SQL-backed read model for adviser profile/ranking inputs.
- `GeoCacheEntries` stores resolved coordinate results for adviser and office lookups.
- `RouteCacheEntries` stores travel-time and distance results for repeated routing evaluations.
- `POST /api/v1/admin/advisers/cache/sync` refreshes the adviser reference cache from the configured live adviser source.
- Search reads `IAdviserRepository`, which now resolves from cache first and only falls back to the live source when the cache is empty.

## Integration Contract (Location -> Calendar Service)
- Endpoint: `POST /api/v1/calendar/users/schedule/batch`
- Body:
  - `userIds`
  - `startUtc`
  - `endUtc`
  - `freshnessMode=PreferCached`
- Auth:
  - `x-functions-key: <calendar function key>` via `CalendarService:FunctionKey`
  - `Authorization: Bearer <shared internal token>` via `CalendarService:InternalToken`

## Required Local Config (Location)
- Copy `src/AFH.Location.Function/local.settings.template.json` to `src/AFH.Location.Function/local.settings.json`.
- Fill in the required values:
  `DomainUserAuth:*`, `ConnectionStrings:AdviserDirectoryDb`, `SharePoint:Advisers:SiteId`, `SharePoint:Advisers:ListId`, `CalendarService:BaseUrl`, `CalendarService:FunctionKey`, `CalendarService:InternalToken`, and `InternalApiAuth:Token`.
- The template now also includes the active SharePoint field-name mapping keys used by the current `SharePointAdviserRepository` implementation, so local list-field overrides do not have to be discovered by source inspection.
- Keep `Maps:Google:Enabled=false`. The Google provider path is intentionally disabled until routing and geocoding are fully implemented.

## Local Settings Conventions
- Internal bearer auth uses `InternalApiAuth:Token`.
- Calendar downstream auth uses `CalendarService:BaseUrl`, `CalendarService:FunctionKey`, and `CalendarService:InternalToken`.
- Adviser-backed user RBAC for `/api/v1/me` uses `DomainUserAuth:*` for bearer-token validation and `ConnectionStrings:AdviserDirectoryDb` for role and permission lookup.
- SharePoint config stays under `SharePoint:Advisers:*` because those keys map directly to the active infrastructure options and SharePointUtils consumption path.

## API Docs
- Scalar UI: `/api/scalar`
- OpenAPI JSON: `/api/openapi/v1.json`

## Protected Routes
- Health and docs remain public.
- Adviser search, batch search, coverage, license, and cache-sync endpoints are function-key protected and also require internal bearer auth unless development auth relaxation is explicitly enabled.
