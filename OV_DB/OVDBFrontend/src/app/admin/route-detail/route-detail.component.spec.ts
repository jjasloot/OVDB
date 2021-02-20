import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';

import { RouteDetailComponent } from './route-detail.component';

describe('FillMissingDataDetailComponent', () => {
  let component: RouteDetailComponent;
  let fixture: ComponentFixture<RouteDetailComponent>;

  beforeEach(waitForAsync(() => {
    TestBed.configureTestingModule({
      declarations: [ RouteDetailComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(RouteDetailComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
