import { Component, OnInit } from '@angular/core';
import { UntypedFormBuilder, UntypedFormGroup, Validators, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { ApiService } from '../services/api.service';
import { UserProfile, UpdateProfile, ChangePassword } from '../models/user-profile.model';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { TranslationService } from '../services/translation.service';
import { MatFormField, MatLabel, MatError } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatButton } from '@angular/material/button';
import { MatSelect, MatOption } from '@angular/material/select';
import { MatCard, MatCardContent, MatCardHeader, MatCardTitle } from '@angular/material/card';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { MatIcon } from '@angular/material/icon';

@Component({
  selector: 'app-profile',
  templateUrl: './profile.component.html',
  styleUrls: ['./profile.component.scss'],
  imports: [
    FormsModule, 
    ReactiveFormsModule, 
    MatFormField, 
    MatLabel, 
    MatError,
    MatInput, 
    MatButton, 
    MatSelect, 
    MatOption, 
    MatCard, 
    MatCardContent, 
    MatCardHeader, 
    MatCardTitle, 
    MatProgressSpinner, 
    MatIcon,
    TranslateModule
  ]
})
export class ProfileComponent implements OnInit {
  profileForm: UntypedFormGroup;
  passwordForm: UntypedFormGroup;
  userProfile: UserProfile | null = null;
  loading = false;
  savingProfile = false;
  changingPassword = false;

  languages = [
    { value: 'en', label: 'English' },
    { value: 'nl', label: 'Nederlands' }
  ];

  constructor(
    private formBuilder: UntypedFormBuilder,
    private apiService: ApiService,
    private snackBar: MatSnackBar,
    private translateService: TranslateService,
    private translationService: TranslationService
  ) {
    this.profileForm = this.formBuilder.group({
      preferredLanguage: ['en', Validators.required],
      telegramUserId: [0]
    });

    this.passwordForm = this.formBuilder.group({
      currentPassword: ['', Validators.required],
      newPassword: ['', [Validators.required, Validators.minLength(10)]],
      confirmPassword: ['', Validators.required]
    }, {
      validators: this.passwordMatchValidator
    });
  }

  ngOnInit(): void {
    this.loadProfile();
  }

  passwordMatchValidator(group: UntypedFormGroup) {
    const newPassword = group.get('newPassword')?.value;
    const confirmPassword = group.get('confirmPassword')?.value;
    return newPassword === confirmPassword ? null : { passwordMismatch: true };
  }

  loadProfile(): void {
    this.loading = true;
    this.apiService.getUserProfile().subscribe({
      next: (profile) => {
        this.userProfile = profile;
        this.profileForm.patchValue({
          preferredLanguage: profile.preferredLanguage || 'en',
          telegramUserId: profile.telegramUserId || 0
        });
        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading profile:', error);
        this.showMessage('PROFILE.ERROR_LOADING');
        this.loading = false;
      }
    });
  }

  saveProfile(): void {
    if (this.profileForm.valid) {
      this.savingProfile = true;
      const updateProfile: UpdateProfile = {
        preferredLanguage: this.profileForm.value.preferredLanguage,
        telegramUserId: this.profileForm.value.telegramUserId
      };

      this.apiService.updateUserProfile(updateProfile).subscribe({
        next: () => {
          this.showMessage('PROFILE.SAVED_SUCCESSFULLY');
          this.savingProfile = false;
          
          // Update language if changed
          if (updateProfile.preferredLanguage !== this.translationService.language) {
            this.translationService.language = updateProfile.preferredLanguage as 'nl' | 'en';
          }
        },
        error: (error) => {
          console.error('Error saving profile:', error);
          this.showMessage('PROFILE.ERROR_SAVING');
          this.savingProfile = false;
        }
      });
    }
  }

  changePassword(): void {
    if (this.passwordForm.valid) {
      this.changingPassword = true;
      const changePassword: ChangePassword = {
        currentPassword: this.passwordForm.value.currentPassword,
        newPassword: this.passwordForm.value.newPassword
      };

      this.apiService.changePassword(changePassword).subscribe({
        next: () => {
          this.showMessage('PROFILE.PASSWORD_CHANGED_SUCCESSFULLY');
          this.passwordForm.reset();
          this.changingPassword = false;
        },
        error: (error) => {
          console.error('Error changing password:', error);
          const errorMessage = error.error?.message || error.error || 'PROFILE.ERROR_CHANGING_PASSWORD';
          this.showMessage(errorMessage);
          this.changingPassword = false;
        }
      });
    }
  }

  private showMessage(messageKey: string): void {
    this.translateService.get(messageKey).subscribe(message => {
      this.snackBar.open(message, '', { duration: 3000 });
    });
  }
}