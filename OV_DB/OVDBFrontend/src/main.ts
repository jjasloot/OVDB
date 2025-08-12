import { enableProdMode, importProvidersFrom, LOCALE_ID } from "@angular/core";
import { environment } from "./environments/environment";
import {
  MatMomentDateModule,
  MAT_MOMENT_DATE_FORMATS,
  MAT_MOMENT_DATE_ADAPTER_OPTIONS
} from "@angular/material-moment-adapter";
import { DateAdapter, MAT_DATE_FORMATS } from '@angular/material/core';
import { TranslationService } from "./app/services/translation.service";
import { DynamicMomentDateAdapter } from "./app/adapters/dynamic-moment-date-adapter";
import {
  HTTP_INTERCEPTORS,
  provideHttpClient,
  withInterceptorsFromDi,
  HttpClient,
} from "@angular/common/http";
import { AuthInterceptor } from "./app/guards/auth.interceptor";
import {
  MatPaginatorIntl,
  MatPaginatorModule,
} from "@angular/material/paginator";
import { PaginatorIntlService } from "./app/admin/data-sources/mat-paginator-intl-nl";
import {
  TranslateService,
  TranslateModule,
  TranslateLoader,
} from "@ngx-translate/core";
import { DatePipe, registerLocaleData } from "@angular/common";
import { BrowserModule, bootstrapApplication } from "@angular/platform-browser";
import { provideAnimations } from "@angular/platform-browser/animations";
import { FormsModule, ReactiveFormsModule } from "@angular/forms";
import { LeafletModule } from "@bluehalo/ngx-leaflet";
import { MatToolbarModule } from "@angular/material/toolbar";
import { MatButtonModule } from "@angular/material/button";
import { MatDialogModule } from "@angular/material/dialog";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { MatDatepickerModule } from "@angular/material/datepicker";
import { MatTabsModule } from "@angular/material/tabs";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import { MatListModule } from "@angular/material/list";
import { MatChipsModule } from "@angular/material/chips";
import { MatExpansionModule } from "@angular/material/expansion";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatIconModule } from "@angular/material/icon";
import { ClipboardModule } from "@angular/cdk/clipboard";
import { MatTooltipModule } from "@angular/material/tooltip";
import { MatTableModule } from "@angular/material/table";
import { MatSortModule } from "@angular/material/sort";
import { DragDropModule } from "@angular/cdk/drag-drop";
import { MatAutocompleteModule } from "@angular/material/autocomplete";
import { MatBottomSheetModule } from "@angular/material/bottom-sheet";
import { MatCardModule } from "@angular/material/card";
import { LeafletMarkerClusterModule } from "@bluehalo/ngx-leaflet-markercluster";
import { AppComponent } from "./app/app.component";
import { TranslateHttpLoader } from "@ngx-translate/http-loader";
import localeNl from "@angular/common/locales/nl";
import localeEnGB from "@angular/common/locales/en-GB";
import "hammerjs";
import "chartjs-plugin-zoom";
import { provideRouter } from "@angular/router";
import { routes } from "./app/app.routes";
import { provideCharts, withDefaultRegisterables } from "ng2-charts";
import { MAT_DATE_LOCALE } from "@angular/material/core";

registerLocaleData(localeNl, "nl-NL");
registerLocaleData(localeEnGB, "en-GB");

function translateLanguage(lang: string) {
  console.log(lang);
  switch (lang) {
    case "nl":
      return "nl";
    default:
      return "en-GB";
  }
}

export class LocaleId extends String {
  constructor(private translateSevice: TranslateService) {
    super();
  }

  override toString(): string {
    return translateLanguage(this.translateSevice.currentLang);
  }

  override valueOf(): string {
    return this.toString();
  }
}


export function HttpLoaderFactory(http: HttpClient) {
  return new TranslateHttpLoader(http);
}

if (environment.production) {
  enableProdMode();
}



bootstrapApplication(AppComponent, {
  providers: [
    importProvidersFrom(
      BrowserModule,
      FormsModule,
      ReactiveFormsModule,
      LeafletModule,
      MatToolbarModule,
      MatButtonModule,
      MatDialogModule,
      MatCheckboxModule,
      MatDatepickerModule,
      MatMomentDateModule,
      MatTabsModule,
      MatInputModule,
      MatSelectModule,
      MatListModule,
      MatChipsModule,
      MatExpansionModule,
      MatProgressSpinnerModule,
      MatIconModule,
      MatProgressSpinnerModule,
      ClipboardModule,
      MatTooltipModule,
      MatTableModule,
      MatPaginatorModule,
      MatSortModule,
      DragDropModule,
      TranslateModule.forRoot({
        loader: {
          provide: TranslateLoader,
          useFactory: HttpLoaderFactory,
          deps: [HttpClient],
        },
      }),
      MatAutocompleteModule,
      MatBottomSheetModule,
      MatCardModule,
      LeafletMarkerClusterModule
    ),
    { provide: DateAdapter, useClass: DynamicMomentDateAdapter, deps: [MAT_DATE_LOCALE, TranslationService] },
    { provide: MAT_DATE_FORMATS, useValue: MAT_MOMENT_DATE_FORMATS },
    { provide: HTTP_INTERCEPTORS, useClass: AuthInterceptor, multi: true },
    {
      provide: MatPaginatorIntl,
      useFactory: (translate) => {
        const service = new PaginatorIntlService();
        service.injectTranslateService(translate);
        return service;
      },
      deps: [TranslateService],
    },
    {
      provide: LOCALE_ID,
      useClass: LocaleId,
      deps: [TranslateService],
    },
    {
      provide: MAT_DATE_LOCALE,
      useFactory: (translate: TranslateService) => translateLanguage(translate.currentLang),
      deps: [TranslateService],
    },
    DatePipe,
    provideHttpClient(withInterceptorsFromDi()),
    provideAnimations(),
    provideRouter(routes),
    provideCharts(withDefaultRegisterables())
  ],
}).catch((err) => console.error(err));
