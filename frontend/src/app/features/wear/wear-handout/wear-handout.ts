import { AfterViewInit, Component, ElementRef, ViewChild, inject, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { WearHandoutInputDto, WearHandoutResult } from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { TranslationService } from '@shared/services';
import { handoutItemText } from '../wear-texts';

// What the "Resultat" box shows: the message and, after a success, the wear handed out or taken back. "Marker
// udleveret" shows the participant's category after each line.
export interface HandoutResult {
  type: 'success' | 'error';
  text: string;
  items: { text: string; category: string | null }[];
}

// The refusals the API gives, by reason (WearHandoutController); each is "Fejl: ..." in Infosys
const ERROR_KEYS: Record<string, string> = {
  noInput: 'noId',
  notFound: 'notFound',
  noShow: 'noShow',
  annulled: 'annulled',
  notCheckedIn: 'notCheckedIn',
  noWear: 'noWear',
  alreadyReceived: 'alreadyReceived',
};

// Infosys wear/handout ("Wear udlevering", wear_handout.js): type or scan a participant id and "Marker udleveret"
// (Enter does the same) to hand out all their wear; "Undo" takes the last one back. The box shows the participant
// and the wear to hand out, or the error. While a request runs, new scans are ignored (Infosys played a sound; its
// sounds aren't ported).
@Component({
  selector: 'app-wear-handout',
  imports: [FormsModule, RouterLink, MatButtonModule, MatIconModule, Card, TranslatePipe],
  templateUrl: './wear-handout.html',
  styleUrl: './wear-handout.scss',
})
export class WearHandoutComponent implements AfterViewInit {
  private readonly http = inject(HttpClient);
  private readonly translation = inject(TranslationService);

  @ViewChild('userId') private readonly userIdInput?: ElementRef<HTMLInputElement>;

  public userId = '';
  public readonly busy = signal(false);
  public readonly result = signal<HandoutResult | null>(null);
  public readonly showUndo = signal(false);
  // The id of the last participant served, for "Undo"
  private lastUser = '';

  public ngAfterViewInit(): void {
    this.focus();
  }

  // "Marker udleveret"
  public markHandedOut(): void {
    if (this.busy()) return;

    this.lastUser = this.userId;
    this.userId = '';
    this.focus();

    if (this.lastUser === '') {
      this.showUndo.set(false);
      this.result.set(this.error('wear.handout.errors.noInput'));
      return;
    }

    this.busy.set(true);
    const dto: WearHandoutInputDto = { input: this.lastUser };
    this.http.post<WearHandoutResult>('/api/wear-handout', dto).subscribe({
      next: result => {
        this.busy.set(false);
        this.showUndo.set(true);
        this.result.set(this.success('wear.handout.marked', result, true));
      },
      error: (err: HttpErrorResponse) => {
        this.busy.set(false);
        this.showUndo.set(false);
        const key = ERROR_KEYS[err.error?.reason] ?? 'unknown';
        this.result.set(this.error(`wear.handout.errors.${key}`, { id: String(err.error?.participantId ?? '') }));
      },
    });
  }

  // "Undo"; Infosys shows the same error whatever went wrong
  public undo(): void {
    if (this.busy()) return;

    this.busy.set(true);
    this.showUndo.set(false);
    this.focus();
    const dto: WearHandoutInputDto = { input: this.lastUser };
    this.http.post<WearHandoutResult>('/api/wear-handout/undo', dto).subscribe({
      next: result => {
        this.busy.set(false);
        this.result.set(this.success('wear.handout.undone', result, false));
      },
      error: () => {
        this.busy.set(false);
        this.result.set(this.error('wear.handout.errors.undoFailed'));
      },
    });
  }

  private success(key: string, result: WearHandoutResult, withCategory: boolean): HandoutResult {
    const words = {
      pieces: this.translation.translate('wear.handout.pieces'),
      inSize: this.translation.translate('wear.handout.inSize'),
    };
    return {
      type: 'success',
      text: this.translation.translate(key, { name: result.name, id: String(result.participantId) }),
      items: result.items.map(item => ({
        text: handoutItemText(item, words),
        category: withCategory ? result.categoryName : null,
      })),
    };
  }

  private error(key: string, params?: Record<string, string>): HandoutResult {
    return {
      type: 'error',
      text: `${this.translation.translate('wear.handout.error')} ${this.translation.translate(key, params)}`,
      items: [],
    };
  }

  private focus(): void {
    this.userIdInput?.nativeElement.focus();
  }
}
