import { Component, OnInit, inject } from '@angular/core';
import { AdminUser } from 'src/app/models/adminUser.model';
import { ApiService } from 'src/app/services/api.service';
import { MatTable, MatColumnDef, MatHeaderCellDef, MatHeaderCell, MatCellDef, MatCell, MatHeaderRowDef, MatHeaderRow, MatRowDef, MatRow } from '@angular/material/table';
import { MatSort, MatSortHeader } from '@angular/material/sort';
import { MatTableDataSource } from '@angular/material/table';
import { MatCheckbox } from '@angular/material/checkbox';
import { DatePipe } from '@angular/common';

@Component({
    selector: 'app-administrator-users',
    templateUrl: './administrator-users.component.html',
    styleUrls: ['./administrator-users.component.scss'],
    imports: [MatTable, MatSort, MatColumnDef, MatHeaderCellDef, MatHeaderCell, MatSortHeader, MatCellDef, MatCell, MatCheckbox, MatHeaderRowDef, MatHeaderRow, MatRowDef, MatRow, DatePipe]
})
export class AdministratorUsersComponent implements OnInit {
  private apiService = inject(ApiService);

  dataSource = new MatTableDataSource<AdminUser>([]);
  displayedColumns: string[] = ['id', 'email', 'lastLogin', 'routes', 'routeInstances', 'routeInstancesWithTime', 'routeInstancesWithTrawelling', 'lastRouteInstanceDate', 'isAdmin'];

  ngOnInit(): void {
    this.apiService.administratorGetUsers().subscribe(data => {
      this.dataSource.data = data;
    });
  }
}
