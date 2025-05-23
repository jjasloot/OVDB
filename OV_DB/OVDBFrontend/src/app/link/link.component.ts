import { Component, OnInit } from '@angular/core';
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
  guid: string;
  error = false;
  loading = true;

  constructor(
    private activatedRoute: ActivatedRoute,
    private apiService: ApiService
  ) { }

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
