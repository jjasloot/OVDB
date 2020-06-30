import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { RouteInstancesEditComponent } from './route-instances-edit.component';

describe('RouteInstancesEditComponent', () => {
  let component: RouteInstancesEditComponent;
  let fixture: ComponentFixture<RouteInstancesEditComponent>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ RouteInstancesEditComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(RouteInstancesEditComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
