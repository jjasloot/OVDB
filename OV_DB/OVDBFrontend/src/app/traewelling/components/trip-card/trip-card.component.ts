import { Component, Input, inject, output } from '@angular/core';

import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDividerModule } from '@angular/material/divider';
import { TranslateModule } from '@ngx-translate/core';
import { Router } from '@angular/router';
import { TrawellingTrip } from '../../../models/traewelling.model';
import { TrawellingService } from '../../services/traewelling.service';
import { RouteInstanceSearchDialogComponent } from '../route-instance-search-dialog/route-instance-search-dialog.component';
import { RouteSearchDialogComponent } from '../route-search-dialog/route-search-dialog.component';
import { MatDialog } from '@angular/material/dialog';

@Component({
  selector: 'app-trip-card',
  standalone: true,
  imports: [
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatMenuModule,
    MatProgressSpinnerModule,
    MatDividerModule,
    TranslateModule
  ],
  templateUrl: './trip-card.component.html',
  styleUrls: ['./trip-card.component.scss']
})
export class TripCardComponent {
  trawellingService = inject(TrawellingService);
  private router = inject(Router);
  private dialog = inject(MatDialog);

  @Input() trip!: TrawellingTrip;
  readonly removeTrip = output<void>();
  isProcessing = false;
  processingMessage = '';

  getTransportIcon(): string {
    return this.trawellingService.getTransportIcon(this.trip.transport.category);
  }

  getTransportColor(): string {
    return this.trawellingService.getTransportColor(this.trip.transport.category);
  }

  getJourneyNumber(): string {
    return this.trip.transport.journeyNumber || '';
  }

  async ignoreTrip() {
    this.isProcessing = true;
    this.processingMessage = 'Ignoring trip...';

    try {
      await this.trawellingService.ignoreTrip(this.trip.id);
      this.removeTrip.emit();
    } catch (error) {
      console.error('Error ignoring trip:', error);
    } finally {
      this.isProcessing = false;
    }
  }

  openRouteInstanceSearch() {
    const dialogRef = this.dialog.open(RouteInstanceSearchDialogComponent, {
      width: '90vw',
      maxWidth: '800px',
      data: { trip: this.trip }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result && result.success) {
        this.removeTrip.emit();
      }
    });
  }

  openRouteSearch() {
    const dialogRef = this.dialog.open(RouteSearchDialogComponent, {
      width: '90vw',
      maxWidth: '800px',
      data: { trip: this.trip }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result && result.routeCreated) {
        this.removeTrip.emit();
      }
    });
  }


  createRouteViaWizard() {
    const tripContext = this.trawellingService.getTripContextForRouteCreation(this.trip);
    // Store context in session storage for the wizard to pick up
    sessionStorage.setItem('traewellingTripContext', JSON.stringify(tripContext));
    this.router.navigate(['/admin/wizard'], {
      queryParams: {
        traewellingTripId: this.trip.id,
      }
    });
  }

  createRouteViaGPX() {
    const tripContext = this.trawellingService.getTripContextForRouteCreation(this.trip);
    // Store context in session storage for the GPX upload to pick up
    sessionStorage.setItem('traewellingTripContext', JSON.stringify(tripContext));
    this.router.navigate(['/admin/addRoute'], {
      queryParams: {
        traewellingTripId: this.trip.id,
      }
    });
  }
}