import { ComponentFixture, TestBed } from '@angular/core/testing';

import { StationMapsEditComponent } from './station-maps-edit.component';

describe('StationMapsEditComponent', () => {
  let component: StationMapsEditComponent;
  let fixture: ComponentFixture<StationMapsEditComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ StationMapsEditComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(StationMapsEditComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
