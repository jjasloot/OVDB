import { Component, OnInit } from '@angular/core';
import { FormBuilder, Validators, FormGroup } from '@angular/forms';
import { AuthenticationService } from '../services/authentication.service';
import { ActivatedRoute, Data } from '@angular/router';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss']
})
export class LoginComponent implements OnInit {
  form: FormGroup;
  failed: boolean;
  loading: boolean;
  error: any;

  constructor(
    private authService: AuthenticationService,
    private formBuilder: FormBuilder,
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

