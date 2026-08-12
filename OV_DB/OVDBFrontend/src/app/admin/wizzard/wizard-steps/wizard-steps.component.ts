import { Component, input, ChangeDetectionStrategy } from "@angular/core";
import { TranslateModule } from "@ngx-translate/core";

/**
 * Progress header for the route wizard. The flow spans two routed components
 * (search + pick line, then pick stops), so neither can own a mat-stepper;
 * this shows where you are in the three steps.
 */
@Component({
  selector: "app-wizard-steps",
  standalone: true,
  imports: [TranslateModule],
  template: `
    <ol class="wizard-steps">
      @for (label of labels; track label; let i = $index) {
      <li [class.active]="i + 1 === current()" [class.done]="i + 1 < current()"
        [attr.aria-current]="i + 1 === current() ? 'step' : null">
        <span class="marker">{{ i + 1 }}</span>
        <span class="label">{{ label | translate }}</span>
      </li>
      }
    </ol>
  `,
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ["./wizard-steps.component.scss"],
})
export class WizardStepsComponent {
  readonly current = input.required<number>();

  readonly labels = [
    "WIZARD.STEP_SEARCH",
    "WIZARD.STEP_PICK_LINE",
    "WIZARD.STEP_PICK_STOPS",
  ];
}
