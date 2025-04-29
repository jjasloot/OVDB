import { ChangeDetectorRef, Component, OnInit } from "@angular/core";
import { environment } from "src/environments/environment";
import { ApiService } from "../services/api.service";
import { Map } from "../models/map.model";
import { TranslationService } from "../services/translation.service";
import { MatCheckboxChange, MatCheckbox } from "@angular/material/checkbox";
import { TranslateService, TranslateModule } from "@ngx-translate/core";
import { MatCard } from "@angular/material/card";
import { MatFormField } from "@angular/material/form-field";
import { MatInput } from "@angular/material/input";
import { FormsModule } from "@angular/forms";
@Component({
  selector: "app-image-creator",
  templateUrl: "./image-creator.component.html",
  styleUrls: ["./image-creator.component.scss"],
  imports: [
    MatCard,
    MatCheckbox,
    MatFormField,
    MatInput,
    FormsModule,
    TranslateModule,
  ]
})
export class ImageCreatorComponent implements OnInit {
  maps: Map[];
  baseUrl = environment.backend + "api/images/";
  selectedGuids: string[] = [];
  imageSrc: string = environment.backend + "api/images/";
  title = "";
  width = 300;
  height = 100;
  includeTotals = false;
  constructor(
    private apiService: ApiService,
    private translationService: TranslationService,
    public translateService: TranslateService,
    private cd: ChangeDetectorRef
  ) { }

  ngOnInit(): void {
    this.apiService.getMaps().subscribe((maps) => {
      this.maps = maps;
    });
  }

  setImage() {
    console.log(this.selectedGuids);
    let query = "";
    query += `?includeTotal=${this.includeTotals}`;
    query += `&language=${this.translationService.language}`;
    this.selectedGuids.forEach((guid) => {
      query += `&guid=${guid}`;
    });
    if (!!this.title) {
      query += `&title=${this.title}`;
    }
    if (!!this.width) {
      query += `&width=${this.width}`;
    }
    if (!!this.height) {
      query += `&height=${this.height}`;
    }
    this.imageSrc = this.baseUrl + query;
    this.cd.detectChanges();
  }

  name(item: Map) {
    return this.translationService.getNameForItem(item);
  }
  isMapChecked(map: string): boolean {
    return this.selectedGuids.includes(map);
  }

  selectMap(map: string, event: MatCheckboxChange) {
    if (event.checked && !this.selectedGuids.includes(map)) {
      this.selectedGuids.push(map);
    }
    if (!event.checked && this.selectedGuids.includes(map)) {
      this.selectedGuids = this.selectedGuids.filter((i) => i !== map);
    }
    this.setImage();
  }
}
