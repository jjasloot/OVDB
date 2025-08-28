import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule } from '@angular/material/table';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTabsModule } from '@angular/material/tabs';
import { MatIconModule } from '@angular/material/icon';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration, ChartData, ChartType } from 'chart.js';
import { ApiService } from '../../services/api.service';
import { AnalyticsService } from '../../services/analytics.service';
import { Map } from '../../models/map.model';
import {
  TripFrequencyData,
  ActivityHeatmapData,
  RouteRankingData,
  TravelTimeTrendsData,
  IntegrationStatsData,
  CoverageOverviewData,
  ActivityDataPoint
} from '../../models/analytics.interfaces';

@Component({
  selector: 'app-enhanced-analytics',
  imports: [
    CommonModule,
    MatCardModule,
    MatSelectModule,
    MatFormFieldModule,
    MatButtonModule,
    MatTableModule,
    MatDatepickerModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatTabsModule,
    MatIconModule,
    FormsModule,
    TranslateModule,
    BaseChartDirective
  ],
  templateUrl: './enhanced-analytics.component.html',
  styleUrls: ['./enhanced-analytics.component.scss']
})
export class EnhancedAnalyticsComponent implements OnInit {
  maps: Map[] = [];
  years: number[] = [];
  selectedMap: string = '';
  selectedYear: number = new Date().getFullYear();
  loading = false;

  // Data properties
  tripFrequencyData: TripFrequencyData[] = [];
  activityHeatmapData: ActivityHeatmapData | null = null;
  routeRankings: RouteRankingData[] = [];
  travelTimeTrends: TravelTimeTrendsData | null = null;
  integrationStats: IntegrationStatsData | null = null;
  coverageOverview: CoverageOverviewData | null = null;

  // Chart configurations
  tripFrequencyChartData: ChartData<'line'> = { datasets: [], labels: [] };
  tripFrequencyChartOptions: ChartConfiguration['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      title: { display: true, text: 'Trip Frequency Over Time' },
      legend: { display: true, position: 'top' }
    },
    scales: {
      x: {
        type: 'time',
        time: { unit: 'day', displayFormats: { day: 'MMM dd' } }
      },
      y: { beginAtZero: true, title: { display: true, text: 'Number of Trips' } }
    }
  };

  travelTimeTrendsChartData: ChartData<'bar'> = { datasets: [], labels: [] };
  travelTimeTrendsChartOptions: ChartConfiguration['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      title: { display: true, text: 'Average Travel Duration by Hour' },
      legend: { display: false }
    },
    scales: {
      x: { title: { display: true, text: 'Hour of Day' } },
      y: { beginAtZero: true, title: { display: true, text: 'Duration (hours)' } }
    }
  };

  // Table configurations
  routeRankingsColumns: string[] = ['rank', 'routeName', 'routeType', 'tripCount', 'totalDistance', 'averageDistance'];

  constructor(
    private apiService: ApiService,
    private analyticsService: AnalyticsService
  ) {}

  ngOnInit(): void {
    this.loadMaps();
    this.initializeYears();
  }

  private loadMaps(): void {
    this.apiService.getMaps().subscribe(maps => {
      this.maps = maps;
      if (maps.length > 0) {
        this.selectedMap = maps[0].mapGuid;
        this.loadAllAnalytics();
      }
    });
  }

  private initializeYears(): void {
    const currentYear = new Date().getFullYear();
    for (let year = currentYear; year >= currentYear - 10; year--) {
      this.years.push(year);
    }
  }

  onMapChange(): void {
    if (this.selectedMap) {
      this.loadAllAnalytics();
    }
  }

  onYearChange(): void {
    if (this.selectedMap) {
      this.loadAllAnalytics();
    }
  }

  private loadAllAnalytics(): void {
    if (!this.selectedMap) return;

    this.loading = true;
    
    // Load all analytics data concurrently
    Promise.all([
      this.loadTripFrequency(),
      this.loadActivityHeatmap(),
      this.loadRouteRankings(),
      this.loadTravelTimeTrends(),
      this.loadIntegrationStats(),
      this.loadCoverageOverview()
    ]).finally(() => {
      this.loading = false;
    });
  }

  private async loadTripFrequency(): Promise<void> {
    try {
      this.tripFrequencyData = await this.analyticsService.getTripFrequency(
        this.selectedMap, 
        undefined, 
        undefined, 
        this.selectedYear
      ).toPromise() || [];
      
      this.updateTripFrequencyChart();
    } catch (error) {
      console.error('Error loading trip frequency:', error);
    }
  }

  private async loadActivityHeatmap(): Promise<void> {
    try {
      this.activityHeatmapData = await this.analyticsService.getActivityHeatmap(
        this.selectedMap, 
        this.selectedYear
      ).toPromise() || null;
    } catch (error) {
      console.error('Error loading activity heatmap:', error);
    }
  }

  private async loadRouteRankings(): Promise<void> {
    try {
      this.routeRankings = await this.analyticsService.getRouteRankings(
        this.selectedMap, 
        this.selectedYear, 
        20
      ).toPromise() || [];
    } catch (error) {
      console.error('Error loading route rankings:', error);
    }
  }

  private async loadTravelTimeTrends(): Promise<void> {
    try {
      this.travelTimeTrends = await this.analyticsService.getTravelTimeTrends(
        this.selectedMap, 
        this.selectedYear
      ).toPromise() || null;
      
      this.updateTravelTimeTrendsChart();
    } catch (error) {
      console.error('Error loading travel time trends:', error);
    }
  }

  private async loadIntegrationStats(): Promise<void> {
    try {
      this.integrationStats = await this.analyticsService.getIntegrationStats(
        this.selectedMap, 
        this.selectedYear
      ).toPromise() || null;
    } catch (error) {
      console.error('Error loading integration stats:', error);
    }
  }

  private async loadCoverageOverview(): Promise<void> {
    try {
      this.coverageOverview = await this.analyticsService.getCoverageOverview(
        this.selectedMap, 
        this.selectedYear
      ).toPromise() || null;
    } catch (error) {
      console.error('Error loading coverage overview:', error);
    }
  }

  private updateTripFrequencyChart(): void {
    if (!this.tripFrequencyData?.length) return;

    this.tripFrequencyChartData = {
      labels: this.tripFrequencyData.map(d => d.date),
      datasets: [
        {
          label: 'Trips per Day',
          data: this.tripFrequencyData.map(d => d.tripCount),
          borderColor: '#1f77b4',
          backgroundColor: 'rgba(31, 119, 180, 0.1)',
          fill: true
        }
      ]
    };
  }

  private updateTravelTimeTrendsChart(): void {
    if (!this.travelTimeTrends?.averageDurationByHour?.length) return;

    this.travelTimeTrendsChartData = {
      labels: this.travelTimeTrends.averageDurationByHour.map(d => d.hour.toString()),
      datasets: [
        {
          label: 'Average Duration',
          data: this.travelTimeTrends.averageDurationByHour.map(d => d.averageDuration),
          backgroundColor: '#ff7f0e'
        }
      ]
    };
  }

  // Activity heatmap helpers
  getActivityLevel(point: ActivityDataPoint): string {
    const levels = ['level-0', 'level-1', 'level-2', 'level-3', 'level-4'];
    return levels[Math.min(point.level, 4)] || 'level-0';
  }

  getWeeksInYear(): any[][] {
    if (!this.activityHeatmapData) return [];
    
    const weeks: any[][] = [];
    const data = this.activityHeatmapData.data;
    const dataMap = new Map(data.map(d => [d.date, d]));
    
    const startDate = new Date(this.selectedYear, 0, 1);
    const endDate = new Date(this.selectedYear, 11, 31);
    
    let currentWeek: any[] = [];
    let currentDate = new Date(startDate);
    
    // Fill empty days at start of first week
    const firstDayOfWeek = currentDate.getDay();
    for (let i = 0; i < firstDayOfWeek; i++) {
      currentWeek.push(null);
    }
    
    while (currentDate <= endDate) {
      const dateStr = currentDate.toISOString().split('T')[0];
      const dataPoint = dataMap.get(dateStr);
      
      currentWeek.push(dataPoint || { date: dateStr, tripCount: 0, totalDistance: 0, level: 0 });
      
      if (currentWeek.length === 7) {
        weeks.push([...currentWeek]);
        currentWeek = [];
      }
      
      currentDate.setDate(currentDate.getDate() + 1);
    }
    
    // Fill empty days at end of last week
    while (currentWeek.length < 7) {
      currentWeek.push(null);
    }
    if (currentWeek.some(d => d !== null)) {
      weeks.push(currentWeek);
    }
    
    return weeks;
  }

  // Export functions
  exportTripFrequency(): void {
    if (this.tripFrequencyData?.length) {
      this.analyticsService.exportToCsv(this.tripFrequencyData, 'trip-frequency.csv');
    }
  }

  exportRouteRankings(): void {
    if (this.routeRankings?.length) {
      this.analyticsService.exportToCsv(this.routeRankings, 'route-rankings.csv');
    }
  }

  exportIntegrationStats(): void {
    if (this.integrationStats) {
      const exportData = [
        { metric: 'Total Trips', value: this.integrationStats.totalTrips },
        { metric: 'Trips with Timing', value: this.integrationStats.tripsWithTiming },
        { metric: 'Träwelling Imported', value: this.integrationStats.traewellingImported },
        { metric: 'Trips with Source', value: this.integrationStats.tripsWithSource },
        { metric: 'Timing Completeness (%)', value: this.integrationStats.timingCompleteness }
      ];
      this.analyticsService.exportToCsv(exportData, 'integration-stats.csv');
    }
  }
}
