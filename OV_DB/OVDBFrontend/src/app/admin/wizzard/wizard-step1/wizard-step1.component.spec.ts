import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';

import { WizzardStep1Component } from './wizard-step1.component';

describe('WizzardStep1Component', () => {
  let component: WizzardStep1Component;
  let fixture: ComponentFixture<WizzardStep1Component>;

  beforeEach(waitForAsync(() => {
    TestBed.configureTestingModule({
      declarations: [ WizzardStep1Component ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(WizzardStep1Component);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
