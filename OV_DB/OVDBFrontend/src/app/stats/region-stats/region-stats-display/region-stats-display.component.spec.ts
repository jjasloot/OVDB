import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RegionStatsDisplayComponent } from './region-stats-display.component';

describe('RegionStatsDisplayComponent', () => {
  let component: RegionStatsDisplayComponent;
  let fixture: ComponentFixture<RegionStatsDisplayComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RegionStatsDisplayComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(RegionStatsDisplayComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
