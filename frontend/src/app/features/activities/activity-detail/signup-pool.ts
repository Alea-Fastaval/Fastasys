import { RunSeatRole, SignupPoolRow } from '@shared/types';

// Dragging on the activity page (Infosys public/js/activity.js, FVActivity): a row of a schedule's "Tilmeldinger"
// table, or a participant on one of its runs
export interface DragItem {
  scheduleId: number;
  participantId: number;
  // The role the participant gets on the run they are dropped on
  role: RunSeatRole;
  // The run the participant is dragged from, or null for a row of the table
  fromRunId: number | null;
}

// What a drop does to the participant's seats
export interface SeatChange {
  removeFromRunId: number | null;
  add: { runId: number; role: RunSeatRole } | null;
}

// Infosys Participant.dropElement: dropped on a run of the same schedule, the participant gets a seat there (and
// leaves the run they were dragged from); dropped anywhere else, a participant dragged from a run leaves it. Dropping
// a row of the table anywhere but on a run, or a participant on their own run, does nothing.
export function seatChangeForDrop(item: DragItem, targetRunId: number | null): SeatChange | null {
  if (targetRunId === null) {
    return item.fromRunId === null ? null : { removeFromRunId: item.fromRunId, add: null };
  }
  if (targetRunId === item.fromRunId) return null;
  return { removeFromRunId: item.fromRunId, add: { runId: targetRunId, role: item.role } };
}

// Infosys can only drag a participant off a run when they have a row in the schedule's table
// (FVActivity.locateParticipantForRow), and they then get the role of their last row there: their player wish when
// they also want to run it. An activity-locked participant can't be dragged.
export function seatDragRole(rows: SignupPoolRow[], participantId: number): RunSeatRole | null {
  const own = rows.filter(row => row.participantId === participantId);
  if (own.length === 0 || own.some(row => row.isLocked)) return null;
  return own[own.length - 1].role;
}

// Infosys FVActivity.startParticipantDrag: busy and activity-locked rows can't be dragged
export function canDragRow(row: SignupPoolRow): boolean {
  return !row.isBusy && !row.isLocked;
}

// Infosys hides the rows of participants who are on a team (tr.on-team { display: none })
export function visibleRows(rows: SignupPoolRow[]): SignupPoolRow[] {
  return rows.filter(row => !row.isOnTeam);
}
