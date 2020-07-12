import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, ParamMap } from '@angular/router';
import { ApiService } from '../services/api.service';

@Component({
  selector: 'app-link',
  templateUrl: './link.component.html',
  styleUrls: ['./link.component.scss']
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
