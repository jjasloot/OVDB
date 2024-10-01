import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';

import { RouteInstancesComponent } from './route-instances.component';

describe('RouteInstancesComponent', () => {
  let component: RouteInstancesComponent;
  let fixture: ComponentFixture<RouteInstancesComponent>;

  beforeEach(waitForAsync(() => {
    TestBed.configureTestingModule({
    imports: [RouteInstancesComponent]
})
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(RouteInstancesComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
