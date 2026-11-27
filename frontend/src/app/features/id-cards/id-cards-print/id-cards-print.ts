import { Component, effect, inject, signal, untracked } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { IdCards } from '@shared/types';
import { TranslatePipe } from '@shared/pipes';
import { QueryParamsService } from '@shared/services';
import { IdCardRendererService, RenderedCard } from '../id-card-renderer.service';

// Infosys id-card/render?ids=1-2-3 (the "Idkort" button of the participant page): the participants' ID cards.
// Infosys drew them on the server and made the browser save them as idcards.pdf, laid out on A4 pages with cut lines.
// Here the browser draws them and lays them out the same way at 300 DPI; "Print" opens the browser's print dialog,
// which can also save a PDF (agreed 2026-11-18).
@Component({
  selector: 'app-id-cards-print',
  imports: [MatButtonModule, MatIconModule, TranslatePipe],
  providers: [QueryParamsService],
  templateUrl: './id-cards-print.html',
  styleUrl: './id-cards-print.scss',
})
export class IdCardsPrintComponent {
  private readonly http = inject(HttpClient);
  private readonly renderer = inject(IdCardRendererService);
  private readonly ids = inject(QueryParamsService).text('ids');

  // null while the cards are drawn
  public readonly cards = signal<RenderedCard[] | null>(null);
  public readonly failed = signal(false);

  constructor() {
    effect(() => {
      const ids = this.ids();
      untracked(() => void this.load(ids));
    });
  }

  public print(): void {
    window.print();
  }

  private async load(ids: string | null): Promise<void> {
    this.cards.set(null);
    this.failed.set(false);
    if (!ids) {
      this.cards.set([]);
      return;
    }

    try {
      const data = await firstValueFrom(this.http.get<IdCards>('/api/id-cards', { params: { ids } }));
      this.cards.set(await this.renderer.render(data));
    } catch {
      this.failed.set(true);
    }
  }
}
