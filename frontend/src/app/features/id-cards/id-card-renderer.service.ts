import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { IdCards } from '@shared/types';
import { fetchImage } from './auth-images';
import { CARD_FONT, MM_PER_PIXEL, drawIdCard } from './id-card-drawing';

// A drawn card, ready to print at its size in millimetres (300 DPI, as in Infosys)
export interface RenderedCard {
  participantId: number;
  src: string;
  widthMm: number;
  heightMm: number;
}

// Draws the cards from api/id-cards: fetches each template's background and each participant's photo (behind login)
// and draws every card on a canvas of its template's size (Infosys IdTemplateRenderer, agreed 2026-11-18 to draw in
// the browser)
@Injectable({ providedIn: 'root' })
export class IdCardRendererService {
  private readonly http = inject(HttpClient);

  public async render(data: IdCards): Promise<RenderedCard[]> {
    // The card font must be loaded before the canvas measures the texts
    await document.fonts?.load(CARD_FONT);

    const templates = new Map(data.templates.map(template => [template.id, template]));
    const backgrounds = new Map<number, HTMLImageElement>();
    for (const template of data.templates) {
      backgrounds.set(template.id, await fetchImage(this.http, `/api/id-card-templates/${template.id}/background`));
    }

    const cards: RenderedCard[] = [];
    for (const card of data.cards) {
      const template = templates.get(card.templateId);
      const background = backgrounds.get(card.templateId);
      if (!template || !background) continue;

      const photo = card.hasPhoto
        ? await fetchImage(this.http, `/api/participants/${card.participantId}/photo/cropped`)
        : null;
      const canvas = document.createElement('canvas');
      canvas.width = template.width;
      canvas.height = template.height;
      const context = canvas.getContext('2d');
      if (!context) throw new Error('The browser can not draw on a canvas');

      drawIdCard(context, template, card, { background, photo });
      cards.push({
        participantId: card.participantId,
        src: canvas.toDataURL('image/png'),
        widthMm: template.width * MM_PER_PIXEL,
        heightMm: template.height * MM_PER_PIXEL,
      });
    }
    return cards;
  }
}
