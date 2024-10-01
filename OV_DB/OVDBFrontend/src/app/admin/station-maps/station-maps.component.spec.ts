import { ComponentFixture, TestBed } from '@angular/core/testing';

import { StationMapsComponent } from './station-maps.component';

describe('StationMapsComponent', () => {
  let component: StationMapsComponent;
  let fixture: ComponentFixture<StationMapsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
    imports: [StationMapsComponent]
})
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(StationMapsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
