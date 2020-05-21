import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { RouteTypesComponent } from './route-types.component';

describe('RouteTypesComponent', () => {
  let component: RouteTypesComponent;
  let fixture: ComponentFixture<RouteTypesComponent>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ RouteTypesComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(RouteTypesComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
