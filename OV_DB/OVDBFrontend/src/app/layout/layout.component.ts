import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { AuthenticationService } from '../services/authentication.service';
import { TranslationService } from '../services/translation.service';
import { MatSelectChange } from '@angular/material/select';

@Component({
  selector: 'app-layout',
  templateUrl: './layout.component.html',
  styleUrls: ['./layout.component.scss']
})
export class LayoutComponent implements OnInit {

  constructor(
    private router: Router,
    private authService: AuthenticationService,
    private translationService: TranslationService) { }

  ngOnInit() {
  }
  gotoHome() {
    this.router.navigate(['/']);
  }

  setLanguage(language: MatSelectChange) {
    this.translationService.language = language.value;
  }
  get currentLanguage() {
    return this.translationService.language;
  }
  signOut() {
    this.authService.logOut();
  }
  signIn() {
    this.router.navigate(['/login']);
  }

  get isLoggedIn() {
    return this.authService.isLoggedIn;
  }
  get email() {
    if (!this.isLoggedIn) {
      return '';
    }
    return this.authService.email;
  }

  get isAdmin(){
    if (!this.isLoggedIn){
      return false;
    }
    return this.authService.admin;
  }
}
