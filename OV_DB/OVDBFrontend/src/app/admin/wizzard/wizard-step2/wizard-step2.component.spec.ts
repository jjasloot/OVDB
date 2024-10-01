import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';

import { WizzardStep2Component } from './wizard-step2.component';

describe('WizzardStep2Component', () => {
  let component: WizzardStep2Component;
  let fixture: ComponentFixture<WizzardStep2Component>;

  beforeEach(waitForAsync(() => {
    TestBed.configureTestingModule({
    imports: [WizzardStep2Component]
})
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(WizzardStep2Component);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
