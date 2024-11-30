import { Component, OnInit } from '@angular/core';
import { UntypedFormBuilder, Validators, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { AuthenticationService } from '../services/authentication.service';
import { RegistrationRequest } from '../models/registrationRequest.model';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatButton } from '@angular/material/button';
import { TranslateModule } from '@ngx-translate/core';

@Component({
    selector: 'app-registration',
    templateUrl: './registration.component.html',
    styleUrls: ['./registration.component.scss'],
    imports: [FormsModule, ReactiveFormsModule, MatFormField, MatLabel, MatInput, MatButton, TranslateModule]
})
export class RegistrationComponent implements OnInit {
  form = this.formBuilder.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
    // inviteCode: ['', Validators.required]
  });
  failed = false;
  error = '';
  constructor(private formBuilder: UntypedFormBuilder, private authService: AuthenticationService) { }

  ngOnInit(): void {
  }
  submit() {
    if (this.form.valid) {
      this.authService.registration(this.form.value as RegistrationRequest).subscribe(() => { },
        err => { this.error = err.error, this.failed = true; });
    }
  }
}
