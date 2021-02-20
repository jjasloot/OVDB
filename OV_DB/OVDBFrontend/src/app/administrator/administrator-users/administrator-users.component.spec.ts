import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';

import { AdministratorUsersComponent } from './administrator-users.component';

describe('AdministratorUsersComponent', () => {
  let component: AdministratorUsersComponent;
  let fixture: ComponentFixture<AdministratorUsersComponent>;

  beforeEach(waitForAsync(() => {
    TestBed.configureTestingModule({
      declarations: [ AdministratorUsersComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(AdministratorUsersComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
