import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';

import { RouteAddComponent } from './route-add.component';

describe('RouteAddComponent', () => {
  let component: RouteAddComponent;
  let fixture: ComponentFixture<RouteAddComponent>;

  beforeEach(waitForAsync(() => {
    TestBed.configureTestingModule({
      declarations: [ RouteAddComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(RouteAddComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
