import { inject } from "@angular/core";
import { MatSnackBar } from "@angular/material/snack-bar";
import { TranslateService } from "@ngx-translate/core";

/**
 * Feedback for a cdkCopyToClipboard button, which otherwise copies without any visible sign.
 * Call in an injection context; bind the result to (cdkCopyToClipboardCopied).
 */
export function injectLinkCopied(): (copied: boolean) => void {
  const snackBar = inject(MatSnackBar);
  const translate = inject(TranslateService);
  return (copied) =>
    snackBar.open(translate.instant(copied ? "LINK_COPIED" : "ERRORS.GENERIC"), undefined, { duration: 2500 });
}
