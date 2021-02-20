import { Injectable, EventEmitter } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

@Injectable({
  providedIn: 'root'
})
export class TranslationService {
  languageChanged = new EventEmitter<null>();
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
  set language(value: 'nl' | 'en') {
    this._language = value;
    this.translateService.use(value);
    this.languageChanged.emit(null);
  }

  // tslint:disable-next-line: variable-name
  private _language: 'nl' | 'en' = 'nl';

  getNameForItem(item: any) {
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
