import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';

import { AdministratorMapsComponent } from './administrator-maps.component';

describe('AdministratorMapsComponent', () => {
  let component: AdministratorMapsComponent;
  let fixture: ComponentFixture<AdministratorMapsComponent>;

  beforeEach(waitForAsync(() => {
    TestBed.configureTestingModule({
      declarations: [ AdministratorMapsComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(AdministratorMapsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
