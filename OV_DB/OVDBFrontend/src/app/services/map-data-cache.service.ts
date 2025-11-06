import { Injectable } from '@angular/core';
import { MapDataDTO } from '../models/map-data.model';

interface CachedMapData {
  data: MapDataDTO;
  timestamp: number;
}

@Injectable({
  providedIn: 'root'
})
export class MapDataCacheService {
  private cache = new Map<string, CachedMapData>();
  private readonly CACHE_DURATION = 5 * 60 * 1000; // 5 minutes

  getCacheKey(
    guid: string,
    filter: string,
    language: string,
    includeLineColours: boolean,
    limitToSelectedArea: boolean
  ): string {
    return `${guid}|${filter}|${language}|${includeLineColours}|${limitToSelectedArea}`;
  }

  get(key: string): MapDataDTO | null {
    const cached = this.cache.get(key);
    
    if (!cached) {
      return null;
    }
    
    // Check if expired
    if (Date.now() - cached.timestamp > this.CACHE_DURATION) {
      this.cache.delete(key);
      return null;
    }
    
    return cached.data;
  }

  set(key: string, data: MapDataDTO): void {
    this.cache.set(key, {
      data,
      timestamp: Date.now()
    });
  }

  clear(): void {
    this.cache.clear();
  }

  // Get cache age in seconds for a key (for UI display)
  getCacheAge(key: string): number | null {
    const cached = this.cache.get(key);
    if (!cached) return null;
    return Math.floor((Date.now() - cached.timestamp) / 1000);
  }
}
