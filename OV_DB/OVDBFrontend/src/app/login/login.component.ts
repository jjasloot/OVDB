import { Component, OnInit, inject } from '@angular/core';
import { UntypedFormBuilder, Validators, UntypedFormGroup, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { AuthenticationService } from '../services/authentication.service';
import { ActivatedRoute, Data, RouterLink } from '@angular/router';
import { MatFormField, MatLabel, MatError } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatButton } from '@angular/material/button';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { TranslateModule } from '@ngx-translate/core';
import { UserPreferenceService } from '../services/user-preference.service';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss'],
  imports: [FormsModule, ReactiveFormsModule, MatFormField, MatLabel, MatError, MatInput, MatButton, MatProgressSpinner, TranslateModule, RouterLink]
})
export class LoginComponent implements OnInit {
  private authService = inject(AuthenticationService);
  private formBuilder = inject(UntypedFormBuilder);
  private userPreferenceService = inject(UserPreferenceService);
  private activatedRoute = inject(ActivatedRoute);

  form: UntypedFormGroup;
  failed: boolean;
  loading: boolean;
  error: any;

  ngOnInit() {
    if (this.authService.isLoggedIn) {
      if (!this.authService.autoUpdateRunning) {
        this.authService.refreshTheToken();
      }
    }
    this.activatedRoute.data.subscribe((data: Data) => {
      if (data.failed) {
        this.failed = true;
      }
    });

    this.form = this.formBuilder.group({
      email: ['', [Validators.required, Validators.email]],
      password: ['', Validators.required]
    });
  }

  submit() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.loading = true;
    this.failed = false;
    this.authService.login(this.form.value.email, this.form.value.password).subscribe({
      next: () => {
        this.loading = false;
        this.userPreferenceService.applyUserLanguagePreference();
      },
      error: (err) => {
        this.error = err;
        this.loading = false;
        this.failed = true;
      },
    });
  }
}

