import { Component, Inject, OnInit, OnDestroy } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogContent, MatDialogActions } from '@angular/material/dialog';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { MatCard, MatCardContent } from '@angular/material/card';
import { MatChipSet, MatChip } from '@angular/material/chips';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';

import { ApiService } from '../../services/api.service';
import { Route } from '../../models/route.model';
import { TrawellingTrip, TrawellingHafasTravelType } from '../../models/traewelling.model';

import { debounceTime, distinctUntilChanged, switchMap, takeUntil } from 'rxjs/operators';
import { of, Subject } from 'rxjs';
import { TranslationService } from 'src/app/services/translation.service';

@Component({
  selector: 'app-route-search-dialog',
  templateUrl: './route-search-dialog.component.html',
  styleUrls: ['./route-search-dialog.component.scss'],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslateModule,
    MatDialogContent,
    MatDialogActions,
    MatFormField,
    MatLabel,
    MatInput,
    MatButton,
    MatIcon,
    MatProgressSpinner,
    MatCard,
    MatCardContent,
    MatChipSet,
    MatChip
  ]
})
export class RouteSearchDialogComponent implements OnInit, OnDestroy {
  searchControl = new FormControl('');
  routes: Route[] = [];
  loading = false;
  selectedRoute: Route | null = null;
  private destroy$ = new Subject<void>();

  constructor(
    public dialogRef: MatDialogRef<RouteSearchDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { trip: TrawellingTrip },
    private apiService: ApiService,
    private translationService: TranslationService
  ) { }

  ngOnInit(): void {
    // Initialize search with origin from trip
    const initialSearch = `${this.data.trip.transport?.origin?.name} => ${this.data.trip.transport?.destination?.name}`;
    this.searchControl.setValue(initialSearch);

    // Set up search subscription
    this.searchControl.valueChanges.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap(searchTerm => {
        if (!searchTerm || searchTerm.length < 2) {
          return of([]);
        }
        this.loading = true;
        return this.apiService.getAllRoutes(0, 20, 'date', true, searchTerm);
      }),
      takeUntil(this.destroy$)
    ).subscribe({
      next: (response: any) => {
        this.routes = response.routes || [];
        this.loading = false;
      },
      error: (error) => {
        console.error('Error searching routes:', error);
        this.routes = [];
        this.loading = false;
      }
    });

    // Trigger initial search
    this.searchControl.updateValueAndValidity();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  selectRoute(route: Route): void {
    this.selectedRoute = route;
  }

  confirm(): void {
    if (this.selectedRoute) {
      this.dialogRef.close({
        route: this.selectedRoute,
        trip: this.data.trip
      });
    }
  }

  cancel(): void {
    this.dialogRef.close();
  }

  getCategoryIcon(category: TrawellingHafasTravelType): string {
    switch (category) {
      case TrawellingHafasTravelType.NATIONAL_EXPRESS:
      case TrawellingHafasTravelType.NATIONAL:
      case TrawellingHafasTravelType.REGIONAL_EXP:
      case TrawellingHafasTravelType.REGIONAL:
      case TrawellingHafasTravelType.SUBURBAN:
        return 'train';
      case TrawellingHafasTravelType.BUS:
        return 'directions_bus';
      case TrawellingHafasTravelType.SUBWAY:
        return 'subway';
      case TrawellingHafasTravelType.TRAM:
        return 'tram';
      case TrawellingHafasTravelType.FERRY:
        return 'directions_boat';
      case TrawellingHafasTravelType.TAXI:
        return 'local_taxi';
      default:
        return 'directions_transit';
    }
  }
  name(item: any) {
    return this.translationService.getNameForItem(item);
  }
}