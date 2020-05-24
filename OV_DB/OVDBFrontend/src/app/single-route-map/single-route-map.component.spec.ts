import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { SingleRouteMapComponent } from './single-route-map.component';

describe('SingleRouteMapComponent', () => {
  let component: SingleRouteMapComponent;
  let fixture: ComponentFixture<SingleRouteMapComponent>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ SingleRouteMapComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(SingleRouteMapComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
