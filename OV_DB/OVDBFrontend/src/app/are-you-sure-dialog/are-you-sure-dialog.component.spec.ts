import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';

import { AreYouSureDialogComponent } from './are-you-sure-dialog.component';

describe('AreYouSureDialogComponent', () => {
  let component: AreYouSureDialogComponent;
  let fixture: ComponentFixture<AreYouSureDialogComponent>;

  beforeEach(waitForAsync(() => {
    TestBed.configureTestingModule({
      declarations: [ AreYouSureDialogComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(AreYouSureDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
