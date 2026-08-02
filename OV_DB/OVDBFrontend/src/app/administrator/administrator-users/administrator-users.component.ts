import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, AfterViewInit, ViewChild, inject } from '@angular/core';
import { AdminUser } from 'src/app/models/adminUser.model';
import { ApiService } from 'src/app/services/api.service';
import { MatTable, MatColumnDef, MatHeaderCellDef, MatHeaderCell, MatCellDef, MatCell, MatHeaderRowDef, MatHeaderRow, MatRowDef, MatRow } from '@angular/material/table';
import { MatSort, MatSortHeader } from '@angular/material/sort';
import { MatPaginator } from '@angular/material/paginator';
import { MatTableDataSource } from '@angular/material/table';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatIcon } from '@angular/material/icon';
import { DatePipe } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { TranslationService } from 'src/app/services/translation.service';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

@Component({
    selector: 'app-administrator-users',
    templateUrl: './administrator-users.component.html',
    styleUrls: ['./administrator-users.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [MatTable, MatSort, MatColumnDef, MatHeaderCellDef, MatHeaderCell, MatSortHeader, MatCellDef, MatCell, MatHeaderRowDef, MatHeaderRow, MatRowDef, MatRow, MatPaginator, MatFormField, MatLabel, MatInput, MatIcon, DatePipe, TranslateModule]
})
export class AdministratorUsersComponent implements OnInit, AfterViewInit {
  private apiService = inject(ApiService);
  private translationService = inject(TranslationService);
  private destroyRef = inject(DestroyRef);

  @ViewChild(MatSort) sort!: MatSort;
  @ViewChild(MatPaginator) paginator!: MatPaginator;

  dataSource = new MatTableDataSource<AdminUser>([]);
  displayedColumns: string[] = ['id', 'email', 'lastLogin', 'routeCount', 'routeInstancesCount', 'routeInstancesWithTimeCount', 'routeInstancesWithTrawellingIdCount', 'lastRouteInstanceDate', 'isAdmin'];

  get currentLocale() {
    return this.translationService.dateLocale;
  }

  ngOnInit(): void {
    this.apiService.administratorGetUsers().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(data => {
      this.dataSource.data = data;
    });
  }

  ngAfterViewInit(): void {
    this.dataSource.sort = this.sort;
    this.dataSource.paginator = this.paginator;
  }

  applyFilter(event: Event) {
    this.dataSource.filter = (event.target as HTMLInputElement).value.trim().toLowerCase();
    this.dataSource.paginator?.firstPage();
  }
}
