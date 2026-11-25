import { Component, Input, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ParticipantMeal, ParticipantTicket, TicketLine } from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { TranslationService } from '@shared/services';
import { Weekday, toConventionTime, weekdayOfDate } from '@shared/utils';

// The translated words a line of tickets is made of
export interface TicketTexts {
  partout: string;
  // The short name of a day, e.g. "Ons"
  shortDay: (weekday: Weekday) => string;
}

// What Infosys shows on one line of the box: the line's tickets joined with ", ". On the Indgang and Overnatning
// lines a ticket is "Partout" or the short name of its day ("Ons") (Indgang::getShortDescription). On the Andet line
// it is the ticket's name (Indgang::getDescription). Infosys's " (Digital)" was dropped (the user, 2026-11-25).
export function ticketLineText(tickets: ParticipantTicket[], line: TicketLine, texts: TicketTexts): string {
  return tickets
    .filter(ticket => ticket.line === line)
    .map(ticket => (line === 'Other' ? ticket.name : ticketShortText(ticket, texts)))
    .join(', ');
}

function ticketShortText(ticket: ParticipantTicket, texts: TicketTexts): string {
  return ticket.isAllConvention ? texts.partout : texts.shortDay(weekdayOfDate(ticket.day));
}

// What Infosys shows for a meal, without its "Udleveret"/"Ikke udleveret": the short name of the meal's day and
// the food, e.g. "Ons Aftensmad"
export function mealText(meal: ParticipantMeal, shortDay: (weekday: Weekday) => string): string {
  return `${shortDay(toConventionTime(meal.time).weekday)} ${meal.category}`;
}

// The Indgang/Mad box of the Infosys participant page (visdeltager.phtml, #indgang_mad_info): the participant's
// tickets on the lines Indgang, Overnatning and Andet, and a list of their meals with whether each has been handed
// out. Each line is shown even when it is empty, like Infosys does.
//
// Infosys's "Ret/slet" link goes to its Indgang/Mad/Wear edit page (deltager/updatemadwear), here
// /participants/:id/entrance-food-wear, shown to users who may change a participant's tickets or meals.
@Component({
  selector: 'app-participant-entrance-food',
  imports: [RouterLink, MatButtonModule, MatIconModule, Card, TranslatePipe],
  templateUrl: './participant-entrance-food.html',
  styleUrl: './participant-entrance-food.scss',
})
export class ParticipantEntranceFoodComponent {
  private readonly translationService = inject(TranslationService);

  // Decorator inputs, like Card's: the unit tests run Angular in JIT mode, which doesn't know signal inputs
  @Input({ required: true }) public tickets: ParticipantTicket[] = [];
  @Input({ required: true }) public meals: ParticipantMeal[] = [];
  @Input({ required: true }) public participantId = 0;
  @Input() public canEdit = false;

  public lineText(tickets: ParticipantTicket[], line: TicketLine): string {
    return ticketLineText(tickets, line, {
      partout: this.translate('participants.entranceFood.partout'),
      shortDay: weekday => this.shortDay(weekday),
    });
  }

  public mealText(meal: ParticipantMeal): string {
    return mealText(meal, weekday => this.shortDay(weekday));
  }

  public receivedKey(meal: ParticipantMeal): string {
    return meal.isReceived ? 'participants.entranceFood.received' : 'participants.entranceFood.notReceived';
  }

  private shortDay(weekday: Weekday): string {
    return this.translate(`common.weekdaysShort.${weekday}`);
  }

  private translate(key: string): string {
    // Reading the signal makes the template update when the language changes, like the translate pipe
    this.translationService.translations();
    return this.translationService.translate(key);
  }
}
