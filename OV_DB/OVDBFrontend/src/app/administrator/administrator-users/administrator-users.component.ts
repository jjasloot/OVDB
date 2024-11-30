import { Component, OnInit } from '@angular/core';
import { AdminUser } from 'src/app/models/adminUser.model';
import { ApiService } from 'src/app/services/api.service';
import { MatTable, MatColumnDef, MatHeaderCellDef, MatHeaderCell, MatCellDef, MatCell, MatHeaderRowDef, MatHeaderRow, MatRowDef, MatRow } from '@angular/material/table';
import { MatCheckbox } from '@angular/material/checkbox';
import { DatePipe } from '@angular/common';

@Component({
    selector: 'app-administrator-users',
    templateUrl: './administrator-users.component.html',
    styleUrls: ['./administrator-users.component.scss'],
    imports: [MatTable, MatColumnDef, MatHeaderCellDef, MatHeaderCell, MatCellDef, MatCell, MatCheckbox, MatHeaderRowDef, MatHeaderRow, MatRowDef, MatRow, DatePipe]
})
export class AdministratorUsersComponent implements OnInit {

  data: AdminUser[];
  displayedColumns: string[] = ['id', 'email', 'lastLogin', 'routes', 'isAdmin'];
  constructor(private apiService: ApiService) { }

  ngOnInit(): void {
    this.apiService.administratorGetUsers().subscribe(data => {
      this.data = data;
    });
  }
}
