import { Component, OnInit, inject } from '@angular/core';
import { ApiService } from 'src/app/services/api.service';
import { Router, ActivatedRoute } from '@angular/router';
import { Route } from 'src/app/models/route.model';
import { DataUpdateService } from 'src/app/services/data-update.service';
import { FileUpload } from 'src/app/models/fileUpload.model';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { MatButton } from '@angular/material/button';
import { NgClass } from '@angular/common';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { TrawellingTripContext } from 'src/app/models/traewelling.model';
import { TrawellingContextCardComponent } from "src/app/traewelling/context-card/traewelling-context-card.component";

@Component({
  selector: 'app-route-add',
  templateUrl: './route-add.component.html',
  styleUrls: ['./route-add.component.scss'],
  imports: [MatButton, NgClass, MatProgressSpinner, TranslateModule, TrawellingContextCardComponent]
})
export class RouteAddComponent implements OnInit {
  private apiService = inject(ApiService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private translateService = inject(TranslateService);
  private dataUpdateService = inject(DataUpdateService);

  inputString: string;
  fileToUpload: FileList;
  text: string;
  filesLoading: boolean;
  files: FileUpload[];
  fromTraewelling = false;
  trawellingTripData: TrawellingTripContext | null = null;

  ngOnInit() {
    // Check if coming from Träwelling
    this.route.queryParams.subscribe(params => {
      if (params['traewellingTripId']) {
        this.fromTraewelling = true;
        const tripDataStr = sessionStorage.getItem('traewellingTripContext');
        if (tripDataStr) {
          const trawellingTripData = JSON.parse(tripDataStr) as TrawellingTripContext;
          if (trawellingTripData.tripId === +params['traewellingTripId']) {
            // If the IDs match, use the data
            this.trawellingTripData = trawellingTripData;
          }
        }
      }
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
    if(!!this.trawellingTripData){
      this.router.navigate(['/admin', 'routes', file.routeId], {
        queryParams: { traewellingTripId: this.trawellingTripData.tripId }
      });
    } else {
      this.router.navigate(['/admin', 'routes', file.routeId]);
    }
  }

}
