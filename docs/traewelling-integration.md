# Träwelling integration

OVDB imports check-ins from [Träwelling](https://traewelling.de) as trips (RouteInstances).
It only reads: nothing is ever written back to Träwelling. The OpenAPI spec of the upstream API
is in `docs/traewelling.json`; check it before assuming an endpoint's shape.

## Code

| Part | Job |
| --- | --- |
| `TraewellingApiClient` | OAuth connect, token lifetime and every HTTP call (`SendAsync`, through the shared rate limiter) |
| `TraewellingTripMapper` | Status → trip DTO with station-local times; the conflict comparisons |
| `TrawellingService` | The inbox, conflicts, webhook events, linking and backfill |
| `TraewellingRateLimiter` | Singleton rate-limit budget shared by every request scope |
| `TraewellingController` | The `/api/traewelling` endpoints below |
| `TraewellingTokenRefreshService` | Daily: refreshes idle users' tokens so the 30-day refresh token never lapses |
| `TraewellingInboxSweepService` | Daily: sweeps every connected user's statuses into the inbox |
| `TraewellingResyncService` | Runs a user-requested full resync in the background |

## Configuration

The `Traewelling` section; see `appsettings.traewelling.example.json`. `ClientSecret` is a
secret: production sets it as the environment variable `Traewelling__ClientSecret`, never in a
tracked file. `WebhookUrl` enables live sync and must exactly equal the webhook URL registered on
the OAuth client, so it only works in production. Locally, set
`Traewelling:EnableBackgroundServices` to `false` (see CLAUDE.md).

## How check-ins get in

1. **Connect:** `GET connect` returns the authorize URL (with a one-use, 10-minute state);
   Träwelling redirects to `GET callback`, which stores the tokens and, with live sync, the
   webhook id and secret.
2. **Inbox:** statuses land in `TrawellingInboxStatuses`, from a sweep of the user's statuses
   (on demand when stale, daily, or a full resync) or from a webhook delivery.
3. **Import:** the user picks a route for a pending status; the frontend saves the trip through
   `PUT api/routes/instances` with the status id, which removes the inbox row. A check-in can
   become one trip per user (a second import gets 409). `POST link` attaches a status to an
   existing trip of the user instead.
4. **Live sync:** `POST webhook` (anonymous, HMAC-SHA256 signed with the per-webhook secret)
   applies `checkin_create/update/delete`. An update or delete of an imported check-in whose
   journey really changed becomes a *conflict* the user resolves (apply times, re-import,
   dismiss, delete the trip).

Access tokens live 60 minutes and refresh tokens 30 days, and **refresh tokens rotate on use**:
refreshes are serialised per user, and tokens are only cleared on `invalid_grant`.

## Endpoints (`/api/traewelling`)

| Endpoint | Purpose |
| --- | --- |
| `GET connect`, `GET callback` | OAuth connect flow |
| `DELETE disconnect` | Clear the tokens and remove the webhook |
| `GET status` | Connected? Live sync available and healthy? |
| `GET unimported`, `POST ignore` | The inbox, and hiding a status from it |
| `POST resync` | Start a full resync (progress over the hub) |
| `GET conflicts`, `POST conflicts/{id}/apply-times`, `…/reimport`, `…/dismiss`, `…/delete-instance` | Upstream edits of imported trips |
| `GET route-instances`, `POST link` | Find a trip on a date and link a status to it |
| `GET stats`, `GET alerts` | Counts for the page; Träwelling's own warning banners |
| `POST backfill-scheduled` | Fill planned times on old imports |
| `POST webhook` | Live sync deliveries (anonymous, signature-checked) |

## Live updates (`/traewellingHub`)

JWT-authenticated SignalR hub, delivering only the connected user's own events. Every payload
is a JSON string in the same shape as the REST responses, so the client parses rather than
maps it.

| Event | Sent when |
| --- | --- |
| `PendingTripUpserted` / `PendingTripRemoved` | A webhook event changed the unimported list |
| `ConflictUpserted` / `ConflictRemoved` | A webhook event changed the conflict list |
| `ResyncProgress` | Every five pages of a running resync: `{ pagesRead, added }` |
| `ResyncFinished` | A resync ended: `{ added, pagesRead, complete }` |

`complete: false` means the walk stopped on an error or the 1000-page safety limit, so older
check-ins may still be missing; a rerun starts at page 1 again.
