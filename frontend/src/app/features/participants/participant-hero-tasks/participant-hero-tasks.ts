import { Component, Input } from '@angular/core';
import { ParticipantHeroTask } from '@shared/types';
import { Card } from '@shared/components';
import { TimeSpanPipe, TranslatePipe } from '@shared/pipes';

// What Infosys shows in bold for a shift: the task and how full the shift is, e.g. "Opvask (3 / 4)"
export function heroTaskText(task: ParticipantHeroTask): string {
  return `${task.name} (${task.participantCount} / ${task.capacity})`;
}

// The Heltestyrke, Opgaver box of the Infosys participant page (visdeltager.phtml, .gdsbox next to Aktiviteter): the
// hero force shifts the participant was put on, by start, with the task, how full the shift is and when it is
// ("Onsdag 10:00 -> Onsdag 14:00").
//
// The hero force is not ported yet, so the page passes null and the box says so. When it is ported, the API fills
// programme.heroTasks (see ParticipantHeroTaskDto in ParticipantsController) and this box shows the shifts as they
// are. Add then Infosys's "Ret/slet" link (deltager/updategds), which it hides from its Read-only roles.
@Component({
  selector: 'app-participant-hero-tasks',
  imports: [Card, TimeSpanPipe, TranslatePipe],
  templateUrl: './participant-hero-tasks.html',
  styleUrl: './participant-hero-tasks.scss',
})
export class ParticipantHeroTasksComponent {
  // A decorator input, like Card's: the unit tests run Angular in JIT mode, which doesn't know signal inputs
  @Input({ required: true }) public tasks: ParticipantHeroTask[] | null = null;
  public readonly taskText = heroTaskText;
}
