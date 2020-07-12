import { TestBed } from '@angular/core/testing';

import { DataUpdateService } from './data-update.service';

describe('DataUpdateService', () => {
  beforeEach(() => TestBed.configureTestingModule({}));

  it('should be created', () => {
    const service: DataUpdateService = TestBed.inject(DataUpdateService);
    expect(service).toBeTruthy();
  });
});
