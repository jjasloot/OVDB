import { Component, OnInit } from "@angular/core";
import { MatDialog } from "@angular/material/dialog";
import { NewRegion, Region } from "src/app/models/region.model";
import { RegionsService } from "src/app/services/regions.service";
import { AdministratorNewRegionComponent } from "../administrator-new-region/administrator-new-region.component";
import { MatCard, MatCardHeader, MatCardTitle, MatCardSubtitle, MatCardContent } from "@angular/material/card";
import { MatButton, MatFabButton } from "@angular/material/button";
import { MatIcon } from "@angular/material/icon";
import { SignalRService } from "src/app/services/signal-r.service";

@Component({
    selector: "app-administrator-regions",
    templateUrl: "./administrator-regions.component.html",
    styleUrl: "./administrator-regions.component.scss",
    imports: [
        MatCard,
        MatCardHeader,
        MatCardTitle,
        MatCardSubtitle,
        MatButton,
        MatIcon,
        MatCardContent,
        MatFabButton,
    ]
})
export class AdministratorRegionsComponent implements OnInit {
  regions: Region[];
  progressUpdates: { [key: number]: number } = {};

  constructor(
    private regionsService: RegionsService,
    private dialog: MatDialog,
    private signalRService: SignalRService
  ) {}

  ngOnInit(): void {
    this.loadData();
    this.signalRService.connect();
    this.signalRService.updates$.subscribe((update) => {
      this.progressUpdates[update.requestIdentifier] = update.percentage;
    });
  }

  private loadData() {
    this.regionsService.getRegions().subscribe((regions) => {
      this.regions = regions;
    });
  }

  addNewRegion() {
    const dialogRef = this.dialog.open(AdministratorNewRegionComponent, {
      data: { regions: this.regions },
    });
    dialogRef.afterClosed().subscribe((result?: NewRegion) => {
      if (!!result) {
        this.regionsService.addRegion(result).subscribe({
          next: () => {
            this.loadData();
          },
        });
      }
    });
  }

  refreshRoutesForRegion(regionId: number) {
    this.regionsService.refreshRoutesForRegion(regionId).subscribe({
      next: () => {
        alert("done");
      },
      error: (err) => {
        alert(JSON.stringify(err));
      },
    });
  }

  refreshRoutesWithoutRegions() {
    this.regionsService.refreshRoutesWithoutRegions().subscribe({
      next: () => {
        alert("done");
      },
      error: (err) => {
        alert(JSON.stringify(err));
      },
    });
  }
}
