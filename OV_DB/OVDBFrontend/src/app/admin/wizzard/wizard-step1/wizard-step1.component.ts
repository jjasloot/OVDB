import { Component, OnInit } from '@angular/core';
import { UntypedFormBuilder, UntypedFormGroup, Validators, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { ApiService } from 'src/app/services/api.service';
import { OSMDataLine } from 'src/app/models/osmDataLine.model';
import { Router, ActivatedRoute } from '@angular/router';
import moment from 'moment';
import { DateAdapter, MatOption } from '@angular/material/core';
import { TranslationService } from 'src/app/services/translation.service';
import { MatProgressSpinner } from '@angular/material/progress-spinner';
import { MatFormField, MatLabel, MatSuffix } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatSelect } from '@angular/material/select';
import { MatCheckbox } from '@angular/material/checkbox';
import { NgClass } from '@angular/common';
import { MatDatepickerInput, MatDatepickerToggle, MatDatepicker } from '@angular/material/datepicker';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { MatChipListbox, MatChipOption } from '@angular/material/chips';
import { TranslateModule } from '@ngx-translate/core';
import { MatCard, MatCardContent, MatCardHeader, MatCardTitle, MatCardActions } from '@angular/material/card';

@Component({
    selector: 'app-wizard-step1',
    templateUrl: './wizard-step1.component.html',
    styleUrls: ['./wizard-step1.component.scss'],
    imports: [MatProgressSpinner, FormsModule, ReactiveFormsModule, MatFormField, MatLabel, MatInput, MatSelect, MatOption, MatCheckbox, NgClass, MatDatepickerInput, MatDatepickerToggle, MatSuffix, MatDatepicker, MatButton, MatIconButton, MatIcon, MatChipListbox, MatChipOption, TranslateModule, MatCard, MatCardContent, MatCardHeader, MatCardTitle, MatCardActions]
})
export class WizzardStep1Component implements OnInit {
  form: UntypedFormGroup;
  lines: OSMDataLine[];
  step = 1;
  loading = false;
  differentTimeLeft = false;
  differentTimeRight = false;
  dateTime: moment.Moment = null;
  error: boolean;
  fromTraewelling = false;
  trawellingTripData: any = null;
  constructor(
    private formBuilder: UntypedFormBuilder,
    private apiService: ApiService,
    private router: Router,
    private route: ActivatedRoute,
    private dateAdapter: DateAdapter<any>,
    private translationService: TranslationService) {
    this.dateAdapter.setLocale(this.translationService.dateLocale);

    this.form = this.formBuilder.group({
      reference: ['', Validators.required],
      network: [''],
      type: ['bus', Validators.required],
      dateTime: [null]
    });
  }
  types = [
    'bus',
    'trolleybus',
    'minibus',
    'share_taxi',
    'train',
    'light_rail',
    'subway',
    'tram',
    'ferry',
    'funicular',
    'not_specified'
  ];

  networks = [
    'Achterhoek-Rivierenland',
    'Amstelland-Meerlanden',
    'Arnhem-Nijmegen',
    'Bus Rotterdam',
    'Busvervoer Almere',
    'Drechtsteden, Molenlanden en Gorinchem',
    'Groningen-Drenthe',
    'Gooi- en Vechtstreek',
    'Haaglanden Stad',
    'Haaglanden Streek',
    'Haarlem-IJmond',
    'Hoeksche Waard - Goeree-Overflakkee',
    'Hoofdrailnet Nederland',
    'IJsselmond',
    'Limburg',
    'Midden-Overijssel',
    'Natransport recreatietransferium Renesse',
    'Noord-Holland Noord',
    'Noord- en Zuidwest-Fryslân en Schiermonnikoog',
    'Noordelijke Nevenlijnen',
    'Oost-Brabant',
    'Parkshuttle Rivium',
    'Provincie Utrecht',
    'Publiek Vervoer Groningen-Drenthe',
    'Rail Rotterdam',
    'Rail Haaglanden',
    'Regio Utrecht',
    'Stadsvervoer Amsterdam',
    'Stadsvervoer Lelystad',
    'Twente',
    'Vechtdallijnen',
    'Veluwe',
    'Voorne-Putten en Rozenburg',
    'Waterland',
    'West-Brabant',
    'Zaanstreek',
    'Zeeland',
    'Zuid-Holland Noord',
    'Zuidoost-Brabant',
    'Zuidoost-Fryslân en Wadden',
    'Zwolle - Kampen en Zwolle - Enschede',
    'RNet',
    'Blauwnet'
  ];

  selectedNetwork = '';
  ngOnInit(): void {
    this.translationService.languageChanged.subscribe(() => {
      this.dateAdapter.setLocale(this.translationService.dateLocale);
    });

    // Check if coming from Träwelling
    this.route.queryParams.subscribe(params => {
      if (params['fromTraewelling']) {
        this.fromTraewelling = true;
        const tripDataStr = sessionStorage.getItem('trawellingTripData');
        if (tripDataStr) {
          this.trawellingTripData = JSON.parse(tripDataStr);
          this.prePopulateFromTraewelling();
        }
      }
    });
  }

  getLines() {
    if (this.form.invalid) {
      return;
    }
    const value = this.form.value;
    this.loading = true;
    this.dateTime = value.dateTime;
    this.apiService.importerGetLines(value.reference, value.network, value.type, value.dateTime).subscribe(data => {
      this.lines = data;
      this.step = 2;
      this.loading = false;
    }, err => { this.error = true; });
  }

  getNetwork() {
    if (!this.selectedNetwork) {
      return;
    }
    this.loading = true;
    this.apiService.importerGetNetwork(this.selectedNetwork, this.dateTime).subscribe(data => {
      this.lines = data;
      this.step = 2;
      this.loading = false;
    }, err => { this.error = true; });
  }
  select(line: OSMDataLine) {
    if (this.dateTime) {
      this.router.navigate(['/', 'admin', 'wizard', line.id], { queryParams: { date: this.dateTime.unix() } });
    } else {
      this.router.navigate(['/', 'admin', 'wizard', line.id]);
    }
  }

  gotoStep1() {
    this.step = 1;
  }

  private prePopulateFromTraewelling(): void {
    if (!this.trawellingTripData) return;

    // Pre-populate form with Träwelling data
    const reference = `${this.trawellingTripData.origin} ${this.trawellingTripData.destination}`;
    const transportType = this.mapTrawellingCategoryToOSMType(this.trawellingTripData.category);
    
    this.form.patchValue({
      reference: reference,
      type: transportType,
      dateTime: this.trawellingTripData.date ? moment(this.trawellingTripData.date) : null
    });
  }

  private mapTrawellingCategoryToOSMType(category: string): string {
    // Map Träwelling transport categories to OSM types
    switch (category) {
      case 'NationalExpress':
      case 'National':
      case 'RegionalExp':
      case 'Regional':
      case 'Suburban':
        return 'train';
      case 'Bus':
        return 'bus';
      case 'Subway':
        return 'subway';
      case 'Tram':
        return 'tram';
      case 'Ferry':
        return 'ferry';
      default:
        return 'train';
    }
  }

  // Method to continue with GPX upload instead
  useGpxUpload(): void {
    // Store trip data and navigate to GPX upload
    if (this.trawellingTripData) {
      sessionStorage.setItem('trawellingTripDataForGpx', JSON.stringify(this.trawellingTripData));
    }
    this.router.navigate(['/admin/route-add'], {
      queryParams: { fromTraewelling: 'true' }
    });
  }

}
