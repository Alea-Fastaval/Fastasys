import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TicketType } from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { AuthService, TranslationService } from '@shared/services';
import { priceText } from '../../participants/participant-price/participant-price';
import { TicketFlashService } from '../ticket-flash.service';
import { ticketTypeListText, translatedShortDay } from '../ticket-texts';

// Infosys indgang/entrytypes ("Alle indgangs-typer", in the Indgang menu): Id, Type ("name - Ons 24/03"), Pris and
// Antal solgt, sorted by name. Infosys's landing page indgang/ only linked here and to "Opret indgangs-type"; this page
// has both links, and "Statistik".
@Component({
  selector: 'app-ticket-types',
  imports: [RouterLink, MatButtonModule, MatIconModule, Card, TranslatePipe],
  templateUrl: './ticket-types.html',
  styleUrl: './ticket-types.scss',
})
export class TicketTypesComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly translation = inject(TranslationService);
  private readonly shortDay = translatedShortDay(this.translation);

  public readonly flash = inject(TicketFlashService).take();
  public readonly mayEdit = inject(AuthService).hasPrivilege('entrance_edit');
  public readonly types = signal<TicketType[] | null>(null);
  public readonly failed = signal(false);
  public readonly priceText = priceText;

  public ngOnInit(): void {
    this.http.get<TicketType[]>('/api/ticket-types').subscribe({
      next: types => this.types.set(types),
      error: () => this.failed.set(true),
    });
  }

  public typeText(type: TicketType): string {
    return ticketTypeListText(type, this.shortDay, this.translation.translate('tickets.partout'));
  }
}
