import { Component, OnInit } from '@angular/core';
import { ApiService } from 'src/app/services/api.service';
import { Route } from 'src/app/models/route.model';
import { Router } from '@angular/router';

@Component({
  selector: 'app-fill-missing-data-list',
  templateUrl: './fill-missing-data-list.component.html',
  styleUrls: ['./fill-missing-data-list.component.scss']
})
export class FillMissingDataListComponent implements OnInit {
  data: Route[];
  loading = false;

  constructor(
    private apiService: ApiService,
    private router: Router) { }

  ngOnInit() {
    this.loading = true;
    this.apiService.getRoutesWithMissingSettings().subscribe(data => {
      this.data = data;
      this.loading = false;
    })
  }

  edit(id: number) {
    this.router.navigate(['/admin','routes', id]);
  }
}
