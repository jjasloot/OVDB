# NetworkLab

Standalone lab for the region rail network completion design (`docs/region-network-completion.md`):
proves the Overpass assembly (junction detection, chain-walk merge, clipping, chunking) and the
route-to-edge matching against real data before anything lands in the app. Not part of the solution
and never touches the app or its tables — it only **reads** regions and routes from the local dev
database (`dev-db/restore.ps1`, port 3307).

```powershell
dotnet run -- regions Nederland     # find the region id
dotnet run -- run 123               # fetch (cached!) + assemble + match + report + geojson export
```

Overpass is queried **once per region**; the raw response is cached in `data/overpass-region-{id}.json`
and every later run reuses it. Delete the file to refetch.

Matching runs at 10/15/20/30 m tolerance simultaneously (each edge stores its best covered fraction
per tolerance), so the tolerance question is answered by comparing one run's outputs. Open
`viewer.html` in a browser and drag the exported `data/network-*.geojson` files onto it to eyeball
covered/uncovered per tolerance.

Edges are flagged `passenger` (OSM `usage` ∉ freight/industrial/military/test; service tracks are
excluded at fetch time). The percentage counts passenger km only; covered non-passenger edges are
reported separately as bonus km.
