# AFH Location Service

## Service Ownership Split

### Location Service (this repo)
- Owns location/geography data and location-owned reference data.
- Owns geocode and route caching for adviser search and ranking.
- Remains the source for location boundaries, office/branch metadata, and map/routing provider integration.

### Adviser Service (separate repo)
- Owns adviser profile, skill/license, availability-rule, coverage-region, and adviser-region assignment APIs.
- Uses the Location Service for geography/routing concepts when needed.
- The standalone service folder is `../afh-adviser-function-app`.

### Identity Service (separate repo)
- Owns Entra token validation, current-user resolution, roles, permissions, and RBAC assignment APIs.
- The standalone service folder is `../afh-identity-function-app`.

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
- During migration these adviser-oriented read models can remain in Location for compatibility, but new gateway wiring should route admin adviser operations to the standalone Adviser service.

## Organisation Assignment Resolution
- `GET /api/v1/admin/organisation-assignments` remains the admin/config list endpoint. When no adviser, organisation, or region scope is supplied it can return all matching assignment rows for the requested context and assignment types.
- `GET /api/v1/admin/advisers/{adviserId}/organisation-assignments?context=Booking&assignmentTypes=ContactCentre,OperationsManager,ReportingManager,Fallback` resolves the assignments that apply to one adviser.
- The adviser-scoped resolver loads the adviser from the cached adviser reference model, derives the organisation scope from the adviser cache `BaseOfficeId` and the region from `Region`, then returns only enabled assignments matching the adviser, organisation+region, organisation-wide, region-only, or fallback scopes.
- Scoped responses include match metadata: `matchLevel`, `matchedOrganisationId`, `matchedRegion`, `matchedAdviserId`, and `priority`.
- `Fallback` assignments are returned only when requested and no more specific assignment matches.
- Booking should call the adviser-scoped resolver when it needs assignment recipients; Notification should only deliver to recipients already resolved by Booking.

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
- Copy `src/AFH.Location.Service.Functions/local.settings.template.json` to `src/AFH.Location.Service.Functions/local.settings.json`.
- Fill in the required values:
  `AzureAD:*`, `SharePoint:Advisers:SiteId`, `SharePoint:Advisers:ListId`, `CalendarService:BaseUrl`, `CalendarService:FunctionKey`, `CalendarService:InternalToken`, and `InternalApiAuth:Token`.
- The template now also includes the active SharePoint field-name mapping keys used by the current `SharePointAdviserRepository` implementation, so local list-field overrides do not have to be discovered by source inspection.
- Keep `Maps:Google:Enabled=false`. The Google provider path is intentionally disabled until routing and geocoding are fully implemented.

## Local Settings Conventions
- Internal bearer auth uses `InternalApiAuth:Token`.
- Calendar downstream auth uses `CalendarService:BaseUrl`, `CalendarService:FunctionKey`, and `CalendarService:InternalToken`.
- SharePoint config stays under `SharePoint:Advisers:*` because those keys map directly to the active infrastructure options and SharePointUtils consumption path.

## API Docs
- Scalar UI: `/api/scalar`
- OpenAPI JSON: `/api/openapi/v1.json`

## Protected Routes
- Health and docs remain public.
- Adviser search, batch search, coverage, license, and cache-sync endpoints are function-key protected and also require internal bearer auth unless development auth relaxation is explicitly enabled.
