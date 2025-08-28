import { Component, OnInit, inject } from '@angular/core';
import { ApiService } from 'src/app/services/api.service';
import { Route } from 'src/app/models/route.model';
import { Router } from '@angular/router';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { MatList, MatListItem } from '@angular/material/list';
import { MatButton } from '@angular/material/button';
import { TranslateModule } from '@ngx-translate/core';

@Component({
    selector: 'app-fill-missing-data-list',
    templateUrl: './fill-missing-data-list.component.html',
    styleUrls: ['./fill-missing-data-list.component.scss'],
    imports: [MatProgressSpinner, MatList, MatListItem, MatButton, TranslateModule]
})
export class FillMissingDataListComponent implements OnInit {
  private apiService = inject(ApiService);
  private router = inject(Router);

  data: Route[];
  loading = false;

  ngOnInit() {
    this.loading = true;
    this.apiService.getRoutesWithMissingSettings().subscribe(data => {
      this.data = data;
      this.loading = false;
    });
  }

  edit(id: number) {
    this.router.navigate(['/admin', 'routes', id]);
  }
}
