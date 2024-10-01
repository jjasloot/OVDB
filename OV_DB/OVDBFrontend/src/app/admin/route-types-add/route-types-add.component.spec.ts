import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';

import { RouteTypesAddComponent } from './route-types-add.component';

describe('RouteTypesAddComponent', () => {
  let component: RouteTypesAddComponent;
  let fixture: ComponentFixture<RouteTypesAddComponent>;

  beforeEach(waitForAsync(() => {
    TestBed.configureTestingModule({
    imports: [RouteTypesAddComponent]
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
