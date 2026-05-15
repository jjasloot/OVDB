# OVDB Bug Analysis Report

This document contains a comprehensive analysis of bugs found throughout the application, with focus on the user interface (Angular frontend) and supporting backend services.

---

## Table of Contents

1. [Critical Frontend Bugs](#critical-frontend-bugs)
2. [UI Component Bugs](#ui-component-bugs)
3. [Service & Guard Bugs](#service--guard-bugs)
4. [Backend Bugs](#backend-bugs)
5. [Summary](#summary)

---

## Critical Frontend Bugs

### 1. Infinite Recursion in Map Component Bounds Setter

**Files:** `map.component.ts`, `single-route-map.component.ts`

The `bounds` setter calls itself recursively when the value is invalid:

```typescript
set bounds(value: LatLngBounds) {
  if (!!value && value.isValid()) {
    this._bounds = value;
  } else {
    this.bounds = new LatLngBounds(...); // Calls setter recursively instead of this._bounds
  }
}
```

**Impact:** Stack overflow if the fallback `LatLngBounds` is also invalid.  
**Fix:** Change `this.bounds = ...` to `this._bounds = ...`.

---

### 2. Copy-Paste Logic Error in Map Query Parameter Parsing

**File:** `map.component.ts` (line ~246)

```typescript
if (queryParams.has("from")) {
  this.from = moment(+queryParams.get("from"));
}
if (queryParams.has("from")) {  // BUG: should be "to"
  this.to = moment(+queryParams.get("to"));
}
```

**Impact:** The `to` parameter is only parsed when `from` exists, not when `to` exists.  
**Fix:** Change second condition to `queryParams.has("to")`.

---

### 3. Double `+=` Syntax Error in Map Filter Construction

**File:** `map.component.ts` (line ~430)

```typescript
filter += filter += ...
```

**Impact:** Produces incorrect filter string by doubling the concatenation.  
**Fix:** Remove one `+=`.

---

### 4. Null Comparison Bug in Map Filter Cache Check

**File:** `map.component.ts` (line ~569)

```typescript
(value.to?.isSame(to) ?? (value.to == null && from == null))
```

**Impact:** The fallback checks `from == null` when it should check `to == null`. The cache may return stale data.  
**Fix:** Change `from == null` to `to == null`.

---

### 5. Auth Interceptor Sends "Bearer null" Header

**File:** `auth.interceptor.ts` (line ~19)

```typescript
Authorization: `Bearer ${this.authService.token}`
```

**Impact:** After logout or when token is null, the header becomes `Bearer null`, causing malformed requests to the backend.  
**Fix:** Only add the Authorization header when token is not null/empty.

---

### 6. SignalR Service Allows Multiple Concurrent Connections

**File:** `signal-r.service.ts` (lines 16-37)

The `connected` flag is set only after the async `.start()` completes. Multiple rapid `connect()` calls before the first resolves create duplicate connections and accumulated event listeners.

**Impact:** Duplicate event handling, memory leaks, multiple WebSocket connections.  
**Fix:** Set a "connecting" state immediately, or use a promise-based guard.

---

## UI Component Bugs

### Login & Registration

| Bug | File | Description |
|-----|------|-------------|
| Missing unsubscribe | `login.component.ts` | `ActivatedRoute.data` subscription never cleaned up |
| Missing unsubscribe | `login.component.ts` | `authService.login()` subscription not managed |
| Empty success handler | `registration.component.ts` | Registration success does nothing - no redirect or message |
| Missing unsubscribe | `registration.component.ts` | Registration subscription not managed |

### Home Component

| Bug | File | Description |
|-----|------|-------------|
| Incorrect signal handling | `home.component.html` | `@if (!!maps() \|\| !!stationMaps())` - `!!` on signals doesn't check array length |
| Missing unsubscribe | `home.component.ts` | `apiService.getMaps()` and `apiService.listStationMaps()` subscriptions unmanaged |
| Non-atomic loading counter | `home.component.ts` | Loading counter incremented/decremented without synchronization |

### Profile Component

| Bug | File | Description |
|-----|------|-------------|
| Memory leak via setInterval | `profile.component.ts` | Polling interval for Träwelling OAuth window not cleaned up on destroy |
| Null window.open result | `profile.component.ts` | `window.open()` can return null (popup blocked), polling continues anyway |
| Multiple missing unsubscribes | `profile.component.ts` | 10+ subscriptions without `takeUntilDestroyed()` |

### Routes List Component

| Bug | File | Description |
|-----|------|-------------|
| Missing null check on ViewChild | `routes-list.component.ts` | `this.input().nativeElement` accessed without null check in `ngAfterViewInit` |
| Missing null check on sort/paginator | `routes-list.component.ts` | `this.sort().sortChange` and `this.paginator().page` accessed without existence check |
| Null operatorIds in template | `routes-list.component.html` | `@for (operatorId of element.operatorIds)` - array could be null |
| Missing routeType check | `routes-list.component.html` | `name(element.routeType)` called when only `routeTypeColour` is checked |

### Route Detail Component

| Bug | File | Description |
|-----|------|-------------|
| Unsafe non-null assertions | `route-detail.component.ts` | Multiple `this.route()!.routeId` without prior null checks |
| Signal vs property confusion | `route-detail.component.ts` | `!this.countries` should be `!this.countries()` (signal not invoked) |
| Same issue for maps | `route-detail.component.ts` | `!this.maps` should be `!this.maps()` |

### Route Instances List Component

| Bug | File | Description |
|-----|------|-------------|
| Missing null check on input | `route-instances-list.component.ts` | `fromEvent(this.input().nativeElement, "keyup")` without null guard |
| Null paginator access | `route-instances-list.component.ts` | `this.paginator().pageIndex = 0` without null check |
| Return type mismatch | `route-instances-list.component.ts` | `formatDelay()` can return `null` but type says `string` |
| Unsafe date pipe | `route-instances-list.component.html` | Date pipes applied to potentially null `startTime`/`endTime` |

### Route Instances Edit Component

| Bug | File | Description |
|-----|------|-------------|
| Two-way binding conflict | `route-instances-edit.component.html` | `[(ngModel)]` and `(selectionChange)` both modify same signal |
| Inverted disabled logic | `route-instances-edit.component.html` | `[disabled]="canAddNewRow"` should be `[disabled]="!canAddNewRow"` |
| Missing null check | `route-instances-edit.component.ts` | `this.table()` could be null before calling `renderRows()` |
| Unsafe Moment check | `route-instances-edit.component.ts` | `this.instance.date['_isAMomentObject']` throws if date is undefined |

### Route Add Component

| Bug | File | Description |
|-----|------|-------------|
| Missing unsubscribe | `route-add.component.ts` | `this.route.queryParams.subscribe()` without cleanup |
| Missing error handling | `route-add.component.ts` | `apiService.postFiles()` has no error handler |
| Null FileList assignment | `route-add.component.ts` | `this.fileToUpload = null` but type is `FileList` (not nullable) |
| Null files from event | `route-add.component.html` | `$event.target.files` could be null |

### Map View / Map Filter Components

| Bug | File | Description |
|-----|------|-------------|
| Missing unsubscribe | `map-view.component.ts` | `activatedRoute.paramMap` subscription not cleaned up |
| Global component exposure | `map.component.ts` | `window["angularComponentRef"] = { component: this }` - security risk |
| Missing unsubscribe | `map-filter.component.ts` | `translationService.languageChanged` subscription leaks |
| Inconsistent selectedYears | `map-filter.component.ts` | Set to empty array instead of actual selected years when dates provided |

### Stats Components

| Bug | File | Description |
|-----|------|-------------|
| Missing unsubscribes | `time-stats.component.ts` | Multiple subscriptions without cleanup |
| Loading flag not reset on error | `time-stats.component.ts` | `loadingMap` stays true if API call fails |
| Null data properties | `time-stats.component.ts` | `data.latMin`, `data.latMax` etc. accessed without null checks |
| Missing unsubscribe | `region-stats.component.ts` | API subscription not cleaned up |

### Single Route Map Component

| Bug | File | Description |
|-----|------|-------------|
| Infinite recursion (same as map) | `single-route-map.component.ts` | Bounds setter calls itself |
| Unhandled promise rejection | `single-route-map.component.ts` | `toPromise()` rejection leaves component in undefined state |
| Missing null checks | `single-route-map.component.ts` | `feature.properties` and `layer.getPopup()` accessed without null guards |
| Missing unsubscribes | `single-route-map.component.ts` | Two subscriptions without cleanup |

### Countries Component

| Bug | File | Description |
|-----|------|-------------|
| Missing unsubscribe | `countries.component.ts` | `translationService.languageChanged` subscription leaks |
| Uninitialized array | `countries.component.ts` | `data: Country[]` used in template before initialization |
| Template null access | `countries.component.html` | `data.length` throws if `data` is undefined |

### Wizard Components

| Bug | File | Description |
|-----|------|-------------|
| Missing unsubscribes | `wizard-step1.component.ts` | `languageChanged` and `queryParams` subscriptions leak |
| Potential null reference | `wizard-step1.component.ts` | `trawellingTripData.tripId` accessed without null check |
| Missing unsubscribes | `wizard-step2.component.ts` | `activatedRoute.params` and `queryParamMap` subscriptions leak |
| Nested subscribes anti-pattern | `wizard-step2.component.ts` | Nested subscriptions instead of `switchMap`/`mergeMap` |

### Traewelling Component

| Bug | File | Description |
|-----|------|-------------|
| Async ngOnInit race condition | `traewelling.component.ts` | Component may be destroyed before promises resolve |
| Missing null check | `traewelling.component.html` | `connectionStatus?.user.displayName` - `user` itself could be null |

### Image Creator Component

| Bug | File | Description |
|-----|------|-------------|
| Uninitialized array | `image-creator.component.ts` | `maps: Map[]` used before API call completes |
| Missing error handling | `image-creator.component.ts` | `getMaps()` has no error handler |
| Template null crash | `image-creator.component.html` | `@for (map of maps)` throws if maps is undefined |

### Maps List Component

| Bug | File | Description |
|-----|------|-------------|
| Missing error handling | `maps-list.component.ts` | `getMaps()` subscription has no error handler, loading stays true |

---

## Service & Guard Bugs

### Authentication Service

| Bug | Description |
|-----|-------------|
| Type mismatch | `token` declared as `string` but assigned `null` on logout |
| Missing null check before token decode | `helper.decodeToken(this.token)` called without verifying token exists |
| String comparison for admin | `this.admin === 'true'` compares to string, fragile if backend changes |
| Race condition in token refresh | Multiple `setTimeout` calls with stale expiry values |
| Subscription memory leak | `refreshTheToken()` subscribes without cleanup |

### Auth Interceptor

| Bug | Description |
|-----|-------------|
| "Bearer null" header | Token null check missing before setting Authorization header |
| No 401 response handling | Expired tokens cause 401 errors without triggering logout/refresh |

### Login Guard

| Bug | Description |
|-----|-------------|
| Incorrect URL construction | `'/' + next.url.join('/')` creates `//` when url is empty |
| Race condition | `isLoggedIn` could change between check and return |

### Administrator Guard

| Bug | Description |
|-----|-------------|
| Missing null safety | `this.authService.admin` may throw if token is null |
| No error handling | Guard crashes instead of returning false on error |

### SignalR Service

| Bug | Description |
|-----|-------------|
| Multiple connections | Rapid `connect()` calls create duplicates before first resolves |
| Event listener accumulation | Each `connection.on()` adds listeners without removing previous ones |
| Connection overwrite leak | Previous connection overwritten without `.stop()` |
| Subjects never completed | `updates$`, `regionUpdates$`, `stationUpdates$` never call `.complete()` |

### Theme Service

| Bug | Description |
|-----|-------------|
| Null pointer | `document.querySelector('meta[name="theme-color"]')` may return null, `.setAttribute()` crashes |

### Translation Service

| Bug | Description |
|-----|-------------|
| EventEmitter leak | `languageChanged` subscribers never cleaned up |

---

## Backend Bugs

### Pervasive Pattern: Unsafe Claim Access (All Controllers)

Nearly every controller has this pattern repeated:

```csharp
User.Claims.SingleOrDefault(c => c.Type == ClaimTypes.NameIdentifier).Value
```

`SingleOrDefault` returns `null` when the claim doesn't exist, then `.Value` throws `NullReferenceException`.

**Affected Controllers:** AuthenticationController, RoutesController, RoutesOdataController, MapsController, StationController, AdminController, TraewellingController, StationMergeController (35+ locations)

**Fix:** Use `?.Value ?? ""` or extract into a helper method with proper null handling.

---

### AuthenticationController

| Bug | Line | Description |
|-----|------|-------------|
| Null reference | ~61 | Admin claim `.Value` access without null check |
| Missing validation | ~235 | No null check on `createAccount.Password` before `.Length` |
| Missing null check | ~49 | `loginRequest` parameter not validated |

### RoutesController

| Bug | Line | Description |
|-----|------|-------------|
| InvalidOperationException | ~241 | `.Single()` throws if no routes found |
| InvalidOperationException | ~367 | `.Single()` on `PostKmlToDatabase()` result throws if empty |
| Unsafe claim access | Multiple | Same pervasive pattern |

### RoutesOdataController

| Bug | Line | Description |
|-----|------|-------------|
| Null RouteType | ~268 | `route.RouteType` accessed without null check |
| Console.WriteLine in production | ~187 | Should use ILogger |

### MapsController

| Bug | Line | Description |
|-----|------|-------------|
| Null dbMap access | ~71 | `dbMap` not null-checked after database query |
| Missing input validation | ~73 | No length validation on `SharingLinkName` and `Name` |

### StationController

| Bug | Line | Description |
|-----|------|-------------|
| Null station access | ~110 | `station.Regions` accessed after `SingleOrDefaultAsync` without null check |
| Race condition | ~106-112 | Station fetched after `SaveChangesAsync` could be deleted by another user |

### AdminController

| Bug | Line | Description |
|-----|------|-------------|
| InvalidOperationException | ~320 | `.Single()` on parsed elements could throw if empty |
| Console.WriteLine in production | Multiple | Should use ILogger |

### ImporterController

| Bug | Line | Description |
|-----|------|-------------|
| Null reference | ~108 | `relation.Members` could be null |
| Null reference | ~193 | `wayOsm.FirstOrDefault()` result `.Nodes` accessed without null check |
| Null reference | ~199 | `osm.Elements.FirstOrDefault(...)` result accessed without null check |
| Null reference | ~209 | `relation.Tags` accessed but relation could be null |

### StationMergeController

| Bug | Line | Description |
|-----|------|-------------|
| Race condition | ~343-346 | Stations could be deleted between queries |

### TrawellingService

| Bug | Line | Description |
|-----|------|-------------|
| Race condition | ~79-89 | `_oauthStates` dictionary not consistently locked |
| Null deserialization | ~150, 265, 325 | `JsonConvert.DeserializeObject` results used without null checks |
| Thread safety | ~331, 407 | `_memoryCache.Set()` called without thread-safety consideration |
| Null navigation | ~412 | `statusesResponse.Links.Next` - `Links` could be null |

---

## Summary

### Bug Count by Category

| Category | Count |
|----------|-------|
| Memory leaks (missing unsubscribe) | 25+ |
| Null reference / null pointer errors | 50+ |
| Missing error handling | 15+ |
| Logic errors (incorrect conditions) | 8 |
| Race conditions | 8 |
| Type mismatches | 5 |
| Security concerns | 2 |
| Infinite recursion | 2 |
| **Total** | **~115+** |

### Most Critical Issues (Likely to Crash the App)

1. **Infinite recursion** in bounds setter (map & single-route-map components)
2. **"Bearer null" header** sent after logout (auth interceptor)
3. **Duplicate SignalR connections** causing double event handling
4. **Copy-paste bug** in map query parameter parsing (`"from"` checked twice instead of `"to"`)
5. **Double `+=`** in filter string construction
6. **Backend NullReferenceException** from unsafe claim access pattern (35+ locations)
7. **`Single()` instead of `SingleOrDefault()`** in backend causing unhandled exceptions

### Most Common Pattern

The single most pervasive issue is **missing subscription cleanup** in Angular components. Over 25 subscriptions across the application lack `takeUntilDestroyed()`, `unsubscribe()`, or similar cleanup, leading to memory leaks during navigation.

The second most common pattern is **unsafe null access** in the C# backend, where `SingleOrDefault()` results are accessed without null checks in 35+ locations across all controllers.
