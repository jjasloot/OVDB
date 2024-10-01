import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';

import { StationMapViewComponent } from './station-map-view.component';

describe('StationMapViewComponent', () => {
  let component: StationMapViewComponent;
  let fixture: ComponentFixture<StationMapViewComponent>;

  beforeEach(waitForAsync(() => {
    TestBed.configureTestingModule({
    imports: [StationMapViewComponent]
})
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(StationMapViewComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
