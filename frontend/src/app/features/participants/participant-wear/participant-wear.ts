import { Component, Input } from '@angular/core';
import { ParticipantWearOrder } from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';

// What Infosys shows for one order, without its "Udleveret"/"Ikke udleveret":
// "2 stk. T-shirt-Sort, str. L — 300,- (150,-)". Infosys has a stray space before ", str."; it is left out.
export function wearOrderText(order: ParticipantWearOrder, pieces: string, size: string): string {
  const sizeText = order.size ? `, ${size} ${order.size}` : '';
  return `${order.amount} ${pieces} ${order.name}${sizeText} — ${order.unitPrice * order.amount},- (${order.unitPrice},-)`;
}

// The Wear box of the Infosys participant page (visdeltager.phtml, #wear_info): one line per order with the amount,
// name, size, price and whether it has been handed out.
//
// Wear is not ported yet, so the page passes null and the box says so. When wear is ported, the API fills
// details.wearOrders (see ParticipantWearOrderDto in ParticipantsController) and this box shows the orders as is.
// Infosys's "Ret/slet" link goes to its Indgang/Mad/Wear edit page (deltager/updatemadwear); add it when that page
// is ported.
@Component({
  selector: 'app-participant-wear',
  imports: [Card, TranslatePipe],
  templateUrl: './participant-wear.html',
  styleUrl: './participant-wear.scss',
})
export class ParticipantWearComponent {
  // A decorator input, like Card's: the unit tests run Angular in JIT mode, which doesn't know signal inputs
  @Input({ required: true }) public orders: ParticipantWearOrder[] | null = null;
  public readonly wearOrderText = wearOrderText;
}
