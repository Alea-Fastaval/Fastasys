import { Component, Input, inject } from '@angular/core';
import { ParticipantIdCard } from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { TranslationService } from '@shared/services';

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
// photo, the buttons "Foto upload" and "Idkort", and "Skabelon:" with the participant's ID card template.
//
// ID cards are not ported yet, so the page passes null and the box says so. When they are ported, the API fills
// details.idCard (see ParticipantIdCardDto in ParticipantsController) and this box shows the photo and the template.
// Add then: the "Foto upload" button (the participant's photo upload form), the "Idkort" button (the participant's ID
// card) and choosing another template by clicking it (Infosys participant.js, "participant-template").
@Component({
  selector: 'app-participant-id-card',
  imports: [Card, TranslatePipe],
  templateUrl: './participant-id-card.html',
  styleUrl: './participant-id-card.scss',
})
export class ParticipantIdCardComponent {
  private readonly translationService = inject(TranslationService);

  // A decorator input, like Card's: the unit tests run Angular in JIT mode, which doesn't know signal inputs
  @Input({ required: true }) public idCard: ParticipantIdCard | null = null;

  public templateText(card: ParticipantIdCard): string {
    // Reading the signal makes the template update when the language changes, like the translate pipe
    this.translationService.translations();
    return idCardTemplateText(
      card,
      name => this.translationService.translate('participants.idCard.defaultTemplate', { name }),
      this.translationService.translate('participants.idCard.noTemplate'),
    );
  }
}
