import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { WearBreakdown, WearBreakdownRow } from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { AuthService } from '@shared/services';
import { breakdownRowText, miniListQuery } from '../wear-texts';

// A cell after "I alt": the size column (null for "Ingen størrelse") and the amount in it
export interface BreakdownCell {
  sizeId: number | null;
  count: number;
}

// The row's cells: "Ingen størrelse", then a cell per size
export function breakdownCells(row: WearBreakdownRow, breakdown: WearBreakdown): BreakdownCell[] {
  return [
    { sizeId: null, count: row.noSize },
    ...breakdown.sizes.map(size => ({
      sizeId: size.id,
      count: row.sizeCounts.find(c => c.sizeId === size.id)?.count ?? 0,
    })),
  ];
}

// Infosys wear/breakdown ("Bestillings-oversigt", wearbreakdown.phtml): a row per wear type and combination of model,
// design and color, with "I alt", "Ingen størrelse" and a column per size. An amount above 0 links to the orders behind
// it (wear/detailed/:id:), for users who may see the order lists. Annulled participants aren't counted (agreed
// 2026-11-26).
@Component({
  selector: 'app-wear-breakdown',
  imports: [RouterLink, MatButtonModule, MatIconModule, Card, TranslatePipe],
  templateUrl: './wear-breakdown.html',
  styleUrl: './wear-breakdown.scss',
})
export class WearBreakdownComponent implements OnInit {
  private readonly http = inject(HttpClient);

  public readonly mayList = inject(AuthService).hasPrivilege('wear_list_view', 'wear_distribute_edit');
  public readonly breakdown = signal<WearBreakdown | null>(null);
  public readonly failed = signal(false);
  public readonly rowText = breakdownRowText;
  public readonly cells = breakdownCells;

  public ngOnInit(): void {
    this.http.get<WearBreakdown>('/api/wear-orders/breakdown').subscribe({
      next: breakdown => this.breakdown.set(breakdown),
      error: () => this.failed.set(true),
    });
  }

  public query(row: WearBreakdownRow, sizeId: number | null): Record<string, string> {
    return miniListQuery(row, sizeId ?? 'none');
  }
}
