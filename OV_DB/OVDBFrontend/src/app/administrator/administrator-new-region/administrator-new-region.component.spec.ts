import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AdministratorNewRegionComponent } from './administrator-new-region.component';

describe('AdministratorNewRegionComponent', () => {
  let component: AdministratorNewRegionComponent;
  let fixture: ComponentFixture<AdministratorNewRegionComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdministratorNewRegionComponent]
    })
    .compileComponents();
    
    fixture = TestBed.createComponent(AdministratorNewRegionComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
