import { Component, OnInit, inject, ChangeDetectionStrategy } from "@angular/core";
import { MatDialog } from "@angular/material/dialog";
import { MatSnackBar } from "@angular/material/snack-bar";
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
import { MatTooltip } from "@angular/material/tooltip";
import { FormsModule } from "@angular/forms";
import { NetworkCompletionService } from "src/app/services/network-completion.service";
import { TranslateModule, TranslateService } from "@ngx-translate/core";
import { NetworkStatus } from "src/app/models/network-completion.model";

@Component({
  selector: "app-administrator-regions",
  templateUrl: "./administrator-regions.component.html",
  styleUrl: "./administrator-regions.component.scss",
  changeDetection: ChangeDetectionStrategy.Eager,
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
    MatTooltip,
    FormsModule,
    TranslateModule
  ]
})
export class AdministratorRegionsComponent implements OnInit {
  private regionsService = inject(RegionsService);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private signalRService = inject(SignalRService);

  regions!: Region[];
  progressUpdates: Record<number, number> = {};
  updateResult: Record<number, number> = {};
  /** Rail network assembly progress per region (docs/region-network-completion.md). */
  networkProgress: Record<number, number> = {};
  /** Why the last network assembly of a region failed; absent when it succeeded. */
  networkError: Record<number, string> = {};

  private networkService = inject(NetworkCompletionService);
  private translateService = inject(TranslateService);

  constructor() {
    this.signalRService.regionUpdates$.pipe(takeUntilDestroyed()).subscribe((update) => {
      this.progressUpdates[update.regionId] = update.percentage;
      if (update.updatedRoutes != null) {
        this.updateResult[update.regionId] = update.updatedRoutes;
      }
    });
    this.signalRService.networkUpdates$.pipe(takeUntilDestroyed()).subscribe((update) => {
      this.networkProgress[update.regionId] = update.percentage;
      if (update.percentage === 0) {
        delete this.networkError[update.regionId];
      }
      if (update.percentage >= 100) {
        // 100 also ends a failed run; the status says which, and why.
        this.networkService.status(update.regionId).subscribe({
          next: (rows) => {
            const failed = rows.find((r) => r.status === NetworkStatus.Failed);
            if (failed) {
              this.networkError[update.regionId] = failed.error ?? "";
            } else {
              delete this.networkError[update.regionId];
            }
          },
          error: () => undefined,
        });
      }
    });
  }

  assembleNetwork(regionId: number, refresh = false) {
    this.networkService.assemble(regionId, refresh).subscribe({
      next: () => this.snackBar.open(this.translateService.instant("STATS.NETWORK.QUEUED"), undefined, { duration: 3000 }),
      error: () => this.snackBar.open(this.translateService.instant("STATS.NETWORK.QUEUE_FAILED"), undefined, { duration: 5000 }),
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
    this.regionsService.refreshRoutesForRegion(regionId).subscribe(() => {
      this.snackBar.open("Refresh queued", undefined, { duration: 3000 });
    });
  }

  refreshRoutesWithoutRegions() {
    this.regionsService.refreshRoutesWithoutRegions().subscribe(() => {
      this.snackBar.open("Refresh queued", undefined, { duration: 3000 });
    });
  }

  saveRegion(region: Region) {
    this.regionsService.updateRegion(region).subscribe(() => {
      this.snackBar.open("Region saved", undefined, { duration: 3000 });
    });
  }
}
