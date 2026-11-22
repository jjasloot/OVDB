import { Routes } from "@angular/router";
import { LoginGuard } from "./guards/login.guard";
import { AdministratorGuard } from "./guards/administrator.guard";
import { LayoutComponent } from "./layout/layout.component";
import { HomeComponent } from "./home/home.component";
import { LoginComponent } from "./login/login.component";

export const routes: Routes = [
  {
    path: "",
    component: LayoutComponent,
    children: [
      { path: "", pathMatch: "full", component: HomeComponent },
      { title: "LOGIN.TITLE", path: "login", component: LoginComponent },
      { title: "LOGIN.TITLE", path: "login/failed", component: LoginComponent, data: { failed: true } },
      { title: "REGISTER.TITLE", path: "register", loadComponent: () => import("./registration/registration.component").then(m => m.RegistrationComponent) },
      { title: "MENU.MAPS", path: "map/:guid", loadComponent: () => import("./map-view/map-view.component").then(m => m.MapViewComponent) },
      { path: "link/:name", loadComponent: () => import("./link/link.component").then(m => m.LinkComponent) },
      { title: "MENU.HELP", path: "help", loadComponent: () => import("./help/help.component").then(m => m.HelpComponent) },
      { path: "route/:routeId/:guid", loadComponent: () => import("./single-route-map/single-route-map.component").then(m => m.SingleRouteMapComponent) },
      { title: "HOME.STATS", path: "stats", loadComponent: () => import("./stats/stats.component").then(m => m.StatsComponent), canActivate: [LoginGuard] },
      { title: "MENU.PROFILE", path: "profile", loadComponent: () => import("./profile/profile.component").then(m => m.ProfileComponent), canActivate: [LoginGuard] },
      { title: "HOME.IMAGES", path: "images", loadComponent: () => import("./image-creator/image-creator.component").then(m => m.ImageCreatorComponent), canActivate: [LoginGuard] },
      {
        path: "admin",
        loadComponent: () => import("./admin/routes/routes.component").then(m => m.RoutesComponent),
        canActivate: [LoginGuard],
        children: [
          { path: "", pathMatch: "full", redirectTo: "maps" },
          { title: "ADMINPAGES.MAPS", path: "maps", loadComponent: () => import("./admin/maps-list/maps-list.component").then(m => m.MapsListComponent) },
          { title: "ADMINPAGES.ROUTES", path: "routes", loadComponent: () => import("./admin/routes-list/routes-list.component").then(m => m.RoutesListComponent) },
          { title: "ROUTES.INSTANCES", path: "route-instances", loadComponent: () => import("./admin/route-instances-list/route-instances-list.component").then(m => m.RouteInstancesListComponent) },
          { title: "ADMINPAGES.ROUTES", path: "routes/:routeId", loadComponent: () => import("./admin/route-detail/route-detail.component").then(m => m.RouteDetailComponent) },
          { title: "ROUTES.INSTANCES", path: "routes/instances/:routeId", loadComponent: () => import("./admin/route-instances/route-instances.component").then(m => m.RouteInstancesComponent) },
          { title: "ADMINPAGES.ADDROUTE", path: "addRoute", loadComponent: () => import("./admin/route-add/route-add.component").then(m => m.RouteAddComponent) },
          { title: "ADMINPAGES.TYPES", path: "types", loadComponent: () => import("./admin/route-types/route-types.component").then(m => m.RouteTypesComponent) },
          { path: "missingData", loadComponent: () => import("./admin/fill-missing-data-list/fill-missing-data-list.component").then(m => m.FillMissingDataListComponent) },
          { title: "ADMINPAGES.WIZARD", path: "wizard/:id", loadComponent: () => import("./admin/wizzard/wizard-step2/wizard-step2.component").then(m => m.WizzardStep2Component) },
          { title: "ADMINPAGES.WIZARD", path: "wizard", loadComponent: () => import("./admin/wizzard/wizard-step1/wizard-step1.component").then(m => m.WizzardStep1Component) },
          { title: "ADMINPAGES.STATIONMAPS", path: "stationmaps", loadComponent: () => import("./admin/station-maps/station-maps.component").then(m => m.StationMapsComponent) },
          { title: "ADMINPAGES.TRAEWELLING", path: "traewelling", loadComponent: () => import("./traewelling/traewelling.component").then(m => m.TrawellingComponent) },
        ],
      },
      {
        path: "stations",
        children: [
          { title: "ADMINPAGES.STATIONMAPS", path: "map/:guid", loadComponent: () => import("./stations/station-map-view/station-map-view.component").then(m => m.StationMapViewComponent), canActivate: [LoginGuard] },
          { title: "ADMINPAGES.STATIONMAPS", path: "link/:name", loadComponent: () => import("./stations/station-map-view/station-map-view.component").then(m => m.StationMapViewComponent) },
          { title: "STATIONS.BACKFILL.LINK", path: "backfill", loadComponent: () => import("./stations/station-backfill/station-backfill.component").then(m => m.StationBackfillComponent), canActivate: [LoginGuard] },
        ],
      },
      {
        path: "administrator",
        title: "MENU.ADMINISTRATOR",
        loadComponent: () => import("./administrator/administrator-layout/administrator-layout.component").then(m => m.AdministratorLayoutComponent),
        canActivate: [LoginGuard, AdministratorGuard],
        children: [
          { path: "", pathMatch: "full", redirectTo: "users" },
          { path: "maps", loadComponent: () => import("./administrator/administrator-maps/administrator-maps.component").then(m => m.AdministratorMapsComponent) },
          { path: "users", loadComponent: () => import("./administrator/administrator-users/administrator-users.component").then(m => m.AdministratorUsersComponent) },
          { path: "stations", loadComponent: () => import("./stations/admin-stations-map/admin-stations-map.component").then(m => m.AdminStationsMapComponent) },
          { path: "regions", loadComponent: () => import("./administrator/administrator-regions/administrator-regions.component").then(m => m.AdministratorRegionsComponent) },
          { path: "requests", loadComponent: () => import("./administrator/administrator-requests/administrator-requests.component").then(m => m.AdministratorRequestsComponent) },
          { path: "operators", loadComponent: () => import("./administrator/administrator-operators/administrator-operators.component").then(m => m.AdministratorOperatorsComponent) },
          { path: "station-merge", loadComponent: () => import("./administrator/administrator-station-merge/administrator-station-merge.component").then(m => m.AdministratorStationMergeComponent) },
        ],
      },
      { title: "MENU.REQUESTS", path: "requests", loadComponent: () => import("./requests/requests-list/requests-list.component").then(m => m.RequestsListComponent), canActivate: [LoginGuard] },
      { path: "**", redirectTo: "" },
    ],
  },
];
