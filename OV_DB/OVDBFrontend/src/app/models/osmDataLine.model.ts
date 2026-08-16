import { OSMStop } from "./stationView.model";

export interface OSMDataLine {
    id: number;
    name: string;
    description: string;
    network: string;
    operator: string;
    from: string;
    to: string;
    potentialErrors: string;
    geoJson: any;
    ref: string;
    colour: string;
    /**
     * Stations the relation calls at along the imported section, from its stop/platform members.
     * Round-trips back to the server on import, so suggesting stations costs no extra OSM request.
     */
    stops?: OSMStop[];
}
