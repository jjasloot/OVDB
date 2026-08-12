import { Component, OnInit, inject, ChangeDetectionStrategy } from '@angular/core';
import { UntypedFormBuilder, Validators, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { AuthenticationService } from '../services/authentication.service';
import { RegistrationRequest } from '../models/registrationRequest.model';
import { MatFormField, MatLabel, MatError } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatButton } from '@angular/material/button';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { TranslateModule, TranslateService } from '@ngx-translate/core';

@Component({
    selector: 'app-registration',
    templateUrl: './registration.component.html',
    styleUrls: ['./registration.component.scss'],
    changeDetection: ChangeDetectionStrategy.Eager,
    imports: [FormsModule, ReactiveFormsModule, MatFormField, MatLabel, MatError, MatInput, MatButton, MatProgressSpinner, TranslateModule]
})
export class RegistrationComponent implements OnInit {
  private formBuilder = inject(UntypedFormBuilder);
  private authService = inject(AuthenticationService);
  private translateService = inject(TranslateService);

  form = this.formBuilder.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
    // inviteCode: ['', Validators.required]
  });
  failed = false;
  loading = false;
  error = '';

  ngOnInit(): void {
  }
  submit() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.loading = true;
    this.failed = false;
    // On success the AuthenticationService logs the new user in and navigates away.
    this.authService.registration(this.form.value as RegistrationRequest).subscribe({
      next: () => (this.loading = false),
      error: (err) => {
        this.error = typeof err?.error === 'string' && err.error
          ? err.error
          : this.translateService.instant('ERRORS.GENERIC');
        this.failed = true;
        this.loading = false;
      },
    });
  }
}
