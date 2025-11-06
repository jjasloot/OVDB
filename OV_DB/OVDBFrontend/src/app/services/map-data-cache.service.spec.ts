import { TestBed } from '@angular/core/testing';
import { MapDataCacheService } from './map-data-cache.service';
import { MapDataDTO } from '../models/map-data.model';

describe('MapDataCacheService', () => {
  let service: MapDataCacheService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(MapDataCacheService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should generate unique cache keys', () => {
    const key1 = service.getCacheKey('guid1', 'filter1', 'en', true, false);
    const key2 = service.getCacheKey('guid1', 'filter1', 'en', false, false);
    const key3 = service.getCacheKey('guid1', 'filter1', 'nl', true, false);
    
    expect(key1).not.toEqual(key2);
    expect(key1).not.toEqual(key3);
    expect(key2).not.toEqual(key3);
  });

  it('should store and retrieve cached data', () => {
    const mockData: MapDataDTO = {
      routes: { type: 'FeatureCollection', features: [] },
      area: null
    };
    const key = service.getCacheKey('guid1', 'filter1', 'en', true, false);
    
    service.set(key, mockData);
    const retrieved = service.get(key);
    
    expect(retrieved).toEqual(mockData);
  });

  it('should return null for non-existent cache key', () => {
    const retrieved = service.get('non-existent-key');
    expect(retrieved).toBeNull();
  });

  it('should clear all cache', () => {
    const mockData: MapDataDTO = {
      routes: { type: 'FeatureCollection', features: [] },
      area: null
    };
    const key1 = service.getCacheKey('guid1', 'filter1', 'en', true, false);
    const key2 = service.getCacheKey('guid2', 'filter2', 'nl', false, true);
    
    service.set(key1, mockData);
    service.set(key2, mockData);
    
    service.clear();
    
    expect(service.get(key1)).toBeNull();
    expect(service.get(key2)).toBeNull();
  });

  it('should return cache age in seconds', () => {
    const mockData: MapDataDTO = {
      routes: { type: 'FeatureCollection', features: [] },
      area: null
    };
    const key = service.getCacheKey('guid1', 'filter1', 'en', true, false);
    
    service.set(key, mockData);
    const age = service.getCacheAge(key);
    
    expect(age).toBeDefined();
    expect(age).toBeGreaterThanOrEqual(0);
    expect(age).toBeLessThan(2); // Should be less than 2 seconds old
  });

  it('should return null for cache age of non-existent key', () => {
    const age = service.getCacheAge('non-existent-key');
    expect(age).toBeNull();
  });

  it('should expire cache after 5 minutes', (done) => {
    const mockData: MapDataDTO = {
      routes: { type: 'FeatureCollection', features: [] },
      area: null
    };
    const key = service.getCacheKey('guid1', 'filter1', 'en', true, false);
    
    // Mock the timestamp to be 5 minutes and 1 second ago
    service.set(key, mockData);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const cache = (service as any).cache;
    const cached = cache.get(key);
    cached.timestamp = Date.now() - (5 * 60 * 1000 + 1000);
    
    const retrieved = service.get(key);
    expect(retrieved).toBeNull();
    done();
  });
});
