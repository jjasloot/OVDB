import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { RouteTypesAddComponent } from './route-types-add.component';

describe('RouteTypesAddComponent', () => {
  let component: RouteTypesAddComponent;
  let fixture: ComponentFixture<RouteTypesAddComponent>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ RouteTypesAddComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(RouteTypesAddComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
