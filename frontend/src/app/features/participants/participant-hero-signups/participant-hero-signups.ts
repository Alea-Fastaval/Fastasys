import { Component, Input, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ParticipantHeroSignup } from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { TranslationService } from '@shared/services';
import { Weekday, toConventionTime } from '@shared/utils';

// The day of a date (yyyy-MM-dd, a day in Denmark). Noon UTC is the same day in Copenhagen all year.
export function weekdayOf(date: string): Weekday {
  return toConventionTime(`${date}T12:00:00Z`).weekday;
}

// The period Infosys shows for a signup: the day's name and the hours, e.g. "Onsdag 7-12"
// (DeltagereGDSTilmeldinger::getMeaningfulPeriod: date('l ' . hours) with danishDayNames)
export function heroSignupPeriodText(
  signup: Pick<ParticipantHeroSignup, 'date' | 'hours'>,
  dayName: (weekday: Weekday) => string,
): string {
  return `${dayName(weekdayOf(signup.date))} ${signup.hours}`;
}

// The Heltestyrken, Tilmeldinger box of the Infosys participant page (visdeltager.phtml, .gdsbox next to
// Aktivitetstilmeldinger): when the participant said they can help the hero force, the category in bold and the period
// ("Onsdag 7-12"), by day and time. A period for any category (the signup's) shows "Alle"; Infosys showed no name.
// Infosys's "Ret/slet" link (deltager/updategdstilmeldinger) goes to /participants/:id/hero-availability, for users
// who may change a participant's hero force (Infosys hides it from its Read-only roles).
@Component({
  selector: 'app-participant-hero-signups',
  imports: [RouterLink, MatButtonModule, MatIconModule, Card, TranslatePipe],
  templateUrl: './participant-hero-signups.html',
  styleUrl: './participant-hero-signups.scss',
})
export class ParticipantHeroSignupsComponent {
  private readonly translationService = inject(TranslationService);

  // Decorator inputs, like Card's: the unit tests run Angular in JIT mode, which doesn't know signal inputs
  @Input({ required: true }) public signups: ParticipantHeroSignup[] = [];
  @Input({ required: true }) public participantId = 0;
  @Input() public canEdit = false;

  public periodText(signup: ParticipantHeroSignup): string {
    // Reading the signal makes the template update when the language changes, like the translate pipe
    this.translationService.translations();
    return heroSignupPeriodText(signup, weekday => this.translationService.translate(`common.weekdays.${weekday}`));
  }
}
