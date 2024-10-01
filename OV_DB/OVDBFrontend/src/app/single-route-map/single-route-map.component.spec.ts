import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';

import { SingleRouteMapComponent } from './single-route-map.component';

describe('SingleRouteMapComponent', () => {
  let component: SingleRouteMapComponent;
  let fixture: ComponentFixture<SingleRouteMapComponent>;

  beforeEach(waitForAsync(() => {
    TestBed.configureTestingModule({
    imports: [SingleRouteMapComponent]
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
