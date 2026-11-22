import { Injectable, inject } from "@angular/core";
import { Title } from "@angular/platform-browser";
import { RouterStateSnapshot, TitleStrategy } from "@angular/router";
import { TranslateService } from "@ngx-translate/core";

/**
 * Route `title`s are translation keys; the browser tab shows "<page> · OVDB" so tabs, history and
 * bookmarks can be told apart. Re-applied when the language changes.
 */
@Injectable({ providedIn: "root" })
export class TranslatedTitleStrategy extends TitleStrategy {
  private readonly title = inject(Title);
  private readonly translate = inject(TranslateService);
  private snapshot: RouterStateSnapshot | null = null;

  constructor() {
    super();
    this.translate.onLangChange.subscribe(() => {
      if (this.snapshot) {
        this.updateTitle(this.snapshot);
      }
    });
  }

  override updateTitle(snapshot: RouterStateSnapshot): void {
    this.snapshot = snapshot;
    const key = this.buildTitle(snapshot);
    if (!key) {
      this.title.setTitle("OVDB");
      return;
    }
    this.translate.get(key).subscribe((page: string) => this.title.setTitle(`${page} · OVDB`));
  }
}
