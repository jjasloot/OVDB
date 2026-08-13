# Träwelling webhooks — live check-in sync plan

Status: **planned, not yet implemented** (2026-08-13)

Goal: new Träwelling check-ins appear in OVDB's "unimported trips" list on their own,
without paging through the statuses API. The manual review-before-import flow stays
exactly as it is — webhooks only feed the list, they never import anything.

Verified against the Träwelling source (local clone, `app/Http/Controllers/Backend/`):

- Webhook creation happens in the **OAuth authorize flow**: `trwl_webhook_url` +
  `trwl_webhook_events` (comma-separated) on the authorize request. The OAuth client
  must have `webhooks_enabled` and the URL must **exactly equal** the client's stored
  `authorized_webhook_url` (both self-service in Träwelling's app dashboard).
- The **token-exchange response** is extended with `webhook: {id, secret, url}` —
  the only moment the secret is handed out, hence a one-time reconnect per user.
- Events: `checkin_create`, `checkin_update`, `checkin_delete` (we skip `notification`).
- Delivery: POST with payload `{event, status: StatusResource}` (same status shape the
  statuses API returns), headers `X-Trwl-User-Id`, `X-Trwl-Webhook-Id`,
  `X-Trwl-OAuth-Client-Id`, and `Signature` = HMAC-SHA256 (hex) of the JSON body with
  the webhook secret (Spatie DefaultSigner).
- Repeated delivery failures set `disabledAt` on the webhook (visible via `GET /webhooks`).

---

## 1. The inbox — how pre-trips are stored

New entity `TraewellingInboxStatus` — one row per known-but-not-imported check-in:

| Column | Type | Notes |
|---|---|---|
| `Id` | int PK | |
| `UserId` | int FK → Users | |
| `TrawellingStatusId` | long | unique index on `(UserId, TrawellingStatusId)` |
| `PayloadJson` | longtext | the raw `StatusResource` JSON, stored verbatim |
| `State` | enum | `Pending`, `ChangedAfterImport`, `DeletedUpstream` |
| `Source` | enum | `Webhook`, `Sweep` |
| `DepartureAt` | datetime | denormalised from payload, for sorting the list |
| `ReceivedAt` / `LastEventAt` | datetime | first seen / last event or sweep touch |

Design decisions:

- **Raw JSON payload, parsed on read.** The mapping logic (StatusResource → trip DTO)
  keeps evolving with the upstream API; storing the raw object means a mapping fix
  applies retroactively to everything in the inbox. The list is small (pending trips
  only), so parse-on-read costs nothing. `DepartureAt` is the one extracted column so
  the list can sort without parsing.
- **The inbox is the single source for the unimported list — for everyone.** The
  frontend's unimported page reads the inbox (instant, no upstream calls); the statuses
  API is only touched by the sweep (§3) and the existing manual refresh button. One
  code path for webhook and non-webhook users; the webhook is just a faster feeder.
- **Imported/ignored state stays where it lives today** (`RouteInstance.TrawellingStatusId`,
  `TrawellingIgnoredStatuses`). Importing or ignoring a trip **deletes** its inbox row —
  the inbox is a queue, not an archive. No state duplication, no drift.

## 2. Updates and deletes after import

Events arriving for a status OVDB already knows about:

| Event | Status is pending | Status was imported | Status was ignored |
|---|---|---|---|
| `checkin_create` | upsert payload (idempotent) | ignore | ignore (stays ignored) |
| `checkin_update` | replace payload, bump `LastEventAt` | **flag, never auto-apply** → inbox row with `State = ChangedAfterImport` holding the new payload | ignore |
| `checkin_delete` | delete the inbox row (nothing curated yet) | **flag, never auto-delete** → `State = DeletedUpstream` | delete ignore-row (moot) |

Rationale: an imported RouteInstance is curated data — the user may have fixed the
route geometry, times or operator. Träwelling-side edits (people often correct the
destination after missing a stop, or times get delay corrections) must not silently
overwrite that. Instead the unimported page grows a small "changed upstream" section:

- `ChangedAfterImport`: shows what differs (old vs new: times, origin/destination,
  line) with actions **Apply times** (update the RouteInstance start/end only),
  **Re-import** (unlink + back to pending), and **Dismiss** (delete the flag row).
- `DeletedUpstream`: "this check-in was deleted on Träwelling" with **Delete trip
  here too** and **Keep my copy** (dismiss). OVDB is the long-term archive; upstream
  deletion is a signal, not a command.

Dismissing never re-triggers: the flag row is deleted, and a later identical event
recreates it only if the payload differs from what was already dismissed
(compare a hash of the payload stored on dismiss — cheap, avoids nagging).

## 3. Missed data — the reconciliation sweep

Webhook delivery is best-effort: OVDB deploys/downtime lose deliveries, and repeated
failures make Träwelling disable the webhook entirely. The safety net is a **sweep**
that reuses today's pull logic (`GetUnimportedStatusesAsync`), but writes into the
inbox instead of returning a response:

- Page through `/user/{username}/statuses` (newest first), upserting unknown statuses
  as `Pending` (`Source = Sweep`).
- **Stop early**: stop when a full page contains only already-known status ids
  (known = inbox ∪ imported ∪ ignored), with a hard cap of ~5 pages per run. First-ever
  run for a user pages until the cap and continues on subsequent runs.
- Triggers:
  - daily, from a background job (piggybacks on the existing hosted-service pattern);
  - when a user opens the unimported page and the last sweep is older than ~1 hour;
  - the existing manual refresh button (always allowed — user control wins).
- The sweep cannot see upstream *deletes* of pending statuses (they just stop appearing).
  Cheap heal: during a sweep, any `Pending` row whose id falls inside the swept page
  range but is absent from the results is deleted. Webhook users get this via
  `checkin_delete` anyway.
- All of this rides the existing `TraewellingRateLimiter`, so sweeps can never trip
  the rate limit for interactive use.

Webhook health: the connection-status endpoint (and the daily job) calls
`GET /webhooks`; a missing webhook or `disabledAt != null` surfaces in the profile as
"live sync disabled — reconnect to re-enable", and the user falls back to sweep-only
until they reconnect. Webhook recreation *requires* the OAuth flow (that's where the
secret comes from), so "reconnect" is the honest remedy.

## 4. Fallback and opt-in

- **Opt-in, not default.** The profile's Träwelling card gets two connect paths:
  "Connect" (as today, no webhook params) and "Connect with live sync" (adds
  `trwl_webhook_url` + `trwl_webhook_events=checkin_create,checkin_update,checkin_delete`).
  Existing connected users keep working untouched — they're sweep-fed until they choose
  to reconnect with live sync. Nothing breaks for users who never reauth.
- Users without a webhook lose nothing they have today: the unimported list is
  inbox-backed and the sweep + refresh button keep it current; the only difference is
  latency (sweep cadence vs. seconds).
- **Server config gates the feature**: `Traewelling:WebhookUrl` in appsettings. Unset
  (e.g. development — the authorize URL must exactly match the dashboard-registered
  production URL, so dev can't create webhooks) → the "live sync" button is hidden and
  the receiver endpoint 404s. No per-environment OAuth juggling.
- Disconnect: best-effort `DELETE /webhooks/{id}` upstream, then clear the local
  webhook columns alongside the tokens (as today).

New `User` columns: `TraewellingWebhookId` (long?), `TraewellingWebhookSecret`
(string?), `TraewellingWebhookCreatedAt` (datetime?).

## 5. The receiver endpoint

`POST /api/traewelling/webhook` — anonymous (no JWT), hardened:

1. Read the raw body (needed byte-exact for the HMAC).
2. `X-Trwl-Webhook-Id` → look up the user by `TraewellingWebhookId` (indexed). Unknown id → 401.
3. Compute HMAC-SHA256(rawBody, storedSecret), hex; constant-time compare
   (`CryptographicOperations.FixedTimeEquals`) with the `Signature` header. Mismatch → 401.
4. **After the signature verifies, always return 200** — processing errors are logged
   and swallowed. Träwelling counts non-2xx as delivery failures and auto-disables the
   webhook after a few; a parsing bug on our side must not kill the subscription.
   The sweep heals whatever a dropped event missed.
5. Apply the §2 state machine. Volume is single-user-scale; processing is inline, no queue.
6. Request size cap (StatusResource payloads are small; reject > ~1 MB).

## 6. Phasing

1. **Inbox + sweep** — schema migration, sweep service, unimported list reads from the
   inbox. Pure improvement on its own (instant list, minimal API usage), no webhook yet,
   benefits every user immediately.
2. **Webhook** — receiver endpoint, "connect with live sync" OAuth variant, secret
   storage, health check in connection status.
3. **Conflict UX** — `ChangedAfterImport` / `DeletedUpstream` sections and actions on
   the unimported page.
4. **(Optional) Telegram ping** on new `Pending` webhook rows via the existing bot.

Each phase ships independently; 1 and 3 work without 2 for sweep-only users.

## Open choices (defaults chosen, flag if you disagree)

- `ChangedAfterImport` never auto-applies, not even time-only corrections. (Safest;
  "Apply times" is one click.)
- Upstream deletes never delete OVDB data.
- Sweep cadence: daily + on-page-open when >1 h stale + manual button.
- `notification` events not subscribed.
