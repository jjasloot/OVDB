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
import { TraewellingTagMapping } from '../../../models/user-profile.model';
import { STANDARD_TRAEWELLING_TAGS, TRAEWELLING_TAG_LABELS } from '../../../constants/traewelling-tags.constants';
import { ApiService } from '../../../services/api.service';
import { TrawellingService } from '../../../traewelling/services/traewelling.service';

@Component({
  selector: 'app-tag-mapping',
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
  templateUrl: './tag-mapping.component.html',
  styleUrls: ['./tag-mapping.component.scss']
})
export class TagMappingComponent {
  @Input() mappings: TraewellingTagMapping[] = [];
  @Input() availableOvdbTags: string[] = [];
  @Output() mappingsChange = new EventEmitter<TraewellingTagMapping[]>();

  private apiService = inject(ApiService);
  private snackBar = inject(MatSnackBar);
  private translateService = inject(TranslateService);
  private trawellingService = inject(TrawellingService);

  isSaving = signal(false);

  // New mapping being added
  newFromTag = '';
  newToTag = '';

  // Constants for autocomplete
  readonly standardTraewellingTags = STANDARD_TRAEWELLING_TAGS;
  readonly traewellingTagLabels = TRAEWELLING_TAG_LABELS;

  // Signals for reactive filtering
  fromTagInput = signal('');
  toTagInput = signal('');

  // Computed filtered suggestions
  filteredTraewellingTags = computed(() => {
    const input = this.fromTagInput().toLowerCase();
    if (!input) {
      return [...this.standardTraewellingTags];
    }
    return this.standardTraewellingTags.filter(tag => 
      tag.toLowerCase().includes(input) ||
      this.traewellingTagLabels[tag]?.toLowerCase().includes(input)
    );
  });

  filteredOvdbTags = computed(() => {
    const input = this.toTagInput().toLowerCase();
    if (!input) {
      return this.availableOvdbTags;
    }
    return this.availableOvdbTags.filter(tag => 
      tag.toLowerCase().includes(input)
    );
  });

  onFromTagInputChange(value: string) {
    this.fromTagInput.set(value);
  }

  onToTagInputChange(value: string) {
    this.toTagInput.set(value);
  }

  async addMapping() {
    if (!this.newFromTag.trim() || !this.newToTag.trim()) {
      return;
    }

    // Check if mapping already exists
    const existingIndex = this.mappings.findIndex(m => m.fromTag === this.newFromTag.trim());
    if (existingIndex >= 0) {
      // Update existing mapping
      this.mappings[existingIndex].toTag = this.newToTag.trim();
    } else {
      // Add new mapping
      this.mappings.push({
        fromTag: this.newFromTag.trim(),
        toTag: this.newToTag.trim()
      });
    }

    // Clear inputs
    this.newFromTag = '';
    this.newToTag = '';
    this.fromTagInput.set('');
    this.toTagInput.set('');

    // Auto-save
    await this.saveMappings();
  }

  async removeMapping(index: number) {
    this.mappings.splice(index, 1);
    
    // Auto-save
    await this.saveMappings();
  }

  private async saveMappings() {
    this.isSaving.set(true);
    
    try {
      // Use dedicated tag mappings endpoint
      await this.apiService.updateTagMappings([...this.mappings]).toPromise();
      
      // Clear the traewelling service tag mappings cache
      this.trawellingService.clearTagMappingsCache();
      
      // Emit changes
      this.mappingsChange.emit([...this.mappings]);
      
      // Show success message
      const message = await this.translateService.get('PROFILE.TAG_MAPPING_SAVED').toPromise();
      this.snackBar.open(message, '', { duration: 2000 });
      
    } catch (error) {
      console.error('Error saving tag mappings:', error);
      const message = await this.translateService.get('PROFILE.TAG_MAPPING_ERROR').toPromise();
      this.snackBar.open(message, '', { duration: 4000 });
      
      // Revert changes on error would require keeping previous state
    } finally {
      this.isSaving.set(false);
    }
  }

  getTraewellingTagLabel(tag: string): string {
    return this.traewellingTagLabels[tag] || tag;
  }

  selectTraewellingTag(tag: string) {
    this.newFromTag = tag;
    this.fromTagInput.set(tag);
  }

  selectOvdbTag(tag: string) {
    this.newToTag = tag;
    this.toTagInput.set(tag);
  }
}
