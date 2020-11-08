import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, ParamMap } from '@angular/router';

@Component({
  selector: 'app-station-map-view',
  templateUrl: './station-map-view.component.html',
  styleUrls: ['./station-map-view.component.scss']
})
export class StationMapViewComponent implements OnInit {
  guid: string;

  constructor(private activatedRoute: ActivatedRoute) { }

  ngOnInit() {
    this.activatedRoute.paramMap.subscribe((paramMap: ParamMap) => {
      this.guid = paramMap.get('guid');
    });
  }

}
