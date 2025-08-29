import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  TripFrequencyData,
  ActivityHeatmapData,
  RouteRankingData,
  TravelTimeTrendsData,
  IntegrationStatsData,
  CoverageOverviewData
} from '../models/analytics.interfaces';

@Injectable({
  providedIn: 'root'
})
export class AnalyticsService {

  constructor(private http: HttpClient) { }

  getTripFrequency(mapGuid: string, startDate?: Date, endDate?: Date, year?: number): Observable<TripFrequencyData[]> {
    let params = new HttpParams();
    if (startDate) params = params.set('startDate', startDate.toISOString());
    if (endDate) params = params.set('endDate', endDate.toISOString());
    if (year) params = params.set('year', year.toString());
    
    return this.http.get<TripFrequencyData[]>(`api/analytics/trip-frequency/${mapGuid}`, { params });
  }

  getActivityHeatmap(mapGuid: string, year?: number): Observable<ActivityHeatmapData> {
    let params = new HttpParams();
    if (year) params = params.set('year', year.toString());
    
    return this.http.get<ActivityHeatmapData>(`api/analytics/activity-heatmap/${mapGuid}`, { params });
  }

  getRouteRankings(mapGuid: string, year?: number, limit?: number): Observable<RouteRankingData[]> {
    let params = new HttpParams();
    if (year) params = params.set('year', year.toString());
    if (limit) params = params.set('limit', limit.toString());
    
    return this.http.get<RouteRankingData[]>(`api/analytics/route-rankings/${mapGuid}`, { params });
  }

  getTravelTimeTrends(mapGuid: string, year?: number): Observable<TravelTimeTrendsData> {
    let params = new HttpParams();
    if (year) params = params.set('year', year.toString());
    
    return this.http.get<TravelTimeTrendsData>(`api/analytics/travel-time-trends/${mapGuid}`, { params });
  }

  getIntegrationStats(mapGuid: string, year?: number): Observable<IntegrationStatsData> {
    let params = new HttpParams();
    if (year) params = params.set('year', year.toString());
    
    return this.http.get<IntegrationStatsData>(`api/analytics/integration-stats/${mapGuid}`, { params });
  }

  getCoverageOverview(mapGuid: string, year?: number): Observable<CoverageOverviewData> {
    let params = new HttpParams();
    if (year) params = params.set('year', year.toString());
    
    return this.http.get<CoverageOverviewData>(`api/analytics/coverage-overview/${mapGuid}`, { params });
  }

  exportToCsv(data: any[], filename: string): void {
    const csv = this.convertToCSV(data);
    this.downloadFile(csv, filename, 'text/csv');
  }

  exportChartAsPNG(canvasElement: HTMLCanvasElement, filename: string): void {
    canvasElement.toBlob((blob) => {
      if (blob) {
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = filename;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);
      }
    }, 'image/png');
  }

  private convertToCSV(data: any[]): string {
    if (!data || data.length === 0) return '';
    
    const keys = Object.keys(data[0]);
    const csvContent = [
      keys.join(','),
      ...data.map(item => keys.map(key => this.formatCSVField(item[key])).join(','))
    ].join('\n');
    
    return csvContent;
  }

  private formatCSVField(field: any): string {
    if (field === null || field === undefined) return '';
    if (typeof field === 'string' && (field.includes(',') || field.includes('\n') || field.includes('"'))) {
      return `"${field.replace(/"/g, '""')}"`;
    }
    return field.toString();
  }

  private downloadFile(content: string, filename: string, mimeType: string): void {
    const blob = new Blob([content], { type: mimeType });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
  }
}