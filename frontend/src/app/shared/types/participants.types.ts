import { RunSeatRole } from './runs.types';

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

// Infosys search box: Søgelogik, the combination with the previous result, and Fødselsdato Før/Efter/Samme
export const SEARCH_LOGICS = ['And', 'Or'] as const;
export type SearchLogic = (typeof SEARCH_LOGICS)[number];
export const SEARCH_COMBINATIONS = ['New', 'Intersection', 'Union', 'Difference'] as const;
export type SearchCombination = (typeof SEARCH_COMBINATIONS)[number];
export const BIRTH_DATE_RANGES = ['Before', 'After', 'Same'] as const;
export type BirthDateRange = (typeof BIRTH_DATE_RANGES)[number];

// The fields of the Infosys search box; a field that is left out is not used
export interface ParticipantCriteria {
  id?: string;
  birthDate?: string;
  birthDateRange?: BirthDateRange;
  categoryId?: number;
  firstName?: string;
  lastName?: string;
  nickname?: string;
  email?: string;
  amountPaid?: string;
  address?: string;
  zipCode?: string;
  city?: string;
  country?: string;
  phoneNumber?: string;
  smsSignup?: boolean;
  activityLanguages?: ParticipantLanguage[];
  superHero?: boolean;
  desiredHeroTasks?: number;
  setupHelpMonday?: boolean;
  setupHelpTuesday?: boolean;
  isAuthorOrDesigner?: boolean;
  desiredActivities?: number;
  superGameMaster?: boolean;
  financialSupport?: boolean;
  isNoShow?: boolean;
  isAnnulled?: boolean;
  youthClub?: string;
  organizerAreaId?: number;
  game?: string;
  checkedInAt?: string;
  notes?: string;
}

export interface QuickSearchStep {
  quick: string;
}

export interface CriteriaSearchStep {
  criteria: ParticipantCriteria;
  logic: SearchLogic;
  combination: SearchCombination;
}

// One search in the URL: the quick search from the page header or the search box
export type ParticipantSearchStep = QuickSearchStep | CriteriaSearchStep;

// One row of the participant list: id, "fornavn efternavn" and the chosen columns
export interface ParticipantRow {
  id: number;
  name: string;
  values: Record<string, unknown>;
}

export interface ParticipantSearchResult {
  // The participant a lone quick search goes straight to
  directId: number | null;
  rows: ParticipantRow[];
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
  // The Pris breakdown box, shown to everyone who can open the page: null until prices are ported
  price: ParticipantPrice | null;
  details: ParticipantDetails | null;
}

// The amounts of the Pris breakdown box in whole DKK (see ParticipantPriceDto in the API)
export interface ParticipantPrice {
  // "Oprindelig pris fra tilmelding": the price the signup calculated; not part of the totals
  originalPrice: number;
  // "Indgang + overnatning"
  entry: number;
  food: number;
  wear: number;
  // "Aktiviteter": the activities the participant signed up for
  signedUpActivities: number;
  // The activities the participant was given a seat on
  assignedActivities: number;
  // "Andet (fest, gebyrer, etc)"
  other: number;
  alea: number;
  // "Pris for tilmelding": calculated by the API from the lines above with signedUpActivities
  signupTotal: number;
  // "Total pris ved chek-ind": the same with assignedActivities
  checkInTotal: number;
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
  // The Wear box: null until wear is ported, then the participant's orders
  wearOrders: ParticipantWearOrder[] | null;
  // The Indgang/Mad box: null until entrance and food are ported, then the participant's tickets and meals
  tickets: ParticipantTicket[] | null;
  meals: ParticipantMeal[] | null;
  // The Soveplads box: null until sleeping places are ported, then where the participant sleeps
  sleepingPlaces: ParticipantSleepingPlace[] | null;
  // The Aktiviteter box, in start time order
  activities: ParticipantActivity[];
  // The Aktivitetstilmeldinger box: the signup wishes, by the schedule's start
  activitySignups: ParticipantActivitySignup[];
}

// One row of the Aktivitetstilmeldinger box (see ParticipantActivitySignupDto in the API). Priority is 1-4 for players;
// a game master wish has 0 (from the Infosys signup) or 1.
export interface ParticipantActivitySignup {
  activityId: number;
  title: string;
  titleEnglish: string;
  scheduleId: number;
  // UTC instants
  start: string;
  end: string;
  priority: number;
  role: RunSeatRole;
}

// What the participant is on a row of the Aktiviteter box: spiller, spilleder, or designer of their own game
export type ParticipantActivityRole = 'Player' | 'GameMaster' | 'Designer';

// One row of the Aktiviteter box (see ParticipantActivityDto in the API)
export interface ParticipantActivity {
  activityId: number;
  title: string;
  // UTC instants; the box shows them in Copenhagen time
  start: string;
  end: string;
  role: ParticipantActivityRole;
  // null for a designer's own game and for automatic signups, which have no run
  runId: number | null;
  runNumber: number | null;
  // null when there is no room (a schedule without a meeting room)
  roomName: string | null;
  roomLocation: string | null;
}

// The line of the Indgang/Mad box a ticket is shown on: Indgang, Overnatning or Andet
export type TicketLine = 'Entrance' | 'Sleeping' | 'Other';

// One ticket in the Indgang/Mad box (see ParticipantTicketDto in the API)
export interface ParticipantTicket {
  line: TicketLine;
  // Shown as "Partout" instead of a day
  isAllConvention: boolean;
  // UTC instant; the box shows its day in Copenhagen
  start: string;
  isDigital: boolean;
  // What the Andet line shows, e.g. "Fest"
  name: string;
}

// One meal in the Indgang/Mad box
export interface ParticipantMeal {
  // UTC instant; the box shows its day in Copenhagen
  time: string;
  // The food's name, e.g. "Aftensmad"
  category: string;
  isReceived: boolean;
}

// One row of the Soveplads box
export interface ParticipantSleepingPlace {
  // UTC instants; the box shows their days in Copenhagen
  starts: string;
  ends: string;
  // The room's name
  room: string;
}

// One order in the Wear box on the Infosys participant page
export interface ParticipantWearOrder {
  amount: number;
  // The wear's name with its model/design/color, e.g. "T-shirt-Sort" (Danish, as Infosys shows it)
  name: string;
  // The size's Danish name, or null when the order has no size
  size: string | null;
  // Whole DKK for one piece
  unitPrice: number;
  isReceived: boolean;
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

// Infosys deltager/updateaktivitet ("Opdater Aktiviteter", the "Ret/slet" link of the Aktiviteter box)
export interface ParticipantSeats {
  participantId: number;
  // The participant's seats on runs, by start time
  seats: ParticipantSeat[];
  // The "Aktivitet" select: the activities that can be signed up for, by title
  activities: SeatActivityOption[];
  // "Deltagerens tilmeldinger": the signup wishes, each with the run its "Tilføj" button gives a seat on
  wishes: SeatWish[];
}

// runId is null, and "Tilføj" is not shown, when no run has room or the participant is busy then
export interface SeatWish {
  scheduleId: number;
  role: RunSeatRole;
  priority: number;
  title: string;
  titleEnglish: string;
  start: string;
  end: string;
  runId: number | null;
  runNumber: number | null;
}

export interface ParticipantSeat {
  runId: number;
  runNumber: number;
  role: RunSeatRole;
  title: string;
  titleEnglish: string;
  // UTC instants
  start: string;
  end: string;
}

export interface SeatActivityOption {
  id: number;
  title: string;
  titleEnglish: string;
  schedules: SeatScheduleOption[];
}

// The "Tid" select; a busy schedule is shown but can't be chosen
export interface SeatScheduleOption {
  id: number;
  start: string;
  end: string;
  isBusy: boolean;
  runs: SeatRunOption[];
}

// The "Hold" select; the role select disables Spiller unless canUsePlayers and Spilleder unless needsGameMasters
export interface SeatRunOption {
  id: number;
  number: number;
  canUsePlayers: boolean;
  needsGameMasters: boolean;
}

// "Opdater": the list replaces the participant's seats
export interface UpdateParticipantSeatsDto {
  seats: { runId: number; role: RunSeatRole }[];
}

// Infosys deltager/updatetilmelding ("Opdater Aktivitets-tilmeldinger") and the row dialog of the Aktivitetstilmeldinger box
export interface ParticipantSignups {
  participantId: number;
  signups: ParticipantActivitySignup[];
  // In id order. The edit page offers those with canSignUp; the row dialog offers the schedules of the wish's activity.
  activities: SignupActivityOption[];
}

export interface SignupActivityOption {
  id: number;
  title: string;
  titleEnglish: string;
  canSignUp: boolean;
  // false: the activity can't get a game master wish
  hasGameMasters: boolean;
  schedules: SignupScheduleOption[];
}

export interface SignupScheduleOption {
  id: number;
  start: string;
  end: string;
}

export interface SignupChoice {
  scheduleId: number;
  role: RunSeatRole;
  priority: number;
}

// The edit page's "Opdater": the list replaces the participant's wishes
export interface UpdateParticipantSignupsDto {
  signups: SignupChoice[];
}

// The row dialog's "Opdater": the wish's new schedule, role and priority
export type UpdateSignupDto = SignupChoice;

// "conflict": player wishes at the same time share a priority, and the user must sort them ("Sorter efter prioritet")
export interface SignupUpdateResult {
  status: 'fine' | 'conflict';
  conflicts: ParticipantActivitySignup[];
}

// The schedules of the player wishes in the order the user sorted them
export interface SignupPrioritiesDto {
  scheduleIds: number[];
}
