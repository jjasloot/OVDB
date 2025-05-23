import { ComponentFixture, TestBed } from '@angular/core/testing';

import { TimeStatsComponent } from './time-stats.component';

describe('TimeStatsComponent', () => {
  let component: TimeStatsComponent;
  let fixture: ComponentFixture<TimeStatsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TimeStatsComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(TimeStatsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
