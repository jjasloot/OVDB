import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, ParamMap } from '@angular/router';
import { ApiService } from 'src/app/services/api.service';
import { StationMapComponent } from '../station-map/station-map.component';

@Component({
    selector: 'app-station-map-view',
    templateUrl: './station-map-view.component.html',
    styleUrls: ['./station-map-view.component.scss'],
    imports: [StationMapComponent]
})
export class StationMapViewComponent implements OnInit {
  private activatedRoute = inject(ActivatedRoute);
  private apiService = inject(ApiService);

  guid: string;

  ngOnInit() {
    this.activatedRoute.paramMap.subscribe((paramMap: ParamMap) => {
      const name = paramMap.get('name');
      if (name != null) {
        this.apiService.getGuidFromStationMapName(name).subscribe(guid => {
          this.guid = guid;
        })
      } else {
        this.guid = paramMap.get('guid');
      }
    });
  }

}
