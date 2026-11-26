import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { UpdateWearOrderReceivedDto, WearOrderLine, WearOrderListParticipant } from '@shared/types';
import { TranslatePipe } from '@shared/pipes';
import { AuthService } from '@shared/services';

// Infosys wear/detailed ("Alle bestillinger") and wear/unfilled ("Alle ikke-udleverede", route data unfilled): each
// participant with orders, by id: "ID: {id} — Navn: {navn} — {kategori}", then their orders with Kategori (the wear's
// name), the price's category, Antal, Str., Status and "Marker udleveret" / "Fortryd udlevering". The unfilled list
// shows only the orders not handed out (Infosys showed all orders of those participants; changed 2026-11-26); an order
// marked on the page stays until the page is loaded again, so a mistake can be undone. "Print version" prints the list
// without Status and the buttons (Infosys's wear/detailed/print/). Annulled participants are left out (agreed
// 2026-11-26).
@Component({
  selector: 'app-wear-orders',
  imports: [RouterLink, MatButtonModule, MatIconModule, TranslatePipe],
  templateUrl: './wear-orders.html',
  styleUrl: './wear-orders.scss',
})
export class WearOrdersComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);

  public readonly unfilled = this.route.snapshot.data['unfilled'] === true;
  public readonly mayMark = inject(AuthService).hasPrivilege('wear_distribute_edit');
  public readonly participants = signal<WearOrderListParticipant[] | null>(null);
  public readonly failed = signal(false);
  public readonly markFailed = signal(false);

  public ngOnInit(): void {
    this.http
      .get<WearOrderListParticipant[]>('/api/wear-orders', { params: { unfilled: String(this.unfilled) } })
      .subscribe({
        next: participants => this.participants.set(participants),
        error: () => this.failed.set(true),
      });
  }

  public print(): void {
    window.print();
  }

  // "Marker udleveret" / "Fortryd udlevering" (weardetails.js: "Kunne ikke ændre markeringen" when it fails)
  public toggle(order: WearOrderLine): void {
    const dto: UpdateWearOrderReceivedDto = { isReceived: !order.isReceived };
    this.markFailed.set(false);
    this.http.put(`/api/wear-orders/${order.id}/received`, dto).subscribe({
      next: () =>
        this.participants.update(list =>
          (list ?? []).map(p => ({
            ...p,
            orders: p.orders.map(o => (o.id === order.id ? { ...o, isReceived: dto.isReceived } : o)),
          })),
        ),
      error: () => this.markFailed.set(true),
    });
  }
}
