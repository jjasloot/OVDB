import { Component, Input, Output, EventEmitter, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslateModule } from '@ngx-translate/core';
import { TraewellingTagMapping } from '../../../models/user-profile.model';
import { STANDARD_TRAEWELLING_TAGS, TRAEWELLING_TAG_LABELS } from '../../../constants/traewelling-tags.constants';

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
    MatChipsModule,
    MatTooltipModule,
    TranslateModule
  ],
  templateUrl: './tag-mapping.component.html',
  styleUrls: ['./tag-mapping.component.scss']
})
export class TagMappingComponent {
  @Input() mappings: TraewellingTagMapping[] = [];
  @Input() availableOvdbTags: string[] = [];
  @Output() mappingsChange = new EventEmitter<TraewellingTagMapping[]>();

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

  addMapping() {
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

    // Emit changes
    this.mappingsChange.emit([...this.mappings]);

    // Clear inputs
    this.newFromTag = '';
    this.newToTag = '';
    this.fromTagInput.set('');
    this.toTagInput.set('');
  }

  removeMapping(index: number) {
    this.mappings.splice(index, 1);
    this.mappingsChange.emit([...this.mappings]);
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
