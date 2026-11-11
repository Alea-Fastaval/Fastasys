// Infosys deltagere.main_lang (da, en)
export const PREFERRED_LANGUAGES = ['Danish', 'English'] as const;
export type PreferredLanguage = (typeof PREFERRED_LANGUAGES)[number];

// Infosys deltagere.sprog (dansk, engelsk, skandinavisk)
export const PARTICIPANT_LANGUAGES = ['Danish', 'English', 'Scandinavian'] as const;
export type ParticipantLanguage = (typeof PARTICIPANT_LANGUAGES)[number];

// Infosys deltagere.sleeping_area
export const SLEEPING_AREAS = ['Regular', 'Sober', 'Youth', 'Organizer', 'Family'] as const;
export type SleepingArea = (typeof SLEEPING_AREAS)[number];

// Infosys deltager/visalle: id and "fornavn efternavn"
export interface ParticipantSummary {
  id: number;
  name: string;
}

// Infosys deltager/visdeltager. details is null when the user may only view participants.
export interface Participant {
  id: number;
  firstName: string;
  lastName: string;
  nickname: string;
  email: string;
  phoneNumber: string;
  addressLine1: string;
  addressLine2: string;
  zipCode: string;
  city: string;
  countryCode: string;
  countryName: string;
  // yyyy-MM-dd, or null when unknown
  birthDate: string | null;
  pronouns: string;
  preferredLanguage: PreferredLanguage;
  categoryId: number;
  categoryName: string;
  amountPaid: number;
  smsSignup: boolean;
  previousId: number | null;
  nextId: number | null;
  canEdit: boolean;
  details: ParticipantDetails | null;
}

export interface ParticipantDetails {
  organizerAreaId: number | null;
  organizerAreaName: string | null;
  isAuthorOrDesigner: boolean;
  gameActivityId: number | null;
  gameActivityTitle: string | null;
  extraVouchers: number;
  activityLanguages: ParticipantLanguage[];
  sleepingAreas: SleepingArea[];
  desiredHeroTasks: number;
  desiredActivities: number;
  superGameMaster: boolean;
  maxGameMasterRuns: number | null;
  juniorGameMaster: boolean;
  superRulesGuide: boolean;
  maxRulesGuideGames: number | null;
  superHero: boolean;
  setupHelpMonday: boolean;
  setupHelpTuesday: boolean;
  financialSupport: boolean;
  isNoShow: boolean;
  isAnnulled: boolean;
  isActivityLocked: boolean;
  // UTC instants
  signedUpAt: string;
  checkedInAt: string | null;
  youthClub: string;
  photoConsent: boolean;
  adminNote: string;
  messageToParticipant: string;
  paymentNote: string;
  // The notes from the signup; null when the participant didn't give one
  commentNote: string | null;
  heroForceNote: string | null;
  juniorContact: string | null;
  juniorComment: string | null;
  sleepingNote: string | null;
}

export interface ParticipantCategory {
  id: number;
  name: string;
}

export interface OrganizerArea {
  id: number;
  nameDanish: string;
  nameEnglish: string | null;
}

export interface Country {
  code: string;
  nameDanish: string;
  nameEnglish: string;
}

export interface GameOption {
  id: number;
  title: string;
  titleEnglish: string;
}

export interface ParticipantOptions {
  categories: ParticipantCategory[];
  organizerAreas: OrganizerArea[];
  countries: Country[];
  games: GameOption[];
}

// The fields of the Infosys create form
export interface ParticipantFieldsDto {
  birthDate: string | null;
  categoryId: number;
  amountPaid: number;
  firstName: string;
  lastName: string;
  email: string;
  addressLine1: string;
  addressLine2: string;
  zipCode: string;
  city: string;
  phoneNumber: string;
  smsSignup: boolean;
  preferredLanguage: PreferredLanguage;
  superHero: boolean;
  isAuthorOrDesigner: boolean;
  setupHelpMonday: boolean;
  setupHelpTuesday: boolean;
  desiredActivities: number;
  desiredHeroTasks: number;
  isNoShow: boolean;
  superGameMaster: boolean;
  youthClub: string;
  countryCode: string;
  // null is "Intet arrangørområde" / "Intet scenarie/brætspil"
  organizerAreaId: number | null;
  gameActivityId: number | null;
  // Only used when at least one is ticked
  activityLanguages: ParticipantLanguage[];
}

export type CreateParticipantDto = ParticipantFieldsDto;

// The edit form adds Annulleret
export interface UpdateParticipantDto extends ParticipantFieldsDto {
  isAnnulled: boolean;
}
