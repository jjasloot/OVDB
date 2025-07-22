import { Component, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { UserPreferenceService } from './services/user-preference.service';

@Component({
    selector: 'app-root',
    templateUrl: './app.component.html',
    styleUrls: ['./app.component.scss'],
    imports: [RouterOutlet]
})
export class AppComponent implements OnInit {

  constructor(private userPreferenceService: UserPreferenceService) { }

  ngOnInit() {
    // Apply user language preference if logged in
    setTimeout(() => {
      this.userPreferenceService.applyUserLanguagePreference();
    }, 200);
  }

}
