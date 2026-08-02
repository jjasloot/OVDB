import { Component, inject } from "@angular/core";
import { LeafletModule } from "@bluehalo/ngx-leaflet";
import { LatLng, LatLngBounds, Layer, latLngBounds, polyline } from "leaflet";
import { RouterLink } from "@angular/router";
import { TranslateModule } from "@ngx-translate/core";
import { MapTileLayersService } from "src/app/services/map-tile-layers.service";
import { environment } from "src/environments/environment";

interface SampleLine {
  colour: string;
  points: [number, number][];
}

/**
 * Illustrative network for the landing page — an example of what a map looks
 * like, not anyone's travel history. Coarse city-to-city lines, drawn in the
 * default route type colours (train, bus, ferry).
 *
 * If environment.demoMapSharingLink is set, the landing page links to that real
 * shared map instead of relying on this.
 */
const SAMPLE_LINES: SampleLine[] = [
  // Train — Amsterdam / Utrecht / Arnhem
  {
    colour: "#0074d9",
    points: [
      [52.3791, 4.9003],
      [52.2967, 4.9519],
      [52.0894, 5.11],
      [52.0327, 5.654],
      [51.9851, 5.8987],
    ],
  },
  // Train — Den Haag / Leiden / Haarlem / Amsterdam
  {
    colour: "#0074d9",
    points: [
      [52.0809, 4.3247],
      [52.1663, 4.4818],
      [52.388, 4.6383],
      [52.3791, 4.9003],
    ],
  },
  // Train — Utrecht / Den Bosch / Eindhoven
  {
    colour: "#0074d9",
    points: [
      [52.0894, 5.11],
      [51.6906, 5.2933],
      [51.4433, 5.4797],
    ],
  },
  // Train — Rotterdam / Breda / Tilburg / Den Bosch
  {
    colour: "#0074d9",
    points: [
      [51.9245, 4.4699],
      [51.5955, 4.78],
      [51.5606, 5.0919],
      [51.6906, 5.2933],
    ],
  },
  // Train — Utrecht / Amersfoort / Zwolle
  {
    colour: "#0074d9",
    points: [
      [52.0894, 5.11],
      [52.1533, 5.3736],
      [52.5049, 6.0913],
    ],
  },
  // Bus — Zwolle / Kampen / Emmeloord
  {
    colour: "#01ff70",
    points: [
      [52.5049, 6.0913],
      [52.5555, 5.9111],
      [52.6098, 5.8203],
      [52.7107, 5.748],
    ],
  },
  // Ferry — Harlingen / Terschelling
  {
    colour: "#f012be",
    points: [
      [53.1747, 5.4097],
      [53.2686, 5.3244],
      [53.3617, 5.2192],
    ],
  },
];

@Component({
  selector: "app-demo-map",
  standalone: true,
  imports: [LeafletModule, RouterLink, TranslateModule],
  templateUrl: "./demo-map.component.html",
  styleUrls: ["./demo-map.component.scss"],
})
export class DemoMapComponent {
  private mapTileLayersService = inject(MapTileLayersService);

  readonly sharedMapLink = environment.demoMapSharingLink;

  layers: Layer[] = SAMPLE_LINES.map((line) =>
    polyline(
      line.points.map(([lat, lng]) => new LatLng(lat, lng)),
      { color: line.colour, weight: 5, opacity: 0.9 }
    )
  );

  bounds: LatLngBounds = latLngBounds(
    SAMPLE_LINES.reduce<LatLng[]>(
      (all, line) => all.concat(line.points.map(([lat, lng]) => new LatLng(lat, lng))),
      []
    )
  );

  options = {
    layers: [this.mapTileLayersService.createThemedLayer(0.6)],
    zoom: 7,
    // Let the page keep scrolling when the pointer crosses the map.
    scrollWheelZoom: false,
  };
}
