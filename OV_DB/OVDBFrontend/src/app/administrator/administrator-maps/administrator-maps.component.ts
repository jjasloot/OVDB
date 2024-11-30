import { Component, OnInit } from '@angular/core';
import { ApiService } from 'src/app/services/api.service';
import { AdminMap } from 'src/app/models/adminMap.model';
import { Router } from '@angular/router';
import { MatTable, MatColumnDef, MatHeaderCellDef, MatHeaderCell, MatCellDef, MatCell, MatHeaderRowDef, MatHeaderRow, MatRowDef, MatRow } from '@angular/material/table';
import { MatIconButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';

@Component({
    selector: 'app-administrator-maps',
    templateUrl: './administrator-maps.component.html',
    styleUrls: ['./administrator-maps.component.scss'],
    imports: [MatTable, MatColumnDef, MatHeaderCellDef, MatHeaderCell, MatCellDef, MatCell, MatIconButton, MatIcon, MatHeaderRowDef, MatHeaderRow, MatRowDef, MatRow]
})
export class AdministratorMapsComponent implements OnInit {
  data: AdminMap[];
  displayedColumns: string[] = ['id', 'name', 'user', 'link', 'routes', 'buttons'];
  constructor(private apiService: ApiService, private router: Router) { }

  ngOnInit(): void {
    this.apiService.administratorGetMaps().subscribe(data => {
      this.data = data;
    });
  }

  view(map: AdminMap) {
    this.router.navigate(['/map', map.guid]);
  }

}
