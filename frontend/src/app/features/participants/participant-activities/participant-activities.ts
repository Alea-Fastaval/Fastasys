import { Component, Input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ParticipantActivity } from '@shared/types';
import { Card } from '@shared/components';
import { TimeSpanPipe, TranslatePipe } from '@shared/pipes';
import { roomLabel } from '../../runs/run-form/run-form';

// The Sted column: "room, location". Infosys writes "beskrivelse, skole" as is, so a room without a location ends in
// ", "; Fastasys trims that like the run form does (agreed 2026-11-13). A row without a room (a designer's schedule
// without a meeting room) is left empty; Infosys only printed ", " there because PHP read the name of a room that
// doesn't exist.
export function activityRoomText(activity: ParticipantActivity): string {
  return activity.roomName === null
    ? ''
    : roomLabel({ name: activity.roomName, location: activity.roomLocation ?? '' });
}

// The Aktiviteter box of the Infosys participant page (visdeltager.phtml, .aktivitetboxen): one row per activity
// the participant has a seat on, with links to the activity and the run, plus the designer rows Infosys adds for
// board game designers (see ParticipantActivityDto in ParticipantsController). Infosys shows no table at all when
// there are no rows.
//
// Infosys's "Ret/slet" link (deltager/updateaktiviteter, hidden when the participant is activity-locked) comes with
// the port of that page. Infosys also links the room to its room page, which isn't ported.
@Component({
  selector: 'app-participant-activities',
  imports: [Card, RouterLink, TimeSpanPipe, TranslatePipe],
  templateUrl: './participant-activities.html',
  styleUrl: './participant-activities.scss',
})
export class ParticipantActivitiesComponent {
  // A decorator input, like Card's: the unit tests run Angular in JIT mode, which doesn't know signal inputs
  @Input({ required: true }) public activities: ParticipantActivity[] = [];
  public readonly roomText = activityRoomText;
}
