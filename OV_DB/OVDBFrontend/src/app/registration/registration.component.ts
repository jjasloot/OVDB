import { Component, OnInit } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { AuthenticationService } from '../services/authentication.service';
import { RegistrationRequest } from '../models/registrationRequest.model';

@Component({
  selector: 'app-registration',
  templateUrl: './registration.component.html',
  styleUrls: ['./registration.component.scss']
})
export class RegistrationComponent implements OnInit {
  form = this.formBuilder.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
    // inviteCode: ['', Validators.required]
  });
  failed = false;
  error = '';
  constructor(private formBuilder: FormBuilder, private authService: AuthenticationService) { }

  ngOnInit(): void {
  }
  submit() {
    if (this.form.valid) {
      this.authService.registration(this.form.value as RegistrationRequest).subscribe(() => { },
        err => { this.error = err.error, this.failed = true; });
    }
  }
}
