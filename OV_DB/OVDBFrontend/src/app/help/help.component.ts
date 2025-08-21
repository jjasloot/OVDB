import { Component, OnInit, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { TranslationService } from '../services/translation.service';

import { TranslateModule } from '@ngx-translate/core';

@Component({
    selector: 'app-help',
    templateUrl: './help.component.html',
    styleUrls: ['./help.component.scss'],
    imports: [TranslateModule]
})
export class HelpComponent implements OnInit {
  private httpClient = inject(HttpClient);
  private translationService = inject(TranslationService);

  changelog: string;

  ngOnInit(): void {
    this.translationService.languageChanged.subscribe(() => {
      this.getChangelog();
    });
    this.getChangelog();
  }


  private getChangelog(): any {
    return this.httpClient.get('assets/changelog_' + this.translationService.language + '.txt',
      { responseType: 'text' as 'json' }).subscribe((data: string) => {
        this.changelog = data;
      });
  }
}
