import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AdministratorOperatorsComponent } from './administrator-operators.component';

describe('AdministratorOperatorsComponent', () => {
  let component: AdministratorOperatorsComponent;
  let fixture: ComponentFixture<AdministratorOperatorsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
    imports: [AdministratorOperatorsComponent]
})
    .compileComponents();
    
    fixture = TestBed.createComponent(AdministratorOperatorsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
