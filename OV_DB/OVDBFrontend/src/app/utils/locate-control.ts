import { inject } from "@angular/core";
import { MatSnackBar } from "@angular/material/snack-bar";
import { TranslateService } from "@ngx-translate/core";
import { CircleMarker, Control, DomEvent, DomUtil, LocationEvent, Map as LeafletMap, circleMarker } from "leaflet";

/**
 * A "my location" button under the zoom buttons: centres the map on the device's position and marks
 * it with a dot. Location is only requested when the button is pressed, never on load.
 */
export function addLocateControl(map: LeafletMap, label: string, onError: () => void): void {
  let marker: CircleMarker | null = null;

  const control = new Control({ position: "topleft" });
  control.onAdd = () => {
    const container = DomUtil.create("div", "leaflet-bar leaflet-control");
    const button = DomUtil.create("a", "locate-button", container) as HTMLAnchorElement;
    button.href = "#";
    button.title = label;
    button.setAttribute("role", "button");
    button.setAttribute("aria-label", label);
    const icon = DomUtil.create("span", "material-icons", button);
    icon.textContent = "my_location";
    DomEvent.disableClickPropagation(container);
    DomEvent.on(button, "click", (event) => {
      DomEvent.preventDefault(event);
      map.locate({ setView: true, maxZoom: 14 });
    });
    return container;
  };
  control.addTo(map);

  map.on("locationfound", (event: LocationEvent) => {
    marker?.remove();
    marker = circleMarker(event.latlng, {
      radius: 8,
      color: "#ffffff",
      weight: 2,
      fillColor: "#1a73e8",
      fillOpacity: 1,
    }).addTo(map);
  });
  map.on("locationerror", onError);
}

/**
 * For components: call in an injection context and bind the result to (leafletMapReady).
 * Labels and the "location unavailable" message come from the translations.
 */
export function injectLocateControl(): (map: LeafletMap) => void {
  const translate = inject(TranslateService);
  const snackBar = inject(MatSnackBar);
  return (map) =>
    addLocateControl(map, translate.instant("MAP.MY_LOCATION"), () =>
      snackBar.open(translate.instant("MAP.LOCATION_UNAVAILABLE"), undefined, { duration: 4000 })
    );
}
