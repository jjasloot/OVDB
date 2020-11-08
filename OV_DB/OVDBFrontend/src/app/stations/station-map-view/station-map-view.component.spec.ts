import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { StationMapViewComponent } from './station-map-view.component';

describe('StationMapViewComponent', () => {
  let component: StationMapViewComponent;
  let fixture: ComponentFixture<StationMapViewComponent>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ StationMapViewComponent ]
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
