import { Component, OnInit, inject, ChangeDetectionStrategy } from '@angular/core';
import { ActivatedRoute, ParamMap } from '@angular/router';
import { StationMapComponent } from '../station-map/station-map.component';

@Component({
    selector: 'app-station-map-view',
    templateUrl: './station-map-view.component.html',
    styleUrls: ['./station-map-view.component.scss'],
    changeDetection: ChangeDetectionStrategy.Eager,
    imports: [StationMapComponent]
})
export class StationMapViewComponent implements OnInit {
  private activatedRoute = inject(ActivatedRoute);

  guid: string | null = null;
  sharedName: string | null = null;

  ngOnInit() {
    this.activatedRoute.paramMap.subscribe((paramMap: ParamMap) => {
      // /stations/link/:name is a sharing link (the owner's visits); /stations/map/:guid your own map.
      this.sharedName = paramMap.get('name');
      this.guid = this.sharedName ? null : paramMap.get('guid');
    });
  }

}
