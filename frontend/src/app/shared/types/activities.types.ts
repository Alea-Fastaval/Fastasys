import { Run, RunSeatRole } from './runs.types';
import { ParticipantLanguage } from './participants.types';

// The "Tilmeldinger" table under a schedule on the activity page (see ScheduleSignupsDto in the API)
export interface ScheduleSignups {
  scheduleId: number;
  // Game masters first, then by priority. Rows with isOnTeam are hidden, like Infosys's "on-team".
  signups: SignupPoolRow[];
}

export interface SignupPoolRow {
  participantId: number;
  // "Fornavn Efternavn"
  name: string;
  role: RunSeatRole;
  priority: number;
  // Not ported yet: always null
  karma: number | null;
  // null when the birth date is unknown
  age: number | null;
  language: 'dk' | 'uk' | null;
  activityLanguages: ParticipantLanguage[];
  desiredActivities: number;
  // The heart: null for none, true for a filled one, false for an empty one
  desiredActivitiesReached: boolean | null;
  // "Andre kommentarer"; only sent to users who may edit participants
  note: string | null;
  isOnTeam: boolean;
  isBusy: boolean;
  isLocked: boolean;
  isDoubleBooked: boolean;
}

// Display order of the types (list grouping and type dropdown)
export const ACTIVITY_TYPES = ['Scenario', 'Quest', 'BoardGame', 'Junior', 'System'] as const;
export type ActivityType = (typeof ACTIVITY_TYPES)[number];

export const ACTIVITY_LANGUAGES = ['Danish', 'English', 'DanishAndEnglish'] as const;
export type ActivityLanguage = (typeof ACTIVITY_LANGUAGES)[number];

// Same option order as the Infosys karmatype select
export const KARMA_TYPES = ['None', 'Small', 'Medium', 'Big'] as const;
export type KarmaType = (typeof KARMA_TYPES)[number];

export const AUTO_SIGNUP_CATEGORIES = ['None', 'All', 'Organizer', 'Author', 'Designer'] as const;
export type AutoSignupCategory = (typeof AUTO_SIGNUP_CATEGORIES)[number];

export interface ActivitySummary {
  id: number;
  title: string;
  titleEnglish: string;
  type: ActivityType;
}

export interface Activity {
  id: number;
  title: string;
  titleEnglish: string;
  author: string;
  type: ActivityType;
  descriptionDanish: string;
  descriptionEnglish: string;
  teaserDanish: string;
  teaserEnglish: string;
  note: string;
  durationHours: number;
  minPlayersPerTeam: number;
  maxPlayersPerTeam: number;
  gameMastersPerTeam: number;
  price: number;
  maxSignups: number;
  minAge: number | null;
  canSignUp: boolean;
  isHidden: boolean;
  isRoomExclusive: boolean;
  isTimeExclusive: boolean;
  isReplayable: boolean;
  playerLanguage: ActivityLanguage;
  gameMasterLanguage: ActivityLanguage;
  karmaType: KarmaType;
  autoSignupCategory: AutoSignupCategory;
  wordPressId: number;
  updatedAt: string;
}

// The fields of the create and edit forms, which are the same
export interface ActivityFieldsDto {
  title: string;
  titleEnglish: string;
  author: string;
  type: ActivityType;
  descriptionDanish: string;
  descriptionEnglish: string;
  note: string;
  durationHours: number;
  minPlayersPerTeam: number;
  maxPlayersPerTeam: number;
  gameMastersPerTeam: number;
  price: number;
  maxSignups: number;
  minAge: number | null;
  canSignUp: boolean;
  isHidden: boolean;
  isRoomExclusive: boolean;
  isTimeExclusive: boolean;
  isReplayable: boolean;
  playerLanguage: ActivityLanguage;
  gameMasterLanguage: ActivityLanguage;
  karmaType: KarmaType;
  autoSignupCategory: AutoSignupCategory;
  wordPressId: number;
}

export type CreateActivityDto = ActivityFieldsDto;

export type UpdateActivityDto = ActivityFieldsDto;

// Infosys: afvikling. The room is the meeting room; each run has its own room.
export interface ActivitySchedule {
  id: number;
  activityId: number;
  startTime: string;
  endTime: string;
  roomId: number | null;
  roomName: string | null;
  runs: Run[];
}

// Times are ISO instants; null room means no meeting room
export interface CreateScheduleDto {
  startTime: string;
  endTime: string;
  roomId: number | null;
}

// Infosys aktivitet/editafvikling: the same fields as creating
export type UpdateScheduleDto = CreateScheduleDto;
