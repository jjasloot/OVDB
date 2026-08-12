import { Injectable, inject } from '@angular/core';
import { MomentDateAdapter } from '@angular/material-moment-adapter';
import { TranslationService } from '../services/translation.service';
import * as moment from 'moment';
import 'moment/locale/nl';
import 'moment/locale/en-gb';

@Injectable()
export class DynamicMomentDateAdapter extends MomentDateAdapter {
    private translationService = inject(TranslationService);

    constructor() {
        super();
        this.translationService.languageChanged.subscribe(() => {
            const newLocale = this.translationService.dateLocale;
            this.setLocale(newLocale);
            moment.locale(newLocale);
        });
        // Set initial locale
        this.setLocale(this.translationService.dateLocale);
        moment.locale(this.translationService.dateLocale);
    }
}
