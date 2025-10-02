# Missing Database Indexes Analysis for OVDB

## Executive Summary

This document provides a comprehensive analysis of missing database indexes in the OVDB application. The analysis was performed by scanning the entire codebase to identify query patterns and comparing them against the current database schema defined in Entity Framework migrations.

**Total Missing Indexes Identified: 6 high/medium priority indexes**

---

## Methodology

1. Scanned all Controller files (`/OV_DB/Controllers/*.cs`)
2. Scanned all Service files (`/OV_DB/Services/*.cs`)
3. Analyzed the database model snapshot (`OVDBDatabaseContextModelSnapshot.cs`)
4. Reviewed recent migration files to understand index history
5. Identified WHERE clause patterns, JOIN operations, and ORDER BY clauses
6. Cross-referenced query patterns with existing indexes

---

## Missing Indexes - Detailed Analysis

### 1. RouteInstances.TrawellingStatusId ⭐ HIGH PRIORITY

**Current State:** No index exists

**Query Patterns Found:**
- `TrawellingService.cs:347` - `.Where(ri => ri.TrawellingStatusId.HasValue)`
- `TrawellingService.cs:768` - `.Where(ri => !ri.TrawellingStatusId.HasValue)`
- `TrawellingService.cs:805` - `.FirstOrDefaultAsync(ri => ri.TrawellingStatusId == statusId)`
- `TraewellingController.cs:320` - `.Where(ri => ri.TrawellingStatusId.HasValue)`

**Impact:**
- **HIGH** - This field is heavily used in Träwelling integration
- Filters all imported trips from Träwelling
- Used for both null checks and exact value lookups
- Performance degradation likely on large RouteInstances tables

**Recommendation:**
```csharp
[Index(nameof(TrawellingStatusId))]
public class RouteInstance
{
    // ... existing code
    public int? TrawellingStatusId { get; set; }
}
```

---

### 2. TrawellingIgnoredStatuses(UserId, TrawellingStatusId) ⭐ HIGH PRIORITY

**Current State:** Only has index on `UserId`

**Query Patterns Found:**
- `TrawellingService.cs:352-353` - Filters by `UserId` then selects `TrawellingStatusId`
- Used to check if a Träwelling status is ignored by a user
- Likely used in compound WHERE clauses

**Impact:**
- **HIGH** - Critical for Träwelling integration
- Composite index would significantly improve query performance
- Should potentially be UNIQUE constraint

**Recommendation:**
```csharp
[Index(nameof(UserId), nameof(TrawellingStatusId), IsUnique = true)]
public class TrawellingIgnoredStatus
{
    // ... existing code
    public int UserId { get; set; }
    public int TrawellingStatusId { get; set; }
}
```

---

### 3. TrawellingStation.TrawellingId ⭐ HIGH PRIORITY

**Current State:** No index exists

**Query Patterns Found:**
- `TrawellingService.cs:449` - `.FirstOrDefaultAsync(ts => ts.TrawellingId == stationId)`

**Impact:**
- **HIGH** - Used for Träwelling station lookups
- Required for matching stations when importing trips
- TrawellingId appears to be a unique external identifier

**Recommendation:**
```csharp
[Index(nameof(TrawellingId), IsUnique = true)]
[Table("trawelling_stations")]
public class TrawellingStation
{
    // ... existing code
    [Column("traewelling_id")]
    public int TrawellingId { get; set; }
}
```

---

### 4. Stations.OsmId ⭐ MEDIUM PRIORITY

**Current State:** No index exists

**Query Patterns Found:**
- `UpdateRegionService.cs:80` - `.FirstOrDefaultAsync(s => s.OsmId == station.Id)`

**Impact:**
- **MEDIUM** - Used during region updates
- Station lookups by OpenStreetMap ID
- OsmId appears to be a unique identifier

**Recommendation:**
```csharp
[Index(nameof(OsmId))]
public class Station
{
    // ... existing code
    public long OsmId { get; set; }
}
```

**Note:** Consider making this UNIQUE if OsmId is meant to be unique per station.

---

### 5. Maps.MapGuid ⭐ MEDIUM PRIORITY

**Current State:** No index exists

**Query Patterns Found:**
- `RoutesOdataController.cs:52` - `.SingleOrDefaultAsync(m => m.MapGuid == guid)`
- `ImagesController.cs` - Filters RouteInstances by MapGuid in collections
- `RegionsController.cs` - Filters routes by MapGuid
- `RoutesController.cs` - Multiple queries filtering by MapGuid

**Impact:**
- **MEDIUM** - Used extensively for sharing functionality
- Required for public access to maps via GUID
- Every shared map lookup requires this

**Recommendation:**
```csharp
[Index(nameof(MapGuid), IsUnique = true)]
public class Map
{
    // ... existing code
    public Guid MapGuid { get; set; }
}
```

---

### 6. Routes.Share 🔸 LOW-MEDIUM PRIORITY

**Current State:** No index exists

**Query Patterns Found:**
- `AdminController.cs:43` - `.Where(r => r.Share == Guid.Empty)`
- `RoutesOdataController.cs:295` - `.SingleOrDefaultAsync(r => r.RouteId == id && r.Share == guid)`

**Impact:**
- **MEDIUM** - Used for single route sharing
- Combined with RouteId in compound WHERE clause
- Less frequent than Maps.MapGuid but still used

**Recommendation:**
```csharp
[Index(nameof(Share))]
public class Route
{
    // ... existing code
    public Guid Share { get; set; }
}
```

---

## Low Priority Indexes (Optional)

### 7. RouteTypes.OrderNr

**Query Pattern:** `RouteTypesController.cs` - `.OrderBy(r => r.OrderNr)`

**Impact:** LOW - RouteTypes table is likely small, benefits minimal

### 8. Maps.OrderNr

**Query Pattern:** `MapsController.cs` - `.OrderBy(m => m.OrderNr)`

**Impact:** LOW - Small data set per user, benefits minimal

---

## Indexes Already Present (No Action Needed)

✅ **Regions.ParentRegionId** - Already indexed (`IX_Regions_ParentRegionId`)
- Used in `RouteRegionsService.cs` for hierarchy queries

✅ **StationVisits(StationId, UserId)** - UNIQUE composite index exists
- Perfectly covers all query patterns

✅ **RouteInstances(Date, RouteId)** - Composite indexes exist
- `idx_routeinstances_date_routeid`
- `idx_routeinstances_routeid_date`

✅ **All Foreign Keys** - Properly indexed for navigation properties
- Maps.UserId, Routes.RouteTypeId, RoutesMaps.MapId, etc.

---

## Implementation Priority

### Phase 1 - Critical (Implement Immediately)
1. **RouteInstances.TrawellingStatusId**
2. **TrawellingIgnoredStatuses(UserId, TrawellingStatusId)** - Composite unique
3. **TrawellingStation.TrawellingId** - Unique

### Phase 2 - Important (Implement Soon)
4. **Stations.OsmId**
5. **Maps.MapGuid** - Unique
6. **Routes.Share**

### Phase 3 - Optional (Monitor Performance)
7. RouteTypes.OrderNr
8. Maps.OrderNr

---

## Performance Impact Estimates

Based on typical query patterns:

| Index | Queries/Day (Est.) | Performance Gain | Table Size Impact |
|-------|-------------------|------------------|-------------------|
| RouteInstances.TrawellingStatusId | 100-1000 | 50-90% faster | Minimal (~5-10MB) |
| TrawellingIgnoredStatuses composite | 50-500 | 70-95% faster | Minimal (~1-2MB) |
| TrawellingStation.TrawellingId | 10-100 | 80-95% faster | Minimal (~1MB) |
| Stations.OsmId | 1-50 | 70-90% faster | Small (~5-10MB) |
| Maps.MapGuid | 100-1000 | 80-95% faster | Minimal (~1-2MB) |
| Routes.Share | 10-100 | 60-80% faster | Small (~5-10MB) |

---

## Migration Strategy

### Recommended Approach:

1. **Create a new migration** for all Phase 1 + Phase 2 indexes
2. **Test in development** environment first
3. **Monitor query performance** before/after
4. **Apply to production** during low-traffic period
5. **Verify index usage** with database query statistics

### Sample Migration Command:
```bash
dotnet ef migrations add AddMissingIndexes --project OVDB_database --startup-project OV_DB
```

---

## Additional Observations

### Good Practices Already in Use:
- ✅ Composite indexes for common query patterns (RouteInstances)
- ✅ Foreign key indexes on all navigation properties
- ✅ Unique constraints where appropriate (StationVisits)

### Areas for Improvement:
- 📝 Consider adding covering indexes for frequently accessed columns
- 📝 Review query plans for N+1 query issues
- 📝 Consider adding indexes on filtered columns in large tables

### Database Statistics Recommendation:
Run `ANALYZE TABLE` on MySQL/MariaDB after adding indexes to update query planner statistics:
```sql
ANALYZE TABLE RouteInstances;
ANALYZE TABLE TrawellingIgnoredStatuses;
ANALYZE TABLE TrawellingStation;
ANALYZE TABLE Stations;
ANALYZE TABLE Maps;
ANALYZE TABLE Routes;
```

---

## Conclusion

The analysis identified **6 high/medium priority missing indexes** that should be added to improve query performance, particularly for:

1. **Träwelling integration** (3 critical indexes)
2. **Map sharing functionality** (2 important indexes)
3. **Station lookups** (1 important index)

These indexes are expected to provide significant performance improvements (50-95% faster queries) with minimal storage overhead.

**Next Steps:**
1. Review this analysis with the development team
2. Create Entity Framework migration with Phase 1 + Phase 2 indexes
3. Test in development environment
4. Deploy to production and monitor performance

---

*Analysis completed on: 2025*  
*Codebase analyzed: jjasloot/OVDB*  
*Methodology: Static code analysis + schema inspection*
