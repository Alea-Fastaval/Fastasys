import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TicketStatsGroup } from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { TranslationService } from '@shared/services';
import { statsDayText, statsHeadingKey, translatedShortDay } from '../ticket-texts';

// Infosys entry/stats ("Statistik" in the Indgang menu, entrystats.phtml): a heading per ticket type name, with each
// day and the number of participants who have that type ("Ons, 24 — 12"). Annulled participants count too.
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

  // "Indgang, partout" and the like, or the name (Indgang::getProperType)
  public heading(group: TicketStatsGroup): string {
    const key = statsHeadingKey(group);
    if (key === null) return group.name;
    // Reading the signal makes the template update when the language changes
    this.translation.translations();
    return this.translation.translate(key);
  }

  public dayText(day: string): string {
    return statsDayText(day, this.shortDay);
  }
}
