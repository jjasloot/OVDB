import { Injectable, EventEmitter } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

type Language = 'nl' | 'en';

@Injectable({
  providedIn: 'root'
})
export class TranslationService {
  languageChanged = new EventEmitter<Language>();
  constructor(private translateService: TranslateService) {
    if (navigator.language.includes('nl')) {
      this.language = 'nl';
    } else {
      this.language = 'en';
    }
  }

  get language() {
    return this._language;
  }
  set language(value: Language) {
    this._language = value;
    this.translateService.use(value);
    this.languageChanged.emit(this._language);
  }

   private _language: 'nl' | 'en' = 'nl';

  getNameForItem(item: { name: string, nameNL: string }) {
    if (!item) {
      return '';
    }
    if (this.language === 'nl' && !!item.nameNL) {
      return item.nameNL;
    }
    return item.name;
  }

  get dateLocale() {
    if (this.language === 'nl') {
      return 'nl-NL';
    }
    return 'en-US';
  }



}
