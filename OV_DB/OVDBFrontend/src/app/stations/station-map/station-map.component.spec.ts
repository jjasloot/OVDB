import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { StationMapComponent } from './station-map.component';

describe('StationMapComponent', () => {
  let component: StationMapComponent;
  let fixture: ComponentFixture<StationMapComponent>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ StationMapComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(StationMapComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
