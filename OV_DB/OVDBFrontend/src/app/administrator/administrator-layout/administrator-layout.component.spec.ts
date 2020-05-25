import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { AdministratorLayoutComponent } from './administrator-layout.component';

describe('AdministratorLayoutComponent', () => {
  let component: AdministratorLayoutComponent;
  let fixture: ComponentFixture<AdministratorLayoutComponent>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ AdministratorLayoutComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(AdministratorLayoutComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
