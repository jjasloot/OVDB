import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslateModule } from '@ngx-translate/core';
import { TripListComponent } from './components/trip-list/trip-list.component';
import { TrawellingService } from './services/traewelling.service';
import { TrawellingConnectionStatus } from '../models/traewelling.model';

@Component({
  selector: 'app-traewelling',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    TranslateModule,
    TripListComponent
  ],
  template: `
    <div class="traewelling-container">
      @if (loading) {
        <div class="loading-container">
          <mat-spinner></mat-spinner>
          <span>{{ 'TRAEWELLING.LOADING_TRIPS' | translate }}</span>
        </div>
      } @else if (!connectionStatus?.connected) {
        <div class="not-connected">
          <mat-card>
            <mat-card-header>
              <mat-card-title>{{ 'TRAEWELLING.TITLE' | translate }}</mat-card-title>
            </mat-card-header>
            <mat-card-content>
              <div class="not-connected-content">
                <mat-icon>link_off</mat-icon>
                <p>{{ 'PROFILE.TRAEWELLING_NOT_CONNECTED' | translate }}</p>
                <button mat-raised-button color="primary" (click)="goToProfile()">
                  <mat-icon>arrow_back</mat-icon>
                  {{ 'BACK' | translate }}
                </button>
              </div>
            </mat-card-content>
          </mat-card>
        </div>
      } @else {
        <div class="traewelling-content">
          <h1>{{ 'TRAEWELLING.TITLE' | translate }}</h1>
          <app-trip-list></app-trip-list>
        </div>
      }
    </div>
  `,
  styles: [`
    .traewelling-container {
      padding: 16px;
      max-width: 1200px;
      margin: 0 auto;
    }

    .loading-container {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 16px;
      padding: 48px 16px;
    }

    .not-connected {
      display: flex;
      justify-content: center;
      padding: 48px 16px;
    }

    .not-connected mat-card {
      max-width: 400px;
      width: 100%;
    }

    .not-connected-content {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 16px;
      text-align: center;
      padding: 24px 0;
    }

    .not-connected-content mat-icon {
      font-size: 48px;
      width: 48px;
      height: 48px;
      opacity: 0.5;
    }

    .traewelling-content h1 {
      margin: 0 0 24px 0;
      font-size: 24px;
      font-weight: 500;
    }

    /* Mobile responsiveness */
    @media (max-width: 768px) {
      .traewelling-container {
        padding: 8px;
      }
      
      .traewelling-content h1 {
        font-size: 20px;
        margin-bottom: 16px;
      }
    }
  `]
})
export class TrawellingComponent implements OnInit {
  loading = true;
  connectionStatus: TrawellingConnectionStatus | null = null;

  constructor(
    private router: Router,
    private trawellingService: TrawellingService
  ) {}

  async ngOnInit() {
    try {
      this.connectionStatus = await this.trawellingService.getConnectionStatus();
    } catch (error) {
      console.error('Error checking Träwelling connection:', error);
      this.connectionStatus = { connected: false };
    } finally {
      this.loading = false;
    }
  }

  goToProfile() {
    this.router.navigate(['/profile']);
  }
}