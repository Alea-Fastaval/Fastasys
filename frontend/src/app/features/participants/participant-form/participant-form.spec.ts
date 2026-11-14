import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { describe, afterEach, it, expect, vi } from 'vitest';
import { Participant, ParticipantOptions } from '@shared/types';
import { ParticipantFlashService } from '../participant-flash.service';
import { ParticipantFormComponent, newParticipantForm, toFieldsDto, toForm, toUpdateDto } from './participant-form';

const options: ParticipantOptions = {
  categories: [
    { id: 1, name: 'Deltager' },
    { id: 2, name: 'Arrangør' },
  ],
  organizerAreas: [],
  countries: [],
  games: [],
};

const existing: Participant = {
  id: 12,
  firstName: 'Testa',
  lastName: 'Testesen',
  nickname: 'Testo',
  email: 'testa@example.invalid',
  phoneNumber: '11223344',
  addressLine1: 'Testvej 1',
  addressLine2: '2. th',
  zipCode: '8000',
  city: 'Testby',
  countryCode: 'DK',
  countryName: 'Danmark',
  birthDate: null,
  pronouns: 'none',
  preferredLanguage: 'English',
  categoryId: 2,
  categoryName: 'Arrangør',
  amountPaid: 100,
  smsSignup: false,
  previousId: null,
  nextId: null,
  canEdit: true,
  price: null,
  details: {
    organizerAreaId: null,
    organizerAreaName: null,
    isAuthorOrDesigner: false,
    gameActivityId: null,
    gameActivityTitle: null,
    extraVouchers: 0,
    activityLanguages: ['English'],
    sleepingAreas: [],
    desiredHeroTasks: 1,
    desiredActivities: 4,
    superGameMaster: true,
    maxGameMasterRuns: null,
    juniorGameMaster: false,
    superRulesGuide: false,
    maxRulesGuideGames: null,
    superHero: false,
    setupHelpMonday: true,
    setupHelpTuesday: false,
    financialSupport: false,
    isNoShow: false,
    isAnnulled: false,
    isActivityLocked: false,
    signedUpAt: '2026-11-01T10:00:00Z',
    checkedInAt: null,
    youthClub: 'Klubben',
    photoConsent: false,
    adminNote: '',
    messageToParticipant: '',
    paymentNote: '',
    commentNote: null,
    heroForceNote: null,
    juniorContact: null,
    juniorComment: null,
    sleepingNote: null,
    wearOrders: null,
    tickets: null,
    meals: null,
    sleepingPlaces: null,
    activities: [],
    activitySignups: [],
  },
};

// The existing participant after "Annuller tilmelding"
const annulledParticipant = (): Participant => ({
  ...existing,
  details: existing.details ? { ...existing.details, isAnnulled: true } : null,
});

const loadedForm = (component: ParticipantFormComponent) => {
  const form = component.form();
  if (!form) throw new Error('Form is not loaded');
  return form;
};

describe('newParticipantForm', () => {
  it('starts with what the untouched Infosys create form submits', () => {
    const dto = toFieldsDto(newParticipantForm(options.categories));

    // nej in every select, da as language, the first category, empty numbers as 0 and no birth date
    expect(dto).toMatchObject({
      categoryId: 1,
      preferredLanguage: 'Danish',
      smsSignup: false,
      superHero: false,
      isNoShow: false,
      amountPaid: 0,
      desiredActivities: 0,
      desiredHeroTasks: 0,
      birthDate: null,
    });
  });

  it('sends Land, Arrangørområde, Scenarie and the languages, which Infosys ignored (fixed)', () => {
    const dto = toFieldsDto(newParticipantForm(options.categories));
    // Danmark, nothing chosen and no language ticked (the server then gives dansk)
    expect(dto).toMatchObject({
      countryCode: 'DK',
      organizerAreaId: null,
      gameActivityId: null,
      activityLanguages: [],
    });
  });
});

describe('toUpdateDto', () => {
  it('sends both address lines, the languages, Land, Arrangørområde, Scenarie and Annulleret', () => {
    expect(toUpdateDto(toForm(existing))).toMatchObject({
      addressLine1: 'Testvej 1',
      addressLine2: '2. th',
      activityLanguages: ['English'],
      isAnnulled: false,
      desiredActivities: 4,
      youthClub: 'Klubben',
      countryCode: 'DK',
      organizerAreaId: null,
      gameActivityId: null,
    });
  });
});

describe('ParticipantFormComponent', () => {
  let httpMock: HttpTestingController;

  afterEach(() => {
    httpMock.verify();
  });

  const setup = async (id: string | null, loaded: Participant = existing, given: ParticipantOptions = options) => {
    await TestBed.configureTestingModule({
      imports: [ParticipantFormComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap(id ? { id } : {}) } } },
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const flash = TestBed.inject(ParticipantFlashService);
    const fixture = TestBed.createComponent(ParticipantFormComponent);
    fixture.detectChanges();
    httpMock.match('/assets/i18n/en.json').forEach(req => req.flush({}));
    httpMock.expectOne('/api/participants/options').flush(given);
    if (id) httpMock.expectOne(`/api/participants/${id}`).flush(loaded);
    fixture.detectChanges();
    // The texts are first needed when the form is shown
    httpMock.match('/assets/i18n/en.json').forEach(req => req.flush({}));

    return { component: fixture.componentInstance, element: fixture.nativeElement as HTMLElement, navigate, flash };
  };

  it('creates a participant and opens them with a success message', async () => {
    const { component, navigate, flash } = await setup(null);
    loadedForm(component).firstName = 'Ny';

    component.submit();

    const req = httpMock.expectOne('/api/participants');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toMatchObject({ firstName: 'Ny', categoryId: 1 });
    req.flush({ ...existing, id: 3 });

    expect(navigate).toHaveBeenCalledWith(['/participants', 3]);
    expect(flash.take()).toEqual({ type: 'success', key: 'created' });
  });

  it('goes to the overview with an error when the create fails, and when it is cancelled', async () => {
    const { component, navigate, flash } = await setup(null);

    component.submit();
    httpMock.expectOne('/api/participants').flush({ message: 'bad' }, { status: 400, statusText: 'Bad Request' });
    expect(navigate).toHaveBeenCalledWith(['/participants']);
    expect(flash.take()).toEqual({ type: 'error', key: 'createFailed' });

    // Infosys "Fortryd" posts the form without "Opret deltager", which gives the same message
    component.cancel();
    expect(flash.take()).toEqual({ type: 'error', key: 'createFailed' });
  });

  it('shows the activity languages, Land, Arrangørområde and Scenarie on the create form', async () => {
    const { element } = await setup(null);
    expect(element.querySelector('.languages')).not.toBeNull();
    expect(element.querySelector('mat-select[name=countryCode]')).not.toBeNull();
    expect(element.querySelector('mat-select[name=organizerAreaId]')).not.toBeNull();
    expect(element.querySelector('mat-select[name=gameActivityId]')).not.toBeNull();
  });

  it('sorts the countries, areas and games in Danish order', async () => {
    const unsorted: ParticipantOptions = {
      ...options,
      countries: [
        { code: 'AX', nameDanish: 'Åland', nameEnglish: 'Åland Islands' },
        { code: 'DK', nameDanish: 'Danmark', nameEnglish: 'Denmark' },
      ],
      organizerAreas: [
        { id: 35, nameDanish: 'Økonomi', nameEnglish: null },
        { id: 6, nameDanish: 'Bar', nameEnglish: null },
      ],
    };
    const { component } = await setup(null, existing, unsorted);

    expect(component.countries().map(c => c.code)).toEqual(['DK', 'AX']);
    expect(component.organizerAreas().map(a => a.nameDanish)).toEqual(['Bar', 'Økonomi']);
  });

  it('updates a participant and opens them with a success message', async () => {
    const { component, element, navigate, flash } = await setup('12');

    expect(element.querySelector('.languages')).not.toBeNull();
    loadedForm(component).city = 'Andetsteds';
    component.submit();

    const req = httpMock.expectOne('/api/participants/12');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toMatchObject({
      city: 'Andetsteds',
      addressLine2: '2. th',
      activityLanguages: ['English'],
    });
    req.flush(existing);

    expect(navigate).toHaveBeenCalledWith(['/participants', 12]);
    expect(flash.take()).toEqual({ type: 'success', key: 'updated' });
  });

  it('stays on the form with an error when the update fails', async () => {
    const { component, navigate } = await setup('12');

    component.submit();
    httpMock.expectOne('/api/participants/12').flush({ message: 'bad' }, { status: 400, statusText: 'Bad Request' });

    expect(component.errorKey()).toBe('updateFailed');
    expect(navigate).not.toHaveBeenCalled();
  });

  it('annuls a participant who is not annulled', async () => {
    const { component, navigate, flash } = await setup('12');

    expect(component.storedAnnulled()).toBe(false);
    component.annul();
    const req = httpMock.expectOne('/api/participants/12/fields/isAnnulled');
    expect(req.request.body).toEqual({ value: true });
    req.flush(existing);

    expect(navigate).toHaveBeenCalledWith(['/participants', 12]);
    expect(flash.take()).toEqual({ type: 'success', key: 'annulled' });
  });

  it('asks before deleting an annulled participant and then goes to the overview', async () => {
    const annulled = annulledParticipant();
    const { component, navigate } = await setup('12', annulled);

    expect(component.storedAnnulled()).toBe(true);
    expect(component.participantName()).toBe('Testo');
    component.askDelete();
    expect(component.confirmingDelete()).toBe(true);
    component.confirmDelete();
    httpMock.expectOne('/api/participants/12').flush({ message: 'deleted' });

    expect(navigate).toHaveBeenCalledWith(['/participants']);
  });

  it('goes back to the participant with an error when the delete fails', async () => {
    const annulled = annulledParticipant();
    const { component, navigate, flash } = await setup('12', annulled);

    component.confirmDelete();
    httpMock.expectOne('/api/participants/12').flush({ message: 'no' }, { status: 409, statusText: 'Conflict' });

    expect(navigate).toHaveBeenCalledWith(['/participants', 12]);
    expect(flash.take()).toEqual({ type: 'error', key: 'deleteFailed' });
  });
});
