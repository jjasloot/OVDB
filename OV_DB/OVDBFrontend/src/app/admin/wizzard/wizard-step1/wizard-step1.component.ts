import { Component, OnInit } from '@angular/core';
import { UntypedFormBuilder, UntypedFormGroup, Validators } from '@angular/forms';
import { ApiService } from 'src/app/services/api.service';
import { OSMDataLine } from 'src/app/models/osmDataLine.model';
import { Router } from '@angular/router';
import * as moment from 'moment';
import { DateAdapter } from '@angular/material/core';
import { TranslationService } from 'src/app/services/translation.service';

@Component({
  selector: 'app-wizard-step1',
  templateUrl: './wizard-step1.component.html',
  styleUrls: ['./wizard-step1.component.scss']
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
  constructor(
    private formBuilder: UntypedFormBuilder,
    private apiService: ApiService,
    private router: Router,
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
    if (!!this.dateTime) {
      this.router.navigate(['/', 'admin', 'wizard', line.id], { queryParams: { date: this.dateTime.unix() } });
    } else {
      this.router.navigate(['/', 'admin', 'wizard', line.id]);
    }
  }

  gotoStep1() {
    this.step = 1;
  }

}
