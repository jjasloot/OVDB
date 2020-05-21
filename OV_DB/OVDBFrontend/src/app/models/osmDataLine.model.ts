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
}