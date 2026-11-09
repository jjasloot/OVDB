import { Injectable, inject } from '@angular/core';
import { LuxonDateAdapter } from '@angular/material-luxon-adapter';
import { TranslationService } from '../services/translation.service';

/** The Material Luxon adapter, following the app's language as it changes. */
@Injectable()
export class DynamicLuxonDateAdapter extends LuxonDateAdapter {
    private translationService = inject(TranslationService);

    constructor() {
        super();
        this.translationService.languageChanged.subscribe(() => this.setLocale(this.translationService.dateLocale));
        this.setLocale(this.translationService.dateLocale);
    }
}
