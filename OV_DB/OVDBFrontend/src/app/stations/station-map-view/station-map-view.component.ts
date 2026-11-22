import { TranslateModule } from '@ngx-translate/core';
import { Component, OnInit, inject, ChangeDetectionStrategy } from '@angular/core';
import { ActivatedRoute, ParamMap } from '@angular/router';
import { ApiService } from 'src/app/services/api.service';
import { StationMapComponent } from '../station-map/station-map.component';

@Component({
    selector: 'app-station-map-view',
    templateUrl: './station-map-view.component.html',
    styleUrls: ['./station-map-view.component.scss'],
    changeDetection: ChangeDetectionStrategy.Eager,
    imports: [StationMapComponent, TranslateModule]
})
export class StationMapViewComponent implements OnInit {
  private activatedRoute = inject(ActivatedRoute);
  private apiService = inject(ApiService);

  guid: string | null = null;
  linkNotFound = false;

  ngOnInit() {
    this.activatedRoute.paramMap.subscribe((paramMap: ParamMap) => {
      const name = paramMap.get('name');
      if (name != null) {
        this.apiService.getGuidFromStationMapName(name).subscribe({
          next: guid => this.guid = guid,
          // An unknown link otherwise leaves an empty page.
          error: () => this.linkNotFound = true,
        });
      } else {
        this.guid = paramMap.get('guid');
      }
    });
  }

}
