import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, ParamMap } from '@angular/router';
import { MapComponent } from '../map/map.component';

@Component({
    selector: 'app-map-view',
    templateUrl: './map-view.component.html',
    styleUrls: ['./map-view.component.scss'],
    imports: [MapComponent]
})
export class MapViewComponent implements OnInit {
  private activatedRoute = inject(ActivatedRoute);

  guid: string;

  ngOnInit() {
    this.activatedRoute.paramMap.subscribe((paramMap: ParamMap) => {
      this.guid = paramMap.get('guid');
    });
  }

}
