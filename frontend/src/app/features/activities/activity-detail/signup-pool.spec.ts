import { describe, it, expect } from 'vitest';
import { SignupPoolRow } from '@shared/types';
import { DragItem, canDragRow, canDragSeat, seatChangeForDrop, visibleRows } from './signup-pool';

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

describe('canDragSeat (dragging a participant off a run)', () => {
  it('needs a row of the participant in the table, like Infosys', () => {
    expect(canDragSeat([row({ role: 'GameMaster', priority: 0 }), row({ role: 'Player', priority: 2 })], 1)).toBe(true);
    expect(canDragSeat([row({ participantId: 2 })], 1)).toBe(false);
  });

  it("can't drag an activity-locked participant", () => {
    expect(canDragSeat([row({ isLocked: true })], 1)).toBe(false);
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
