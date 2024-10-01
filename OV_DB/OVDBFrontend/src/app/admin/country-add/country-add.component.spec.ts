import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';

import { CountryAddComponent } from './country-add.component';

describe('CountryAddComponent', () => {
  let component: CountryAddComponent;
  let fixture: ComponentFixture<CountryAddComponent>;

  beforeEach(waitForAsync(() => {
    TestBed.configureTestingModule({
    imports: [CountryAddComponent]
})
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(CountryAddComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
