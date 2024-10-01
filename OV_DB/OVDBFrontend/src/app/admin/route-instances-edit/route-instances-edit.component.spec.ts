import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';

import { RouteInstancesEditComponent } from './route-instances-edit.component';

describe('RouteInstancesEditComponent', () => {
  let component: RouteInstancesEditComponent;
  let fixture: ComponentFixture<RouteInstancesEditComponent>;

  beforeEach(waitForAsync(() => {
    TestBed.configureTestingModule({
    imports: [RouteInstancesEditComponent]
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
