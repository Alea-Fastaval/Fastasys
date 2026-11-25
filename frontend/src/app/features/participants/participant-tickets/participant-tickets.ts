import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { ParticipantTickets, TicketTypeOption, UpdateParticipantTicketsDto } from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { TranslationService } from '@shared/services';
import { ParticipantFlashService } from '../participant-flash.service';
import { ticketOptionText, translatedShortDay } from '../../tickets/ticket-texts';

// imwedit.js setupIndgang: "Tilføj" adds the chosen type unless it is already listed
export function addTicket(tickets: TicketTypeOption[], chosen: TicketTypeOption | undefined): TicketTypeOption[] {
  if (!chosen || tickets.some(t => t.id === chosen.id)) return tickets;
  return [...tickets, chosen];
}

// The Indgang part of Infosys's "Rediger indgang, mad og wear" (deltager/updatemadwear/:id:, the "Ret/slet" link of
// the Indgang/Mad box): pick a ticket type, "Tilføj" it, "Slet" the ones that should go, and "Opdater" saves the list.
// Food and wear are on the same Infosys page; they come with their own ports.
@Component({
  selector: 'app-participant-tickets',
  imports: [
    FormsModule,
    RouterLink,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatSelectModule,
    Card,
    TranslatePipe,
  ],
  templateUrl: './participant-tickets.html',
  styleUrl: './participant-tickets.scss',
})
export class ParticipantTicketsComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly flash = inject(ParticipantFlashService);
  private readonly translation = inject(TranslationService);
  private readonly shortDay = translatedShortDay(this.translation);

  public readonly participantId = Number(this.route.snapshot.paramMap.get('id'));
  public readonly page = signal<ParticipantTickets | null>(null);
  public readonly tickets = signal<TicketTypeOption[]>([]);
  public readonly notFound = signal(false);
  public readonly busy = signal(false);

  // The select starts on the first type, like Infosys's
  public chosenId: number | null = null;

  public ngOnInit(): void {
    this.http.get<ParticipantTickets>(`/api/participants/${this.participantId}/tickets`).subscribe({
      next: page => {
        this.page.set(page);
        this.tickets.set(page.tickets);
        this.chosenId = page.ticketTypes[0]?.id ?? null;
      },
      error: () => this.notFound.set(true),
    });
  }

  public optionText(option: TicketTypeOption): string {
    return ticketOptionText(option, this.shortDay, this.translation.translate('tickets.partout'));
  }

  public add(): void {
    const chosen = this.page()?.ticketTypes.find(t => t.id === this.chosenId);
    this.tickets.update(tickets => addTicket(tickets, chosen));
  }

  public remove(ticket: TicketTypeOption): void {
    this.tickets.update(tickets => tickets.filter(t => t !== ticket));
  }

  // Infosys goes back to the participant with "Deltageren blev opdateret." or "Kunne ikke opdatere deltageren."
  public submit(): void {
    const dto: UpdateParticipantTicketsDto = { ticketTypeIds: this.tickets().map(t => t.id) };
    this.busy.set(true);
    this.http.put(`/api/participants/${this.participantId}/tickets`, dto).subscribe({
      next: () => this.backToParticipant('success', 'updated'),
      error: () => this.backToParticipant('error', 'ticketsUpdateFailed'),
    });
  }

  private backToParticipant(type: 'success' | 'error', key: string): void {
    this.flash.set(type, key);
    void this.router.navigate(['/participants', this.participantId]);
  }
}
