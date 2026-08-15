# Spatial indexes (manual step)

`Regions.Geometry` and `Routes.LineString` are not spatially indexed, so
`ST_Intersects` predicates against them are table scans. MySQL/MariaDB only allow a
`SPATIAL` index on a `NOT NULL` column with a declared SRID, and both columns are
currently nullable. This cannot be applied automatically on startup: if any existing
row has a null geometry, the `NOT NULL` conversion fails, and because migrations
auto-apply on boot that would leave a broken migration state.

Apply this manually after verifying there are no null geometries, and back up first.

```sql
-- 1. Check for nulls (must both return 0 before continuing)
SELECT COUNT(*) FROM Regions WHERE Geometry IS NULL;
SELECT COUNT(*) FROM Routes  WHERE LineString IS NULL;

-- 2. Ensure an SRID is set (WGS84 = 4326) and make the columns NOT NULL
ALTER TABLE Regions MODIFY Geometry  MULTIPOLYGON NOT NULL /*!80003 SRID 4326 */;
ALTER TABLE Routes  MODIFY LineString LINESTRING   NOT NULL /*!80003 SRID 4326 */;

-- 3. Create the spatial indexes
CREATE SPATIAL INDEX IX_Regions_Geometry  ON Regions (Geometry);
CREATE SPATIAL INDEX IX_Routes_LineString ON Routes  (LineString);

-- 4. Refresh planner statistics
ANALYZE TABLE Regions, Routes;
```

Note: after the region/station assignment refactor (loading the hierarchy once with
prepared geometries), the per-route/per-station spatial queries no longer run inside
the hot batch loops, so these indexes now mainly benefit ad-hoc single-station lookups
and any future spatial queries.
