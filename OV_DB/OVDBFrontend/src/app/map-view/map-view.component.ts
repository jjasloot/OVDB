import { Component, OnInit, inject, ChangeDetectionStrategy } from '@angular/core';
import { ActivatedRoute, ParamMap } from '@angular/router';
import { MapComponent } from '../map/map.component';

@Component({
    selector: 'app-map-view',
    templateUrl: './map-view.component.html',
    styleUrls: ['./map-view.component.scss'],
    changeDetection: ChangeDetectionStrategy.Eager,
    imports: [MapComponent]
})
export class MapViewComponent implements OnInit {
  private activatedRoute = inject(ActivatedRoute);

  guid: string | null = null;

  ngOnInit() {
    this.activatedRoute.paramMap.subscribe((paramMap: ParamMap) => {
      this.guid = paramMap.get('guid');
    });
  }

}
