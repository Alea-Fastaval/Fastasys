import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TicketStatsGroup, TicketStatsType } from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { TranslationService } from '@shared/services';
import { statsKindKey, statsTypeText, translatedShortDay } from '../ticket-texts';

// Infosys entry/stats ("Statistik" in the Indgang menu, entrystats.phtml): a heading per ticket type name, with each
// type and the number of participants who have it ("Ons, 24 — 12"). Annulled participants count too. The heading is
// the name with the kind under it (agreed 2026-11-25; Infosys showed only the kind), and every type is listed.
@Component({
  selector: 'app-ticket-stats',
  imports: [RouterLink, MatButtonModule, MatIconModule, Card, TranslatePipe],
  templateUrl: './ticket-stats.html',
  styleUrl: './ticket-stats.scss',
})
export class TicketStatsComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly translation = inject(TranslationService);
  private readonly shortDay = translatedShortDay(this.translation);

  public readonly groups = signal<TicketStatsGroup[] | null>(null);
  public readonly failed = signal(false);

  public ngOnInit(): void {
    this.http.get<TicketStatsGroup[]>('/api/ticket-types/stats').subscribe({
      next: groups => this.groups.set(groups),
      error: () => this.failed.set(true),
    });
  }

  // "Indgang, partout", "Alea-medlemskab" and the like (Indgang::getProperType)
  public kindText(group: TicketStatsGroup): string {
    return this.translate(statsKindKey(group));
  }

  // "Ons, 24", "Partout" or "Antal solgt"
  public typeText(type: TicketStatsType): string {
    return statsTypeText(type, this.shortDay, {
      partout: this.translate('tickets.partout'),
      soldCount: this.translate('tickets.fields.soldCount'),
    });
  }

  private translate(key: string): string {
    // Reading the signal makes the template update when the language changes
    this.translation.translations();
    return this.translation.translate(key);
  }
}
