import { Component, Input } from '@angular/core';
import { ParticipantSleepingPlace } from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { toConventionTime } from '@shared/utils';

// A day in the Soveplads box's Tidsrum column, like Infosys's PHP date('l, j/n'): "Wednesday, 24/3".
// Infosys uses English day names here, also on its Danish page; that is kept for now.
export function sleepingDayText(instant: string): string {
  const time = toConventionTime(instant);
  return `${time.weekday}, ${time.day}/${time.month}`;
}

// The Soveplads box of the Infosys participant page (visdeltager.phtml, #sleeping): a table with one row per
// period the participant sleeps somewhere at Fastaval, "Wednesday, 24/3 -> Thursday, 25/3", and the room.
//
// Sleeping places are not ported yet, so the page passes null and the box says so. When they are ported, the API
// fills details.sleepingPlaces (see ParticipantSleepingPlaceDto in ParticipantsController) and this box shows them
// as is. Infosys's "Ret/slet" link goes to its sleeping edit page (update_participant_sleeping); add it when that
// page is ported.
@Component({
  selector: 'app-participant-sleeping',
  imports: [Card, TranslatePipe],
  templateUrl: './participant-sleeping.html',
  styleUrl: './participant-sleeping.scss',
})
export class ParticipantSleepingComponent {
  // A decorator input, like Card's: the unit tests run Angular in JIT mode, which doesn't know signal inputs
  @Input({ required: true }) public places: ParticipantSleepingPlace[] | null = null;
  public readonly sleepingDayText = sleepingDayText;
}
