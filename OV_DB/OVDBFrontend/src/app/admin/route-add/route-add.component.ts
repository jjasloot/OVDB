import { Component, OnInit, ViewChild, ElementRef } from '@angular/core';
import { ApiService } from 'src/app/services/api.service';
import { Router } from '@angular/router';
import { Route } from 'src/app/models/route.model';
import { DataUpdateService } from 'src/app/services/data-update.service';
import { FileUpload } from 'src/app/models/fileUpload.model';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { MatButton } from '@angular/material/button';
import { NgClass } from '@angular/common';
import { MatProgressSpinner } from '@angular/material/progress-spinner';

@Component({
    selector: 'app-route-add',
    templateUrl: './route-add.component.html',
    styleUrls: ['./route-add.component.scss'],
    standalone: true,
    imports: [MatButton, NgClass, MatProgressSpinner, TranslateModule]
})
export class RouteAddComponent implements OnInit {
  inputString: string;
  fileToUpload: FileList;
  text: string;
  filesLoading: boolean;
  files: FileUpload[];
  constructor(
    private apiService: ApiService,
    private router: Router,
    private translateService: TranslateService,
    private dataUpdateService: DataUpdateService
  ) { }

  ngOnInit() {
  }

  save() {
    if (!this.inputString) {
      return;
    }
    this.apiService.postRoute(this.inputString).subscribe((resp: Route) => {
      this.inputString = null;
      this.router.navigate(['/admin', 'routes', resp.routeId]);
    });
  }

  handleFileInput(files: FileList) {
    this.fileToUpload = files;
  }

  saveFiles() {
    if (!this.fileToUpload || this.fileToUpload.length === 0) {
      return;
    }
    this.filesLoading = true;
    this.apiService.postFiles(this.fileToUpload).subscribe((result: FileUpload[]) => {
      this.fileToUpload = null;
      this.text = result.length + ' ' + this.translateService.instant('ADDROUTE.FILESUPLOADED') + ', '
        + (result.filter(f => f.failed).length) + ' ' + this.translateService.instant('ADDROUTE.ERRORS');
      this.files = result;
      this.dataUpdateService.requestUpdate();
      this.filesLoading = false;
    });
  }

  edit(file: FileUpload) {
    this.router.navigate(['/admin', 'routes', file.routeId]);
  }

}
