import { describe, it, expect } from 'vitest';
import { SignupPoolRow } from '@shared/types';
import { DragItem, canDragRow, seatChangeForDrop, seatDragRole, visibleRows } from './signup-pool';

const row = (overrides: Partial<SignupPoolRow>): SignupPoolRow => ({
  participantId: 1,
  name: 'Anna',
  role: 'Player',
  priority: 1,
  karma: null,
  age: null,
  language: null,
  activityLanguages: [],
  desiredActivities: 0,
  desiredActivitiesReached: null,
  note: null,
  isOnTeam: false,
  isBusy: false,
  isLocked: false,
  isDoubleBooked: false,
  ...overrides,
});

describe('seatChangeForDrop (Infosys Participant.dropElement)', () => {
  const fromTable: DragItem = { scheduleId: 9, participantId: 1, role: 'GameMaster', fromRunId: null };
  const fromRun: DragItem = { ...fromTable, role: 'Player', fromRunId: 20 };

  it('places a row of the table on the run it is dropped on', () => {
    expect(seatChangeForDrop(fromTable, 21)).toEqual({ removeFromRunId: null, add: { runId: 21, role: 'GameMaster' } });
  });

  it('moves a participant from their run to another', () => {
    expect(seatChangeForDrop(fromRun, 21)).toEqual({ removeFromRunId: 20, add: { runId: 21, role: 'Player' } });
  });

  it('takes a participant dropped anywhere but on a run off their run', () => {
    expect(seatChangeForDrop(fromRun, null)).toEqual({ removeFromRunId: 20, add: null });
  });

  it('does nothing with a row dropped anywhere but on a run, or a participant dropped on their own run', () => {
    expect(seatChangeForDrop(fromTable, null)).toBeNull();
    expect(seatChangeForDrop(fromRun, 20)).toBeNull();
  });
});

describe('seatDragRole (dragging a participant off a run)', () => {
  it("gives the role of the participant's last row in the table: the player wish when there is one", () => {
    const rows = [row({ role: 'GameMaster', priority: 0 }), row({ role: 'Player', priority: 2 })];
    expect(seatDragRole(rows, 1)).toBe('Player');
    expect(seatDragRole([row({ role: 'GameMaster' })], 1)).toBe('GameMaster');
  });

  it("can't drag a participant without a row, or an activity-locked one", () => {
    expect(seatDragRole([row({ participantId: 2 })], 1)).toBeNull();
    expect(seatDragRole([row({ isLocked: true })], 1)).toBeNull();
  });
});

describe('canDragRow and visibleRows', () => {
  it("can't drag busy or activity-locked rows", () => {
    expect(canDragRow(row({}))).toBe(true);
    expect(canDragRow(row({ isBusy: true }))).toBe(false);
    expect(canDragRow(row({ isLocked: true }))).toBe(false);
  });

  it('hides the rows of participants on a team', () => {
    expect(
      visibleRows([row({ participantId: 1 }), row({ participantId: 2, isOnTeam: true })]).map(r => r.participantId),
    ).toEqual([1]);
  });
});
