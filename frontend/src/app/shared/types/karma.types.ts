import { KarmaType } from './activities.types';
import { RunSeatRole } from './runs.types';

export interface ActivitySignup {
  isCancelled: boolean;
  id: number;
  participantId: number;
  scheduleId: number;
  activityId: number;
  activityTitle: string;
  startTime: string;
  endTime: string;
  role: string;
  priority: number | null;
  signedUpAt: string;
}

export interface ActivitySignupFields {
  scheduleId: number;
  role: RunSeatRole;
  priority: number;
}

export interface KarmaContribution {
  count: number;
  points: number;
}

export interface KarmaResult {
  total: number;
  signups: (KarmaContribution & { priority: number })[];
  assignments: (KarmaContribution & { karmaType: KarmaType })[];
  exclusions: {
    nonTimeExclusiveSignups: number;
    unrankedPlayerSignups: number;
    nonTimeExclusiveAssignments: number;
    gameMasterAssignments: number;
    unknownSignupRoles: number;
    cancelledSignups: number;
    cancelledAssignments: number;
  };
}

export interface KarmaExcludedInput {
  source: 'Signup' | 'Assignment';
  scheduleId: number;
  activityId: number;
  activityTitle: string;
  runId: number | null;
  role: string;
  priority: number | null;
  reason:
    'NonTimeExclusive' | 'UnrankedPlayerSignup' | 'GameMasterAssignment' | 'UnknownSignupRole' | 'CancelledSchedule';
}

export interface ParticipantKarma {
  participantId: number;
  participantName: string;
  karma: KarmaResult;
  excludedInputs: KarmaExcludedInput[];
}
