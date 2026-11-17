import { ActivityType } from './activities.types';

// Infosys pladser.type: spiller, spilleder
export const RUN_SEAT_ROLES = ['Player', 'GameMaster'] as const;
export type RunSeatRole = (typeof RUN_SEAT_ROLES)[number];

export interface RunSeat {
  seatNumber: number;
  participantId: number;
  participantName: string;
  // Karma is not ported yet and is always null
  karma: number | null;
  role: RunSeatRole;
}

// Infosys: hold
export interface Run {
  id: number;
  number: number;
  scheduleId: number;
  activityId: number;
  activityTitle: string;
  startTime: string;
  endTime: string;
  roomId: number;
  roomName: string;
  needsPlayers: boolean;
  needsGameMasters: boolean;
  canUsePlayers: boolean;
  seats: RunSeat[];
}

export interface RunSummary {
  id: number;
  number: number;
  activityId: number;
  activityTitle: string;
  activityType: ActivityType;
  startTime: string;
  endTime: string;
  roomId: number;
  roomName: string;
  needsPlayers: boolean;
  needsGameMasters: boolean;
}

export interface RunRoom {
  id: number;
  name: string;
  location: string;
}

export interface CreateRunDto {
  scheduleId: number;
}

export interface UpdateRunDto {
  roomId: number;
}

export interface CreateRunSeatDto {
  participantId: number;
  role: RunSeatRole;
}
