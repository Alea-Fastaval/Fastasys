import { Component, Input } from '@angular/core';
import { ParticipantPrice } from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';

// An amount as Infosys shows it: the whole number of kroner followed by ",-", e.g. "450,-" or "-150,-"
export function priceText(amount: number): string {
  return `${amount},-`;
}

// The lines of the box that Infosys works out in the template (visdeltager.phtml) instead of in Deltagere
export interface PriceDifferences {
  // "Difference for tilmelding": what is left to pay for the signup (negative when the participant paid too much)
  signup: number;
  // "Pris for ikke tildelte aktiviteter": activities signed up for but not given a seat on
  unassignedActivities: number;
  // "Difference ved check-ind": what is left to pay at check-in
  checkIn: number;
}

export function priceDifferences(price: ParticipantPrice, amountPaid: number): PriceDifferences {
  return {
    signup: price.signupTotal - amountPaid,
    unassignedActivities: price.signedUpActivities - price.assignedActivities,
    checkIn: price.checkInTotal - amountPaid,
  };
}

// The Pris breakdown box of the Infosys participant page (visdeltager.phtml, #prisbreakdown): the price from the
// signup, what the participant's tickets, food, wear, activities and the like cost, what they have paid, and the
// difference both for the signup and at check-in. Unlike the other boxes, Infosys shows it to its Read-only roles.
//
// Prices are not ported yet, so the page passes null and the box says so. When tickets, food, wear and activity
// signups are ported, the API fills price (see ParticipantPriceDto in ParticipantsController, which also works out
// the totals) and this box shows it as is.
@Component({
  selector: 'app-participant-price',
  imports: [Card, TranslatePipe],
  templateUrl: './participant-price.html',
  styleUrl: './participant-price.scss',
})
export class ParticipantPriceComponent {
  // Decorator inputs, like Card's: the unit tests run Angular in JIT mode, which doesn't know signal inputs
  @Input({ required: true }) public price: ParticipantPrice | null = null;
  // "Forudbetalt", which the box shows and subtracts
  @Input({ required: true }) public amountPaid = 0;

  public readonly priceText = priceText;

  public differences(price: ParticipantPrice): PriceDifferences {
    return priceDifferences(price, this.amountPaid);
  }

  // Infosys writes both amounts into the label: "Difference ved check-ind (905 - 450):"
  public checkInLabelParams(price: ParticipantPrice): Record<string, string> {
    return { total: price.checkInTotal.toString(), paid: this.amountPaid.toString() };
  }
}
