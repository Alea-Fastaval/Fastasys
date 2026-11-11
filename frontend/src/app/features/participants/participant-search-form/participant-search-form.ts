import { Component, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatRadioModule } from '@angular/material/radio';
import { MatSelectModule } from '@angular/material/select';
import {
  BIRTH_DATE_RANGES,
  BirthDateRange,
  CriteriaSearchStep,
  OrganizerArea,
  PARTICIPANT_LANGUAGES,
  ParticipantCategory,
  ParticipantCriteria,
  ParticipantLanguage,
  SEARCH_COMBINATIONS,
  SEARCH_LOGICS,
  SearchCombination,
  SearchLogic,
} from '@shared/types';
import { Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';

// The Infosys ja/nej selects start empty, which means the field is not used
export type YesNoChoice = '' | 'ja' | 'nej';

export interface SearchForm {
  id: string;
  birthDateRange: BirthDateRange;
  birthDate: string;
  categoryId: number | null;
  firstName: string;
  lastName: string;
  nickname: string;
  email: string;
  amountPaid: string;
  address: string;
  zipCode: string;
  city: string;
  country: string;
  phoneNumber: string;
  smsSignup: YesNoChoice;
  activityLanguages: ParticipantLanguage[];
  superHero: YesNoChoice;
  desiredHeroTasks: number | null;
  setupHelpMonday: YesNoChoice;
  setupHelpTuesday: YesNoChoice;
  isAuthorOrDesigner: YesNoChoice;
  desiredActivities: number | null;
  superGameMaster: YesNoChoice;
  financialSupport: YesNoChoice;
  isNoShow: YesNoChoice;
  isAnnulled: YesNoChoice;
  youthClub: string;
  organizerAreaId: number | null;
  game: string;
  checkedInAt: string;
  notes: string;
  logic: SearchLogic;
  combination: SearchCombination;
}

// Infosys's search box: every field empty, Fødselsdato "Før", Søgelogik "And" and "Ny søgning"
export function newSearchForm(): SearchForm {
  return {
    id: '',
    birthDateRange: 'Before',
    birthDate: '',
    categoryId: null,
    firstName: '',
    lastName: '',
    nickname: '',
    email: '',
    amountPaid: '',
    address: '',
    zipCode: '',
    city: '',
    country: '',
    phoneNumber: '',
    smsSignup: '',
    activityLanguages: [],
    superHero: '',
    desiredHeroTasks: null,
    setupHelpMonday: '',
    setupHelpTuesday: '',
    isAuthorOrDesigner: '',
    desiredActivities: null,
    superGameMaster: '',
    financialSupport: '',
    isNoShow: '',
    isAnnulled: '',
    youthClub: '',
    organizerAreaId: null,
    game: '',
    checkedInAt: '',
    notes: '',
    logic: 'And',
    combination: 'New',
  };
}

const TEXT_FIELDS = [
  'id',
  'birthDate',
  'firstName',
  'lastName',
  'nickname',
  'email',
  'amountPaid',
  'address',
  'zipCode',
  'city',
  'country',
  'phoneNumber',
  'youthClub',
  'game',
  'checkedInAt',
  'notes',
] as const;
const YES_NO_FIELDS = [
  'smsSignup',
  'superHero',
  'setupHelpMonday',
  'setupHelpTuesday',
  'isAuthorOrDesigner',
  'superGameMaster',
  'financialSupport',
  'isNoShow',
  'isAnnulled',
] as const;
const NUMBER_FIELDS = ['categoryId', 'desiredHeroTasks', 'desiredActivities', 'organizerAreaId'] as const;

// Like the Infosys search box's script, only the fields that have a value are sent
export function toSearchStep(form: SearchForm): CriteriaSearchStep {
  const criteria: ParticipantCriteria = {};
  const set = criteria as Record<string, unknown>;
  for (const field of TEXT_FIELDS) if (form[field] !== '') set[field] = form[field];
  for (const field of YES_NO_FIELDS) if (form[field] !== '') set[field] = form[field] === 'ja';
  for (const field of NUMBER_FIELDS) if (form[field] !== null) set[field] = form[field];
  if (form.activityLanguages.length > 0) criteria.activityLanguages = form.activityLanguages;
  if (criteria.birthDate !== undefined) criteria.birthDateRange = form.birthDateRange;
  return { criteria, logic: form.logic, combination: form.combination };
}

// Infosys ParticipantController search box ("Søg" on the participant list). Its "Reset" button is left out: in
// Infosys it searches instead of clearing the box (reported 2026-11-11). Indgang and Mad come with those features.
@Component({
  selector: 'app-participant-search-form',
  imports: [
    FormsModule,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatInputModule,
    MatRadioModule,
    MatSelectModule,
    Card,
    TranslatePipe,
  ],
  templateUrl: './participant-search-form.html',
  styleUrl: './participant-search-form.scss',
})
export class ParticipantSearchFormComponent {
  public readonly categories = input<ParticipantCategory[]>([]);
  // Sorted by Danish name
  public readonly organizerAreas = input<OrganizerArea[]>([]);
  public readonly searched = output<CriteriaSearchStep>();

  public readonly form = signal<SearchForm>(newSearchForm());
  public readonly birthDateRanges = BIRTH_DATE_RANGES;
  public readonly languages = PARTICIPANT_LANGUAGES;
  public readonly logics = SEARCH_LOGICS;
  public readonly combinations = SEARCH_COMBINATIONS;
  // The text fields, in the order of the Infosys search box
  public readonly personalTextFields = [
    'firstName',
    'lastName',
    'nickname',
    'email',
    'amountPaid',
    'address',
    'zipCode',
    'city',
    'country',
    'phoneNumber',
  ] as const;
  public readonly otherTextFields = ['game', 'checkedInAt', 'notes'] as const;
  public readonly yesNoFields: { name: (typeof YES_NO_FIELDS)[number]; label: string }[] = [
    { name: 'smsSignup', label: 'participants.search.smsSignup' },
    { name: 'superHero', label: 'participants.search.superHero' },
    { name: 'setupHelpMonday', label: 'participants.search.setupHelpMonday' },
    { name: 'setupHelpTuesday', label: 'participants.search.setupHelpTuesday' },
    { name: 'isAuthorOrDesigner', label: 'participants.search.isAuthor' },
    { name: 'superGameMaster', label: 'participants.search.superGameMaster' },
    { name: 'financialSupport', label: 'participants.search.financialSupport' },
    { name: 'isNoShow', label: 'participants.search.isNoShow' },
    { name: 'isAnnulled', label: 'participants.search.isAnnulled' },
  ];

  public hasLanguage(language: ParticipantLanguage): boolean {
    return this.form().activityLanguages.includes(language);
  }

  public toggleLanguage(language: ParticipantLanguage, checked: boolean): void {
    const form = this.form();
    const others = form.activityLanguages.filter(l => l !== language);
    form.activityLanguages = checked ? [...others, language] : others;
  }

  public submit(): void {
    this.searched.emit(toSearchStep(this.form()));
  }
}
