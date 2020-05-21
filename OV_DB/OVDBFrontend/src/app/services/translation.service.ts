import { Injectable, EventEmitter } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

@Injectable({
  providedIn: 'root'
})
export class TranslationService {
  languageChanged = new EventEmitter<null>();
  constructor(private translateService: TranslateService) {
    console.log(navigator.language)
    if (navigator.language.includes('nl')) {
      this.language = 'nl';
    } else {
      this.language = 'en'
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

  private _language: 'nl' | 'en' = 'nl';

  getNameForItem(item: any) {
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
