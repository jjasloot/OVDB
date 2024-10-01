import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';

import { FillMissingDataListComponent } from './fill-missing-data-list.component';

describe('FillMissingDataListComponent', () => {
  let component: FillMissingDataListComponent;
  let fixture: ComponentFixture<FillMissingDataListComponent>;

  beforeEach(waitForAsync(() => {
    TestBed.configureTestingModule({
    imports: [FillMissingDataListComponent]
})
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(FillMissingDataListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
