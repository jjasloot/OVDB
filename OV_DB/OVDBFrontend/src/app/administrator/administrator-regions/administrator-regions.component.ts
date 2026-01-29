import { Component, OnInit, inject } from "@angular/core";
import { MatDialog } from "@angular/material/dialog";
import { NewRegion, Region } from "src/app/models/region.model";
import { RegionsService } from "src/app/services/regions.service";
import { AdministratorNewRegionComponent } from "../administrator-new-region/administrator-new-region.component";
import { MatCard, MatCardHeader, MatCardTitle, MatCardSubtitle, MatCardContent } from "@angular/material/card";
import { MatButton, MatFabButton } from "@angular/material/button";
import { MatIcon } from "@angular/material/icon";
import { SignalRService } from "src/app/services/signal-r.service";
import { MatChip } from "@angular/material/chips";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { MatFormField, MatLabel } from "@angular/material/form-field";
import { MatInput } from "@angular/material/input";
import { FormsModule } from "@angular/forms";

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
    MatChip,
    MatFormField,
    MatLabel,
    MatInput,
    FormsModule
  ]
})
export class AdministratorRegionsComponent implements OnInit {
  private regionsService = inject(RegionsService);
  private dialog = inject(MatDialog);
  private signalRService = inject(SignalRService);

  regions: Region[];
  progressUpdates: Record<number, number> = {};
  updateResult: Record<number, number> = {};

  constructor() {
    this.signalRService.regionUpdates$.pipe(takeUntilDestroyed()).subscribe((update) => {
      this.progressUpdates[update.regionId] = update.percentage;
      if (update.updatedRoutes != null) {
        this.updateResult[update.regionId] = update.updatedRoutes;
      }
    });
  }

  ngOnInit(): void {
    this.loadData();

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
      if (result) {
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
      },
      error: (err) => {
        alert(JSON.stringify(err));
      },
    });
  }

  refreshRoutesWithoutRegions() {
    this.regionsService.refreshRoutesWithoutRegions().subscribe({
      next: () => {
      },
      error: (err) => {
        alert(JSON.stringify(err));
      },
    });
  }

  saveRegion(region: Region) {
    this.regionsService.updateRegion(region).subscribe({
      next: () => {
        // Optional: show snackbar or notification
      },
      error: (err) => {
        alert("Failed to update region: " + JSON.stringify(err));
      }
    });
  }
}
