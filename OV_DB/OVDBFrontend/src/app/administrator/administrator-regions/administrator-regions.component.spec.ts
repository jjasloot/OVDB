import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AdministratorRegionsComponent } from './administrator-regions.component';

describe('AdministratorRegionsComponent', () => {
  let component: AdministratorRegionsComponent;
  let fixture: ComponentFixture<AdministratorRegionsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdministratorRegionsComponent]
    })
    .compileComponents();
    
    fixture = TestBed.createComponent(AdministratorRegionsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
