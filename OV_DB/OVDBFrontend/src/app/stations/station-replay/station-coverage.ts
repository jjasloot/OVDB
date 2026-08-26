/**
 * The geometry behind the coverage circles, kept clear of Angular and Leaflet so it can be reasoned
 * about — and checked — on its own.
 */

const METRES_PER_DEGREE_LAT = 111_320;

/**
 * Grid cell for the nearest-neighbour searches, in metres. Big enough that a dense country needs
 * one or two rings, small enough that a remote station does not scan the continent.
 */
const COVERAGE_CELL_M = 20_000;

/** A station reduced to what the replay needs, with its date already parsed. */
export interface ReplayStation {
  id: number;
  name: string;
  lat: number;
  lon: number;
  /**
   * When this station entered the set: a timestamp, `-Infinity` for a visit with no date, and
   * `Infinity` for one that never happens at this level. Undated visits count from the start
   * rather than never — they did happen, and treating them as unvisited would punch permanent
   * holes in the coverage that the data does not actually claim.
   */
  at: number;
}

/**
 * How one station's coverage radius grows over the replay, as a step function: `radii[j]` is the
 * radius from `thresholds[j]` until `thresholds[j + 1]`.
 *
 * A radius only ever grows — the set of stations you have not visited only shrinks — and it changes
 * only when the station currently capping it gets visited. So the whole history of a circle is a
 * short list of steps, worked out once, instead of a nearest-neighbour search on every frame.
 */
export interface CoverageSteps {
  thresholds: number[];
  radii: number[];
}

/** Equirectangular, using the pair's own mean latitude. Well under a percent out at any distance
 * two neighbouring stations are apart, and it costs a cosine rather than a haversine. */
export function metresBetween(a: { lat: number; lon: number }, b: { lat: number; lon: number }): number {
  const meanLat = (((a.lat + b.lat) / 2) * Math.PI) / 180;
  const dLat = (a.lat - b.lat) * METRES_PER_DEGREE_LAT;
  const dLon = (a.lon - b.lon) * METRES_PER_DEGREE_LAT * Math.cos(meanLat);
  return Math.hypot(dLat, dLon);
}

/** The radius the step function gives at a moment, or 0 before the first step. */
export function radiusAt(steps: CoverageSteps | undefined, at: number): number {
  if (!steps || steps.thresholds.length === 0) {
    return 0;
  }
  // Few steps per station, so a walk from the end beats the ceremony of a binary search.
  for (let j = steps.thresholds.length - 1; j >= 0; j--) {
    if (steps.thresholds[j] <= at) {
      return steps.radii[j];
    }
  }
  return 0;
}

/**
 * The radius of every circle, for every moment, in one pass.
 *
 * A circle around a visited station may reach exactly as far as the nearest station that has not
 * been visited yet — one metre further and it would contain a station that has not been visited,
 * which is the one thing it promises not to do. Nothing caps it beyond that: a lone station whose
 * nearest unvisited neighbour is 200 km away really does own 200 km of emptiness.
 *
 * Rather than redo that search on every frame, each station's neighbours are walked outwards once.
 * Reading them in order of distance, a neighbour only matters if it was visited later than every
 * neighbour before it — those are the moments the circle is free to jump out to the next one. The
 * walk ends at the first neighbour that is never visited at all, which fixes the circle's final
 * size. That is a handful of steps per station, and the whole animation is then a lookup.
 */
export function buildCoverage(
  all: ReplayStation[],
  visited: ReplayStation[]
): Map<number, CoverageSteps> {
  const coverage = new Map<number, CoverageSteps>();
  if (all.length === 0 || visited.length === 0) {
    return coverage;
  }

  // A grid needs one flat metric, so longitude is scaled at the highest latitude in the data. That
  // understates real distances everywhere else, which is the safe direction: the search can only
  // end up looking at more cells than it strictly had to, never at fewer.
  const maxAbsLat = Math.min(Math.max(...all.map((s) => Math.abs(s.lat))), 89);
  const metresPerDegreeLon = METRES_PER_DEGREE_LAT * Math.cos((maxAbsLat * Math.PI) / 180);
  const x = (station: ReplayStation) => (station.lon * metresPerDegreeLon) / COVERAGE_CELL_M;
  const y = (station: ReplayStation) => (station.lat * METRES_PER_DEGREE_LAT) / COVERAGE_CELL_M;

  const grid = new Map<string, ReplayStation[]>();
  const key = (row: number, col: number) => row + '|' + col;
  let minRow = Infinity;
  let maxRow = -Infinity;
  let minCol = Infinity;
  let maxCol = -Infinity;
  for (const station of all) {
    const row = Math.floor(y(station));
    const col = Math.floor(x(station));
    minRow = Math.min(minRow, row);
    maxRow = Math.max(maxRow, row);
    minCol = Math.min(minCol, col);
    maxCol = Math.max(maxCol, col);
    const cell = grid.get(key(row, col));
    if (cell) {
      cell.push(station);
    } else {
      grid.set(key(row, col), [station]);
    }
  }
  // Beyond this the rings have left the data behind, which only happens on a map with no unvisited
  // station left anywhere — every circle is then as large as the map itself.
  const maxRing = Math.max(maxRow - minRow, maxCol - minCol) + 1;

  for (const from of visited) {
    const originRow = Math.floor(y(from));
    const originCol = Math.floor(x(from));
    const neighbours: { at: number; distance: number }[] = [];
    let nearestNever = Infinity;

    for (let ring = 0; ring <= maxRing; ring++) {
      for (let row = originRow - ring; row <= originRow + ring; row++) {
        for (let col = originCol - ring; col <= originCol + ring; col++) {
          // Only the newly added edge of the box; the inside was covered by earlier rings.
          if (ring > 0 && Math.abs(row - originRow) !== ring && Math.abs(col - originCol) !== ring) {
            continue;
          }
          const cell = grid.get(key(row, col));
          if (!cell) {
            continue;
          }
          for (const other of cell) {
            if (other.id === from.id) {
              continue;
            }
            const distance = metresBetween(from, other);
            neighbours.push({ at: other.at, distance });
            if (other.at === Infinity && distance < nearestNever) {
              nearestNever = distance;
            }
          }
        }
      }
      // Everything closer than `ring` cells has now been seen, so once a never-visited station is
      // that close, no unseen station can be closer and the walk has all it needs.
      if (nearestNever <= ring * COVERAGE_CELL_M) {
        break;
      }
    }

    neighbours.sort((a, b) => a.distance - b.distance);
    const thresholds: number[] = [];
    const radii: number[] = [];
    let visitedBy = -Infinity;
    for (const neighbour of neighbours) {
      if (neighbour.at <= visitedBy) {
        continue; // already inside the circle by the time it could have blocked it
      }
      thresholds.push(visitedBy);
      radii.push(neighbour.distance);
      if (neighbour.at === Infinity) {
        break; // never visited: this is the circle's final size
      }
      visitedBy = neighbour.at;
    }
    coverage.set(from.id, { thresholds, radii });
  }

  return coverage;
}
