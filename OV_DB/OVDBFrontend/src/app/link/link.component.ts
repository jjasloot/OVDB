import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, ParamMap } from '@angular/router';
import { ApiService } from '../services/api.service';
import { MapComponent } from '../map/map.component';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { TranslateModule } from '@ngx-translate/core';

@Component({
    selector: 'app-link',
    templateUrl: './link.component.html',
    styleUrls: ['./link.component.scss'],
    imports: [MapComponent, MatProgressSpinner, TranslateModule]
})
export class LinkComponent implements OnInit {
  private activatedRoute = inject(ActivatedRoute);
  private apiService = inject(ApiService);

  guid: string;
  error = false;
  loading = true;

  ngOnInit() {
    this.activatedRoute.paramMap.subscribe((paramMap: ParamMap) => {
      const name = paramMap.get('name');
      this.apiService.getGuidFromMapName(name).subscribe(guid => {
        this.guid = guid;
        this.loading = false;
      }, err => {
        this.error = true;
        this.loading = false;

      });
    });
  }

}
