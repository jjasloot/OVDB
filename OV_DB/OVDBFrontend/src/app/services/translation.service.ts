import { Injectable, EventEmitter, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

type Language = 'nl' | 'en';

@Injectable({
  providedIn: 'root'
})
export class TranslationService {
  private translateService = inject(TranslateService);

  languageChanged = new EventEmitter<Language>();
  constructor() {
    const saved = localStorage.getItem('OVDBLanguage');
    if (saved === 'nl' || saved === 'en') {
      this.language = saved;
    } else if (navigator.language.includes('nl')) {
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
    localStorage.setItem('OVDBLanguage', value);
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
    // Keep in sync with the locales registered in main.ts (nl-NL / en-GB).
    return 'en-GB';
  }



}
