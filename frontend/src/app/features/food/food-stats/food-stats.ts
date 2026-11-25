import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { FoodStats, FoodStatsMealTime } from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { TranslationService } from '@shared/services';
import { translatedShortDay } from '../../tickets/ticket-texts';
import { foodStatsDayText, receivedPercent } from '../food-texts';

// Infosys food/stats ("Statistik" in the Mad menu, foodstats.phtml): per food type "Alle dage, sammenlagt: N" and each
// meal time "Ons, 24 — N - heraf M udleveret (P%)". Annulled participants aren't counted (agreed 2026-11-25; Infosys
// counted them). Infosys also listed its "Jubilæumsfest" ticket here when one existed; that was left out.
@Component({
  selector: 'app-food-stats',
  imports: [RouterLink, MatButtonModule, MatIconModule, Card, TranslatePipe],
  templateUrl: './food-stats.html',
  styleUrl: './food-stats.scss',
})
export class FoodStatsComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly translation = inject(TranslationService);
  private readonly shortDay = translatedShortDay(this.translation);

  public readonly stats = signal<FoodStats[] | null>(null);
  public readonly failed = signal(false);

  public ngOnInit(): void {
    this.http.get<FoodStats[]>('/api/food-types/stats').subscribe({
      next: stats => this.stats.set(stats),
      error: () => this.failed.set(true),
    });
  }

  public dayText(time: string): string {
    return foodStatsDayText(time, this.shortDay);
  }

  // "12 - heraf 5 udleveret (41.7%)", or just "0" when nobody ordered it (foodstats.phtml)
  public countText(mealTime: FoodStatsMealTime): string {
    if (mealTime.total === 0) return '0';
    // Reading the signal makes the template update when the language changes
    this.translation.translations();
    const received = this.translation.translate('food.stats.received', {
      received: mealTime.received.toString(),
      percent: receivedPercent(mealTime.received, mealTime.total).toString(),
    });
    return `${mealTime.total} - ${received}`;
  }
}
