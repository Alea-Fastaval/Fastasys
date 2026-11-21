import {
  Component,
  EventEmitter,
  Input,
  OnChanges,
  OnDestroy,
  Output,
  SimpleChanges,
  inject,
  signal,
} from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { IdCardTemplateOption, ParticipantIdCard } from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { TranslationService } from '@shared/services';
import { fetchImageUrl } from '../../id-cards/auth-images';

// What Infosys shows after "Skabelon:": the participant's own template, or "Default ({category's template})", or
// "Default (ingen)" when the category has none (ParticipantModel::fetchTemplateSelectData)
export function idCardTemplateText(
  card: ParticipantIdCard,
  defaultText: (name: string) => string,
  none: string,
): string {
  return card.templateName ?? defaultText(card.defaultTemplateName ?? none);
}

// The IDkort box of the Infosys participant page (visdeltager.phtml, #id-card-content): the participant's cropped
// photo; "Foto upload", which opens the photo page; "Idkort", which opens the participant's ID card in a new tab; and
// "Skabelon:" with the participant's template, which can be changed by clicking it (Infosys participant.js,
// "participant-template"). Like the rest of that part of the page, it is for participant editors.
@Component({
  selector: 'app-participant-id-card',
  imports: [FormsModule, RouterLink, MatButtonModule, Card, TranslatePipe],
  templateUrl: './participant-id-card.html',
  styleUrl: './participant-id-card.scss',
})
export class ParticipantIdCardComponent implements OnChanges, OnDestroy {
  private readonly http = inject(HttpClient);
  private readonly translationService = inject(TranslationService);

  // Decorator inputs, like Card's: the unit tests run Angular in JIT mode, which doesn't know signal inputs
  @Input({ required: true }) public idCard!: ParticipantIdCard;
  @Input({ required: true }) public participantId = 0;
  // The choices of "Skabelon:"
  @Input() public templates: IdCardTemplateOption[] = [];

  // A template was chosen; the page loads the participant again
  @Output() public readonly changed = new EventEmitter<void>();

  // The photo as a blob URL (the API's address needs login, which an <img src> can't send)
  public readonly photo = signal<string | null>(null);
  public readonly editing = signal(false);
  public readonly failed = signal(false);

  public ngOnChanges(changes: SimpleChanges): void {
    if (changes['idCard']) void this.loadPhoto(this.idCard.photoUrl);
  }

  public ngOnDestroy(): void {
    this.setPhoto(null);
  }

  public templateText(card: ParticipantIdCard): string {
    // Reading the signal makes the template update when the language changes, like the translate pipe
    this.translationService.translations();
    return idCardTemplateText(
      card,
      name => this.translationService.translate('participants.idCard.defaultTemplate', { name }),
      this.translationService.translate('participants.idCard.noTemplate'),
    );
  }

  // "Default (...)", the first choice, removes the participant's own template
  public defaultText(card: ParticipantIdCard): string {
    this.translationService.translations();
    return this.translationService.translate('participants.idCard.defaultTemplate', {
      name: card.defaultTemplateName ?? this.translationService.translate('participants.idCard.noTemplate'),
    });
  }

  public startEdit(): void {
    this.failed.set(false);
    this.editing.set(true);
  }

  public cancelEdit(): void {
    this.editing.set(false);
  }

  public chooseTemplate(templateId: number | null): void {
    this.http.put(`/api/participants/${this.participantId}/fields/idCardTemplateId`, { value: templateId }).subscribe({
      next: () => {
        this.editing.set(false);
        this.changed.emit();
      },
      error: () => this.failed.set(true),
    });
  }

  private async loadPhoto(url: string | null): Promise<void> {
    if (!url) {
      this.setPhoto(null);
      return;
    }
    try {
      this.setPhoto(await fetchImageUrl(this.http, url));
    } catch {
      this.setPhoto(null);
    }
  }

  private setPhoto(url: string | null): void {
    const old = this.photo();
    if (old) URL.revokeObjectURL(old);
    this.photo.set(url);
  }
}
