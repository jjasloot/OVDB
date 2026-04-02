import type { MarkerClusterGroup, MarkerClusterGroupOptions } from "leaflet";

type LeafletWithMarkerCluster = typeof import("leaflet") & {
  markerClusterGroup?: (options?: MarkerClusterGroupOptions) => MarkerClusterGroup;
  MarkerClusterGroup?: new (options?: MarkerClusterGroupOptions) => MarkerClusterGroup;
};

let leafletMarkerClusterPromise: Promise<LeafletWithMarkerCluster> | undefined;

export async function loadLeafletMarkerCluster(): Promise<LeafletWithMarkerCluster> {
  if (!leafletMarkerClusterPromise) {
    leafletMarkerClusterPromise = (async () => {
      const leaflet = (await import("leaflet")) as LeafletWithMarkerCluster;
      (window as Window & typeof globalThis & { L?: LeafletWithMarkerCluster }).L = leaflet;
      await import("leaflet.markercluster");

      if (!leaflet.markerClusterGroup && !leaflet.MarkerClusterGroup) {
        throw new Error("Leaflet.markercluster failed to initialize.");
      }

      return leaflet;
    })();
  }

  return leafletMarkerClusterPromise;
}

export async function createMarkerClusterGroup(
  options: MarkerClusterGroupOptions
): Promise<MarkerClusterGroup> {
  const leaflet = await loadLeafletMarkerCluster();

  if (leaflet.markerClusterGroup) {
    return leaflet.markerClusterGroup(options);
  }

  if (leaflet.MarkerClusterGroup) {
    return new leaflet.MarkerClusterGroup(options);
  }

  throw new Error("Leaflet.markercluster did not expose a cluster group constructor.");
}
