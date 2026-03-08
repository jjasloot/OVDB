import { Component, Input, Output, EventEmitter, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { TrainlogOperatorMapping } from '../../../models/user-profile.model';
import { ApiService } from '../../../services/api.service';

@Component({
  selector: 'app-operator-mapping',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatAutocompleteModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
    TranslateModule
  ],
  templateUrl: './operator-mapping.component.html',
  styleUrls: ['./operator-mapping.component.scss']
})
export class OperatorMappingComponent {
  @Input() mappings: TrainlogOperatorMapping[] = [];
  @Input() availableOperators: string[] = [];
  @Output() mappingsChange = new EventEmitter<TrainlogOperatorMapping[]>();

  private apiService = inject(ApiService);
  private snackBar = inject(MatSnackBar);
  private translateService = inject(TranslateService);

  isSaving = signal(false);

  newOvdbOperator = '';
  newTrainlogOperator = '';

  ovdbOperatorInput = signal('');
  trainlogOperatorInput = signal('');

  filteredOperators = computed(() => {
    const input = this.ovdbOperatorInput().toLowerCase();
    if (!input) {
      return this.availableOperators;
    }
    return this.availableOperators.filter(op => op.toLowerCase().includes(input));
  });

  onOvdbOperatorInputChange(value: string) {
    this.ovdbOperatorInput.set(value);
  }

  onTrainlogOperatorInputChange(value: string) {
    this.trainlogOperatorInput.set(value);
  }

  async addMapping() {
    if (!this.newOvdbOperator.trim() || !this.newTrainlogOperator.trim()) {
      return;
    }

    const existingIndex = this.mappings.findIndex(m => m.ovdbOperator === this.newOvdbOperator.trim());
    if (existingIndex >= 0) {
      this.mappings[existingIndex].trainlogOperator = this.newTrainlogOperator.trim();
    } else {
      this.mappings.push({
        ovdbOperator: this.newOvdbOperator.trim(),
        trainlogOperator: this.newTrainlogOperator.trim()
      });
    }

    this.newOvdbOperator = '';
    this.newTrainlogOperator = '';
    this.ovdbOperatorInput.set('');
    this.trainlogOperatorInput.set('');

    await this.saveMappings();
  }

  async removeMapping(index: number) {
    this.mappings.splice(index, 1);
    await this.saveMappings();
  }

  private async saveMappings() {
    this.isSaving.set(true);

    try {
      await this.apiService.updateOperatorMappings([...this.mappings]).toPromise();
      this.mappingsChange.emit([...this.mappings]);

      const message = await this.translateService.get('PROFILE.TRAINLOG_OPERATOR_MAPPING_SAVED').toPromise();
      this.snackBar.open(message, '', { duration: 2000 });
    } catch (error) {
      console.error('Error saving operator mappings:', error);
      const message = await this.translateService.get('PROFILE.TRAINLOG_OPERATOR_MAPPING_ERROR').toPromise();
      this.snackBar.open(message, '', { duration: 4000 });
    } finally {
      this.isSaving.set(false);
    }
  }

  selectOvdbOperator(op: string) {
    this.newOvdbOperator = op;
    this.ovdbOperatorInput.set(op);
  }
}
