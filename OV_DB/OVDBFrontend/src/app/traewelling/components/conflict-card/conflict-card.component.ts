import { Component, EventEmitter, Input, Output, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslateModule } from '@ngx-translate/core';
import { TrawellingConflict, TrawellingConflictAction } from '../../../models/traewelling.model';

/**
 * One imported trip whose Träwelling status was edited or deleted upstream.
 * Dumb card: shows the current OVDB values against the upstream values and
 * emits the chosen resolution; the parent talks to the backend.
 */
@Component({
  selector: 'app-conflict-card',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatButtonModule, MatIconModule, TranslateModule],
  templateUrl: './conflict-card.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./conflict-card.component.scss']
})
export class ConflictCardComponent {
  @Input({ required: true }) conflict!: TrawellingConflict;
  @Input() busy = false;
  @Output() resolve = new EventEmitter<TrawellingConflictAction>();

  get isDeleted(): boolean {
    return this.conflict.state === 'DeletedUpstream';
  }

  get title(): string {
    const route = this.conflict.routeName
      || [this.conflict.routeFrom, this.conflict.routeTo].filter(Boolean).join(' → ');
    return route || this.conflict.newTrip?.transport?.lineName || `#${this.conflict.statusId}`;
  }

  get newLine(): string | undefined {
    return this.conflict.newTrip?.transport?.lineName;
  }

  get newRoute(): string | undefined {
    const transport = this.conflict.newTrip?.transport;
    if (!transport?.origin?.name || !transport?.destination?.name) {
      return undefined;
    }
    return `${transport.origin.name} → ${transport.destination.name}`;
  }

  get newStart(): string | undefined {
    const origin = this.conflict.newTrip?.transport?.origin;
    return origin?.departureReal ?? origin?.departureScheduled;
  }

  get newEnd(): string | undefined {
    const destination = this.conflict.newTrip?.transport?.destination;
    return destination?.arrivalReal ?? destination?.arrivalScheduled;
  }

  get startChanged(): boolean {
    return this.timesDiffer(this.conflict.instanceStartTime, this.newStart);
  }

  get endChanged(): boolean {
    return this.timesDiffer(this.conflict.instanceEndTime, this.newEnd);
  }

  get fromChanged(): boolean {
    return this.stationsDiffer(this.conflict.routeFrom, this.conflict.newTrip?.transport?.origin?.name);
  }

  get toChanged(): boolean {
    return this.stationsDiffer(this.conflict.routeTo, this.conflict.newTrip?.transport?.destination?.name);
  }

  /**
   * When the journey's endpoints changed, copying times onto the existing route would be
   * wrong — the geometry no longer matches. Apply-times is replaced by a re-import hint.
   */
  get locationChanged(): boolean {
    return this.fromChanged || this.toChanged;
  }

  private timesDiffer(current?: string, upstream?: string): boolean {
    if (!current || !upstream) {
      return !!current !== !!upstream;
    }
    // Compare at minute precision; sub-minute serialization noise is not a change
    return Math.floor(new Date(current).getTime() / 60000) !== Math.floor(new Date(upstream).getTime() / 60000);
  }

  private stationsDiffer(current?: string, upstream?: string): boolean {
    // Only claim a location change when both sides are known and clearly different;
    // OVDB route endpoints are user-editable text, so compare normalized
    if (!current || !upstream) {
      return false;
    }
    const normalize = (value: string) => value.toLowerCase().replace(/\s+/g, ' ').trim();
    return normalize(current) !== normalize(upstream);
  }
}
