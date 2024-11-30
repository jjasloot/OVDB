import { Component, OnInit } from '@angular/core';
import { MatToolbar } from '@angular/material/toolbar';
import { RouterOutlet } from '@angular/router';

@Component({
    selector: 'app-admin',
    templateUrl: './admin.component.html',
    styleUrls: ['./admin.component.scss'],
    imports: [MatToolbar, RouterOutlet]
})
export class AdminComponent implements OnInit {

  constructor() { }

  ngOnInit() {
  }

}
