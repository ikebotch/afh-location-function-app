# AFH Location Service

## Service Ownership Split

### Location Service (this repo)
- Owns adviser discovery and search inputs.
- Reads advisers from SharePoint (region, skills, rating, home postcode).
- Reads adviser availability from Booking Service `getSchedule`.
- Returns ranked advisers with coverage and travel outputs.

### Booking Service (separate repo)
- Owns booking lifecycle: create/confirm/cancel.
- Owns calendar write operations for booked events.
- Owns booking/calendar webhook processing.

## Why This Split
- Avoids duplicated calendar integration logic.
- Keeps a single source of truth for booking and event state.
- Reduces config/auth drift across services.

## Integration Contract (Location -> Booking)
- Endpoint: `GET /api/v1/calendar/advisers/{adviserId}/schedule`
- Query:
  - `startUtc`
  - `endUtc`
- Auth:
  - `x-functions-key` header via `BookingCalendar:FunctionKey` (when required)

## Required Local Config (Location)
- `SharePointGraph:*`
- `SharePoint:Advisers:*`
- `BookingCalendar:BaseUrl`
- `BookingCalendar:FunctionKey`
- `LocationSearch:*`

## API Docs
- Scalar UI: `/api/scalar`
- OpenAPI JSON: `/api/openapi/v1.json`
