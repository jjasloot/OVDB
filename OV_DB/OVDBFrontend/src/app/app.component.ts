import { Component, OnInit, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { UserPreferenceService } from './services/user-preference.service';
import { ThemeService } from './services/theme.service';
@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss'],
  imports: [RouterOutlet]
})
export class AppComponent implements OnInit {
  private userPreferenceService = inject(UserPreferenceService);
  private themeService = inject(ThemeService);

  ngOnInit() {
    // Apply user language preference if logged in
    setTimeout(() => {
      this.userPreferenceService.applyUserLanguagePreference();
    }, 200);
  }

}
