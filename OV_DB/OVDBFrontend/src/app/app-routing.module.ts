import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { MapComponent } from './map/map.component';
import { AdminComponent } from './admin/admin.component';
import { FillMissingDataListComponent } from './admin/fill-missing-data-list/fill-missing-data-list.component';
import { RouteDetailComponent } from './admin/route-detail/route-detail.component';
import { RoutesComponent } from './admin/routes/routes.component';
import { LoginComponent } from './login/login.component';
import { LoginGuard } from './guards/login.guard';
import { HomeComponent } from './home/home.component';
import { MapViewComponent } from './map-view/map-view.component';
import { LayoutComponent } from './layout/layout.component';
import { RegistrationComponent } from './registration/registration.component';
import { LinkComponent } from './link/link.component';
import { MapsListComponent } from './admin/maps-list/maps-list.component';
import { RoutesListComponent } from './admin/routes-list/routes-list.component';
import { RouteAddComponent } from './admin/route-add/route-add.component';
import { CountriesComponent } from './admin/countries/countries.component';
import { RouteTypesComponent } from './admin/route-types/route-types.component';
import { HelpComponent } from './help/help.component';
import { WizzardStep1Component } from './admin/wizzard/wizard-step1/wizard-step1.component';
import { WizzardStep2Component } from './admin/wizzard/wizard-step2/wizard-step2.component';


const routes: Routes = [
  {
    path: 'admin',
    component: LayoutComponent,
    canActivate: [LoginGuard],
    children: [
      {
        path: '', component: RoutesComponent, children: [
          { path: 'maps', component: MapsListComponent },
          { path: 'routes', component: RoutesListComponent },
          { path: 'routes/:routeId', component: RouteDetailComponent },
          { path: 'addRoute', component: RouteAddComponent },
          { path: 'countries', component: CountriesComponent },
          { path: 'types', component: RouteTypesComponent },
          { path: 'missingData', component: FillMissingDataListComponent },
          { path: 'wizard/:id', component: WizzardStep2Component },
          { path: 'wizard', component: WizzardStep1Component }
        ]
      },
    ]
  },
  {
    path: '', component: LayoutComponent, children: [
      { path: '', pathMatch: 'full', component: HomeComponent },
      { path: 'login', component: LoginComponent },
      { path: 'login/failed', component: LoginComponent, data: { failed: true } },
      { path: 'register', component: RegistrationComponent },
      { path: 'map/:guid', component: MapViewComponent },
      { path: 'link/:name', component: LinkComponent },
      { path: 'help', component: HelpComponent }
    ]
  }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }