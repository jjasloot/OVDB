import { Component, OnInit } from '@angular/core';
import { AdminUser } from 'src/app/models/adminUser.model';
import { ApiService } from 'src/app/services/api.service';

@Component({
  selector: 'app-administrator-users',
  templateUrl: './administrator-users.component.html',
  styleUrls: ['./administrator-users.component.scss']
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
