import { Component, OnInit } from '@angular/core';
import { UntypedFormBuilder, Validators, UntypedFormGroup, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { AuthenticationService } from '../services/authentication.service';
import { ActivatedRoute, Data } from '@angular/router';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatButton } from '@angular/material/button';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { TranslateModule } from '@ngx-translate/core';

@Component({
    selector: 'app-login',
    templateUrl: './login.component.html',
    styleUrls: ['./login.component.scss'],
    imports: [FormsModule, ReactiveFormsModule, MatFormField, MatLabel, MatInput, MatButton, MatProgressSpinner, TranslateModule]
})
export class LoginComponent implements OnInit {
  form: UntypedFormGroup;
  failed: boolean;
  loading: boolean;
  error: any;

  constructor(
    private authService: AuthenticationService,
    private formBuilder: UntypedFormBuilder,
    private activatedRoute: ActivatedRoute) { }

  ngOnInit() {
    if (this.authService.isLoggedIn) {
      if (!this.authService.autoUpdateRunning) {
        this.authService.refreshTheToken();
      }
    }
    this.activatedRoute.data.subscribe((data: Data) => {
      if (!!data.failed) {
        this.failed = true;
      }
    });

    this.form = this.formBuilder.group({
      email: ['', [Validators.required, Validators.email]],
      password: ['', Validators.required]
    });
  }

  submit() {
    this.loading = true;
    if (this.form.valid) {
      this.authService.login(this.form.value.email, this.form.value.password).subscribe(() => {
        this.loading = false;
      },
        err => { this.error = err; this.loading = false; this.failed = true; });
    }
  }
}

