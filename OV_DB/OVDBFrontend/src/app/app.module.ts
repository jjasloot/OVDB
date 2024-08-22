import { BrowserModule } from "@angular/platform-browser";
import { NgModule, LOCALE_ID } from "@angular/core";
import { AppRoutingModule } from "./app-routing.module";
import { AppComponent } from "./app.component";
import { BrowserAnimationsModule } from "@angular/platform-browser/animations";
import { MatButtonModule } from "@angular/material/button";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { MatChipsModule } from "@angular/material/chips";
import { MAT_DATE_LOCALE } from "@angular/material/core";
import { MatDatepickerModule } from "@angular/material/datepicker";
import { MatDialogModule } from "@angular/material/dialog";
import { MatExpansionModule } from "@angular/material/expansion";
import { MatIconModule } from "@angular/material/icon";
import { MatInputModule } from "@angular/material/input";
import { MatListModule } from "@angular/material/list";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatSelectModule } from "@angular/material/select";
import { MatTabsModule } from "@angular/material/tabs";
import { MatToolbarModule } from "@angular/material/toolbar";
import {
  MatMomentDateModule,
  MAT_MOMENT_DATE_ADAPTER_OPTIONS,
} from "@angular/material-moment-adapter";
import { FormsModule, ReactiveFormsModule } from "@angular/forms";
import { LeafletModule } from "@asymmetrik/ngx-leaflet";
import {
  HttpClientModule,
  HTTP_INTERCEPTORS,
  HttpClient,
} from "@angular/common/http";
import { MapComponent } from "./map/map.component";
import { FillMissingDataListComponent } from "./admin/fill-missing-data-list/fill-missing-data-list.component";
import { RouteDetailComponent } from "./admin/route-detail/route-detail.component";
import { AdminComponent } from "./admin/admin.component";
import { RoutesListComponent } from "./admin/routes-list/routes-list.component";
import { RoutesComponent } from "./admin/routes/routes.component";
import { registerLocaleData, DatePipe } from "@angular/common";
import localeNl from "@angular/common/locales/nl";
import { RouteAddComponent } from "./admin/route-add/route-add.component";
import { MapFilterComponent } from "./map-filter/map-filter.component";
import { LoginComponent } from "./login/login.component";
import { AuthInterceptor } from "./guards/auth.interceptor";
import { HomeComponent } from "./home/home.component";
import { MapViewComponent } from "./map-view/map-view.component";
import { LayoutComponent } from "./layout/layout.component";
import { CountriesComponent } from "./admin/countries/countries.component";
import { CountryAddComponent } from "./admin/country-add/country-add.component";
import { AreYouSureDialogComponent } from "./are-you-sure-dialog/are-you-sure-dialog.component";
import { RouteTypesComponent } from "./admin/route-types/route-types.component";
import { RouteTypesAddComponent } from "./admin/route-types-add/route-types-add.component";
import { ClipboardModule } from "@angular/cdk/clipboard";
import { MatTooltipModule } from "@angular/material/tooltip";
import { MatTableModule } from "@angular/material/table";
import {
  MatPaginatorModule,
  MatPaginatorIntl,
} from "@angular/material/paginator";
import { MatSortModule } from "@angular/material/sort";
import { PaginatorIntlService } from "./admin/data-sources/mat-paginator-intl-nl";
import { RegistrationComponent } from "./registration/registration.component";
import { MapsListComponent } from "./admin/maps-list/maps-list.component";
import { MapsAddComponent } from "./admin/maps-add/maps-add.component";
import { HelpComponent } from "./help/help.component";
import { LinkComponent } from "./link/link.component";
import { SortItemsDialogComponent } from "./admin/sort-items-dialog/sort-items-dialog.component";
import { DragDropModule } from "@angular/cdk/drag-drop";
import {
  TranslateModule,
  TranslateLoader,
  TranslateService,
} from "@ngx-translate/core";
import { TranslateHttpLoader } from "@ngx-translate/http-loader";
import { WizzardStep1Component } from "./admin/wizzard/wizard-step1/wizard-step1.component";
import { WizzardStep2Component } from "./admin/wizzard/wizard-step2/wizard-step2.component";
import { SingleRouteMapComponent } from "./single-route-map/single-route-map.component";
import { AdministratorLayoutComponent } from "./administrator/administrator-layout/administrator-layout.component";
import { AdministratorUsersComponent } from "./administrator/administrator-users/administrator-users.component";
import { AdministratorMapsComponent } from "./administrator/administrator-maps/administrator-maps.component";
import { EditMultipleComponent } from "./admin/edit-multiple/edit-multiple.component";
import { RouteInstancesComponent } from "./admin/route-instances/route-instances.component";
import { RouteInstancesEditComponent } from "./admin/route-instances-edit/route-instances-edit.component";
import { MapInstanceDialogComponent } from "./map-instance-dialog/map-instance-dialog.component";
import { StatsComponent } from "./stats/stats.component";
import "hammerjs";
import "chartjs-plugin-zoom";
import { MatAutocompleteModule } from "@angular/material/autocomplete";
import { MapsListBottomsheetComponent } from "./admin/maps-list/maps-list-bottomsheet/maps-list-bottomsheet.component";

import { MatBottomSheetModule } from "@angular/material/bottom-sheet";
import { RoutesListBottomsheetComponent } from "./admin/routes-list/routes-list-bottomsheet/routes-list-bottomsheet.component";
import { MatCardModule } from "@angular/material/card";
import { StationMapComponent } from "./stations/station-map/station-map.component";
import { StationMapViewComponent } from "./stations/station-map-view/station-map-view.component";
import { AdminStationsMapComponent } from "./stations/admin-stations-map/admin-stations-map.component";
import { ImageCreatorComponent } from "./image-creator/image-creator.component";
import { StationMapsComponent } from "./admin/station-maps/station-maps.component";
import { StationMapsEditComponent } from "./admin/station-maps-edit/station-maps-edit.component";
import { LeafletMarkerClusterModule } from "@asymmetrik/ngx-leaflet-markercluster";
import { AdministratorRegionsComponent } from "./administrator/administrator-regions/administrator-regions.component";
import { AdministratorNewRegionComponent } from "./administrator/administrator-new-region/administrator-new-region.component";
import { RequestsListComponent } from "./requests/requests-list/requests-list.component";
import { AdministratorRequestsComponent } from './administrator/administrator-requests/administrator-requests.component';
import { AdministratorOperatorsComponent } from './administrator/administrator-operators/administrator-operators.component';

registerLocaleData(localeNl, "nl-NL");
export function HttpLoaderFactory(http: HttpClient) {
  return new TranslateHttpLoader(http);
}
@NgModule({
  declarations: [
    AppComponent,
    MapComponent,
    FillMissingDataListComponent,
    RouteDetailComponent,
    AdminComponent,
    RoutesListComponent,
    RoutesComponent,
    RouteAddComponent,
    MapFilterComponent,
    LoginComponent,
    HomeComponent,
    MapViewComponent,
    LayoutComponent,
    CountriesComponent,
    CountryAddComponent,
    AreYouSureDialogComponent,
    RouteTypesComponent,
    RouteTypesAddComponent,
    RegistrationComponent,
    MapsListComponent,
    MapsAddComponent,
    HelpComponent,
    LinkComponent,
    SortItemsDialogComponent,
    WizzardStep1Component,
    WizzardStep2Component,
    SingleRouteMapComponent,
    AdministratorLayoutComponent,
    AdministratorUsersComponent,
    AdministratorMapsComponent,
    EditMultipleComponent,
    RouteInstancesComponent,
    RouteInstancesEditComponent,
    MapInstanceDialogComponent,
    StatsComponent,
    MapsListBottomsheetComponent,
    RoutesListBottomsheetComponent,
    StationMapComponent,
    StationMapViewComponent,
    AdminStationsMapComponent,
    ImageCreatorComponent,
    StationMapsComponent,
    StationMapsEditComponent,
    AdministratorRegionsComponent,
    AdministratorNewRegionComponent,
    RequestsListComponent,
    AdministratorRequestsComponent,
    AdministratorOperatorsComponent,
  ],
  imports: [
    BrowserModule,
    AppRoutingModule,
    BrowserAnimationsModule,
    HttpClientModule,
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
    LeafletMarkerClusterModule,
  ],
  providers: [
    { provide: MAT_MOMENT_DATE_ADAPTER_OPTIONS, useValue: { useUtc: true } },
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
    DatePipe,
  ],
  bootstrap: [AppComponent],
})
export class AppModule {}
