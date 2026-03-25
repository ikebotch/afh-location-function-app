# AFH Location Service

## Service Ownership Split

### Location Service (this repo)
- Owns adviser discovery and search inputs.
- Reads advisers from SharePoint (region, skills, rating, home postcode).
- Reads adviser availability from Calendar Service schedule endpoint.
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

## Integration Contract (Location -> Calendar Service)
- Endpoint: `GET /api/v1/calendar/users/{userId}/schedule`
- Query:
  - `startUtc`
  - `endUtc`
- Auth:
  - `Authorization: Bearer <shared internal token>` via `CalendarService:InternalToken`

## Required Local Config (Location)
- Copy `src/AFH.Location.Service.Api/local.settings.template.json` to `src/AFH.Location.Service.Api/local.settings.json`.
- Fill in `SharePointGraph:*`, `SharePoint:Advisers:*`, `CalendarService:*`, `InternalApiAuth:*`, and `BusinessTime:TimeZone`.
- Keep `Maps:Google:Enabled=false`. The Google provider path is intentionally disabled until routing and geocoding are fully implemented.

## API Docs
- Scalar UI: `/api/scalar`
- OpenAPI JSON: `/api/openapi/v1.json`

## Protected Routes
- Health and docs remain public.
- Adviser search, batch search, coverage, and license endpoints are protected by internal bearer auth unless development auth relaxation is explicitly enabled.
