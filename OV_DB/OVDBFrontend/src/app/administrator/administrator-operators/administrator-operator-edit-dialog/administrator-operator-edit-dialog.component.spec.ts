import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AdministratorOperatorEditDialogComponent } from './administrator-operator-edit-dialog.component';

describe('AdministratorOperatorEditDialogComponent', () => {
  let component: AdministratorOperatorEditDialogComponent;
  let fixture: ComponentFixture<AdministratorOperatorEditDialogComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
    imports: [AdministratorOperatorEditDialogComponent]
})
    .compileComponents();
    
    fixture = TestBed.createComponent(AdministratorOperatorEditDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
