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
  description: string;
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
  maxAge: number | null;
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

export interface SaveActivityDto {
  title: string;
  titleEnglish: string;
  author: string;
  type: ActivityType;
  description: string;
  descriptionEnglish: string;
  note: string;
  durationHours: number;
  minPlayersPerTeam: number;
  maxPlayersPerTeam: number;
  gameMastersPerTeam: number;
  price: number;
  maxSignups: number;
  minAge: number | null;
  maxAge: number | null;
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

export interface ActivitySchedule {
  id: number;
  activityId: number;
  startTime: string;
  endTime: string;
  roomId?: number;
}
