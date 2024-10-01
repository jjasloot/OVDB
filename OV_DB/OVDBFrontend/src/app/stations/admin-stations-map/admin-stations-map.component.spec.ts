import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';

import { AdminStationsMapComponent } from './admin-stations-map.component';

describe('AdminStationsMapComponent', () => {
  let component: AdminStationsMapComponent;
  let fixture: ComponentFixture<AdminStationsMapComponent>;

  beforeEach(waitForAsync(() => {
    TestBed.configureTestingModule({
    imports: [AdminStationsMapComponent]
})
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(AdminStationsMapComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
