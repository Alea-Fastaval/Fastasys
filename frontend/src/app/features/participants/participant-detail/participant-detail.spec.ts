import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { describe, afterEach, it, expect, vi } from 'vitest';
import { Participant, ParticipantDetails, ParticipantOptions, ParticipantPrice } from '@shared/types';
import { AuthService } from '@shared/services';
import { ParticipantFlashService } from '../participant-flash.service';
import { ParticipantDetailComponent, editorValue } from './participant-detail';
import { ParticipantActivitySignupsComponent } from '../participant-activity-signups/participant-activity-signups';

const details: ParticipantDetails = {
  organizerAreaId: 2,
  organizerAreaName: 'Brætspilsdesigner',
  isAuthorOrDesigner: true,
  gameActivityId: null,
  gameActivityTitle: null,
  extraVouchers: 0,
  activityLanguages: ['Danish', 'English'],
  sleepingAreas: ['Organizer'],
  desiredHeroTasks: 2,
  desiredActivities: 3,
  superGameMaster: false,
  maxGameMasterRuns: null,
  juniorGameMaster: false,
  superRulesGuide: false,
  maxRulesGuideGames: null,
  superHero: false,
  setupHelpMonday: false,
  setupHelpTuesday: true,
  financialSupport: false,
  isNoShow: false,
  isAnnulled: true,
  isActivityLocked: false,
  signedUpAt: '2026-11-01T10:00:00Z',
  checkedInAt: null,
  youthClub: '',
  photoConsent: false,
  adminNote: 'Intern note',
  messageToParticipant: '',
  paymentNote: '',
  commentNote: 'Glæder mig',
  heroForceNote: null,
  juniorContact: null,
  juniorComment: null,
  sleepingNote: null,
  wearOrders: null,
  tickets: null,
  meals: null,
  sleepingPlaces: null,
  activities: [
    {
      activityId: 4,
      title: 'Andet spil',
      start: '2027-03-24T09:00:00Z',
      end: '2027-03-24T13:00:00Z',
      role: 'Player',
      runId: 400,
      runNumber: 2,
      roomName: 'Lokale A1',
      roomLocation: 'Skolen',
    },
  ],
  activitySignups: [
    {
      activityId: 4,
      title: 'Andet spil',
      titleEnglish: 'Other game',
      scheduleId: 40,
      start: '2027-03-24T09:00:00Z',
      end: '2027-03-24T13:00:00Z',
      priority: 1,
      role: 'Player',
    },
  ],
};

const participant: Participant = {
  id: 7,
  firstName: 'Testa',
  lastName: 'Testesen',
  nickname: '',
  email: 'testa@example.invalid',
  phoneNumber: '11223344',
  addressLine1: 'Testvej 1',
  addressLine2: '',
  zipCode: '8000',
  city: 'Testby',
  countryCode: 'DK',
  countryName: 'Danmark',
  birthDate: '2001-02-03',
  pronouns: 'fsfo',
  preferredLanguage: 'Danish',
  categoryId: 1,
  categoryName: 'Deltager',
  amountPaid: 450,
  smsSignup: true,
  previousId: 3,
  nextId: 9,
  canEdit: true,
  price: null,
  details,
};

const options: ParticipantOptions = {
  categories: [
    { id: 1, name: 'Deltager' },
    { id: 2, name: 'Arrangør' },
  ],
  organizerAreas: [
    { id: 1, nameDanish: 'Scenarieforfatter', nameEnglish: null },
    { id: 2, nameDanish: 'Brætspilsdesigner', nameEnglish: null },
  ],
  countries: [
    { code: 'DK', nameDanish: 'Danmark', nameEnglish: 'Denmark' },
    { code: 'AX', nameDanish: 'Åland', nameEnglish: 'Åland Islands' },
  ],
  games: [],
};

describe('editorValue', () => {
  const editor = (value: string | number | boolean | null) => ({
    field: 'x',
    value,
    pronouns: { first: 'ns', second: 'no' },
  });

  it('sends what the API expects for each kind of field', () => {
    expect(editorValue('number', editor(''))).toBe(0);
    expect(editorValue('number', editor('12'))).toBe(12);
    expect(editorValue('optionalNumber', editor(''))).toBeNull();
    expect(editorValue('date', editor(''))).toBeNull();
    expect(editorValue('date', editor('1999-12-24'))).toBe('1999-12-24');
    expect(editorValue('pronouns', editor(null))).toBe('nsno');
    expect(editorValue('yesNo', editor(false))).toBe(false);
  });
});

describe('ParticipantDetailComponent (Infosys deltager/visdeltager)', () => {
  let httpMock: HttpTestingController;

  afterEach(() => {
    httpMock.verify();
  });

  const setup = async (loaded: Participant = participant, mayEditActivities = false) => {
    const params = new BehaviorSubject(convertToParamMap({ id: String(loaded.id) }));
    await TestBed.configureTestingModule({
      imports: [ParticipantDetailComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { paramMap: params } },
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    // Only the seat editor link asks; it needs activities_edit
    vi.spyOn(TestBed.inject(AuthService), 'hasPrivilege').mockReturnValue(mayEditActivities);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(ParticipantDetailComponent);
    fixture.detectChanges();
    httpMock.match('/assets/i18n/en.json').forEach(req => req.flush({}));
    httpMock.expectOne(`/api/participants/${loaded.id}`).flush(loaded);
    if (loaded.canEdit) httpMock.expectOne('/api/participants/options').flush(options);
    fixture.detectChanges();

    return {
      fixture,
      component: fixture.componentInstance,
      element: fixture.nativeElement as HTMLElement,
      navigate,
      params,
    };
  };

  const field = (component: ParticipantDetailComponent, name: string) => {
    const all = [
      ...component.personalFields,
      ...component.organizerFields,
      ...component.preferenceFields,
      ...component.statusFields,
      ...component.otherFields,
      ...component.noteFields,
    ];
    const def = all.find(f => f.field === name);
    if (!def) throw new Error(`No field ${name}`);
    return def;
  };

  const shown = (component: ParticipantDetailComponent) => {
    const p = component.participant();
    if (!p) throw new Error('No participant is shown');
    return p;
  };

  const openEditor = (component: ParticipantDetailComponent) => {
    const editor = component.editor();
    if (!editor) throw new Error('No field is being edited');
    return editor;
  };

  const choices = (component: ParticipantDetailComponent, name: string) => {
    const options = field(component, name).options;
    if (!options) throw new Error(`${name} has no options`);
    return options();
  };

  it('shows the whole page with the notes to editors', async () => {
    const { element } = await setup();

    expect(element.textContent).toContain('Testa');
    expect(element.textContent).toContain('03-02-2001');
    expect(element.textContent).toContain('Brætspilsdesigner');
    // Infosys shows the raw set values
    expect(element.textContent).toContain('dansk, engelsk');
    expect(element.textContent).toContain('organizer');
    // Annulleret: Ja in red
    expect(element.querySelector('.annulled')).not.toBeNull();
    // The admin note and the one signup note given; the missing signup notes are not shown
    const notes = element.querySelectorAll('.notes app-card');
    expect(notes.length).toBe(4);
    expect(element.textContent).toContain('Glæder mig');
    // The boxes are in Infosys's order and say that their features are not ported while the API sends no data
    const boxes = [...element.querySelectorAll('.info-boxes > *')].map(box => box.tagName.toLowerCase());
    expect(boxes).toEqual([
      'app-participant-entrance-food',
      'app-participant-sleeping',
      'app-participant-wear',
      'app-participant-price',
    ]);
    expect(element.querySelector('.info-boxes app-participant-entrance-food .tickets-not-ported')).not.toBeNull();
    expect(element.querySelector('.info-boxes app-participant-entrance-food .meals-not-ported')).not.toBeNull();
    expect(element.querySelector('.info-boxes app-participant-sleeping .not-ported')).not.toBeNull();
    expect(element.querySelector('.info-boxes app-participant-wear .not-ported')).not.toBeNull();
    expect(element.querySelector('.info-boxes app-participant-price .not-ported')).not.toBeNull();
    // Aktiviteter comes after the notes, as in Infosys
    expect(element.querySelector('.notes + .activity-boxes app-participant-activities')).not.toBeNull();
    expect(element.querySelector('app-participant-activities .activity-table tbody')?.textContent).toContain(
      'Andet spil',
    );
  });

  it('passes the price and the amount paid to the Pris breakdown box', async () => {
    const price: ParticipantPrice = {
      originalPrice: 900,
      entry: 400,
      food: 150,
      wear: 200,
      signedUpActivities: 90,
      assignedActivities: 30,
      other: 75,
      alea: 50,
      signupTotal: 965,
      checkInTotal: 905,
    };
    const { element } = await setup({ ...participant, price });

    // Prepaid 450 (amountPaid), so 965 - 450 is still to pay for the signup
    expect(element.querySelector('app-participant-price .signup-difference .amount')?.textContent?.trim()).toBe(
      '515,-',
    );
  });

  it('shows each note as a box, with a hint in the empty ones', async () => {
    const { element } = await setup();

    const boxes = [...element.querySelectorAll<HTMLElement>('.notes .note-box')];
    // Admin note, message to the participant, finance note and the one signup note given
    expect(boxes.length).toBe(4);
    expect(boxes[0].textContent?.trim()).toBe('Intern note');
    expect(boxes[0].querySelector('.placeholder')).toBeNull();
    expect(boxes[1].querySelector('.placeholder')?.textContent).toContain('participants.notePlaceholder');
    expect(boxes[1].getAttribute('role')).toBe('button');
  });

  it('turns a clicked note into a focused text box and saves it', async () => {
    const { component, fixture, element } = await setup();

    element.querySelectorAll<HTMLElement>('.notes .note-box')[1].click();
    fixture.detectChanges();
    const textarea = element.querySelector<HTMLTextAreaElement>('.notes textarea.note-box');
    expect(textarea).not.toBeNull();
    expect(document.activeElement).toBe(textarea);

    openEditor(component).value = 'Husk at hente nøglen';
    component.submitEdit(field(component, 'messageToParticipant'));
    const req = httpMock.expectOne('/api/participants/7/fields/messageToParticipant');
    expect(req.request.body).toEqual({ value: 'Husk at hente nøglen' });
    req.flush({ ...participant, details: { ...details, messageToParticipant: 'Husk at hente nøglen' } });
    fixture.detectChanges();

    expect(element.querySelector('.notes textarea')).toBeNull();
    expect(element.querySelector('.note.saved')?.textContent).toContain('Husk at hente nøglen');
  });

  it('shows the Aktivitetstilmeldinger box after Aktiviteter and reloads the participant when a wish changes', async () => {
    const { fixture, element } = await setup();

    const boxes = [...element.querySelectorAll('.activity-boxes > *')].map(box => box.tagName.toLowerCase());
    expect(boxes).toEqual(['app-participant-activities', 'app-participant-activity-signups']);
    expect(element.querySelector('app-participant-activity-signups tbody')?.textContent).toContain(
      'Andet spil / Other game',
    );
    expect(element.querySelector('app-participant-activity-signups a.edit-signups')).not.toBeNull();

    const box = fixture.debugElement.query(By.directive(ParticipantActivitySignupsComponent))
      .componentInstance as ParticipantActivitySignupsComponent;
    box.changed.emit();
    httpMock.expectOne('/api/participants/7').flush({ ...participant, details: { ...details, activitySignups: [] } });
    fixture.detectChanges();

    expect(element.querySelector('app-participant-activity-signups tbody tr.signup')).toBeNull();
  });

  it("doesn't let the wishes of an activity-locked participant be changed", async () => {
    const { element } = await setup({ ...participant, details: { ...details, isActivityLocked: true } });

    expect(element.querySelector('app-participant-activity-signups a.edit-signups')).toBeNull();
  });

  it('links the Aktiviteter box to the seat editor for users who may edit activities', async () => {
    const { element } = await setup(participant, true);

    expect(element.querySelector('app-participant-activities a.edit-seats')?.getAttribute('href')).toBe(
      '/participants/7/activities',
    );
  });

  it('has no seat editor link for users who may not edit activities, or when the participant is activity-locked', async () => {
    // Infosys hides "Ret/slet" when Aktivitets-låst is ja; without Activity-admin, Infonaut or admin it shows "no access"
    const withoutRight = await setup(participant, false);
    expect(withoutRight.element.querySelector('a.edit-seats')).toBeNull();

    TestBed.resetTestingModule();
    const locked = await setup({ ...participant, details: { ...details, isActivityLocked: true } }, true);
    expect(locked.element.querySelector('a.edit-seats')).toBeNull();
  });

  it('shows only the personal info to users who may only view participants', async () => {
    const { component, element } = await setup({ ...participant, canEdit: false, details: null });

    expect(component.options()).toBeNull();
    expect(element.textContent).toContain('Testa');
    expect(element.querySelector('.notes')).toBeNull();
    expect(element.querySelector('.editable')).toBeNull();
    // Infosys hides the Indgang/Mad, Soveplads and Wear boxes from its Read-only roles too, but shows them the Pris
    // breakdown
    const boxes = [...element.querySelectorAll('.info-boxes > *')].map(box => box.tagName.toLowerCase());
    expect(boxes).toEqual(['app-participant-price']);
    // and the Aktiviteter box from its Read-only role
    expect(element.querySelector('app-participant-activities')).toBeNull();
    // Clicking does nothing
    component.startEdit(field(component, 'firstName'), shown(component));
    expect(component.editor()).toBeNull();
  });

  it('saves a clicked field and shows the result', async () => {
    const { component } = await setup();
    const def = field(component, 'lastName');

    component.startEdit(def, shown(component));
    expect(component.editor()?.value).toBe('Testesen');
    openEditor(component).value = 'van der Berg';
    component.submitEdit(def);

    const req = httpMock.expectOne('/api/participants/7/fields/lastName');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ value: 'van der Berg' });
    req.flush({ ...participant, lastName: 'van der Berg' });

    expect(component.participant()?.lastName).toBe('van der Berg');
    expect(component.editor()).toBeNull();
    expect(component.saved()).toBe('lastName');
  });

  it('saves the pronoun widget as one value', async () => {
    const { component } = await setup();
    const def = field(component, 'pronouns');

    component.startEdit(def, shown(component));
    expect(component.editor()?.pronouns).toEqual({ first: 'fs', second: 'fo' });
    component.changeFirstPronoun('ask');
    component.submitEdit(def);

    httpMock.expectOne('/api/participants/7/fields/pronouns').flush({ ...participant, pronouns: 'ask' });
    expect(component.participant()?.pronouns).toBe('ask');
  });

  it('keeps the editor open with an error when the save fails', async () => {
    const { component } = await setup();
    const def = field(component, 'birthDate');

    component.startEdit(def, shown(component));
    component.submitEdit(def);
    httpMock
      .expectOne('/api/participants/7/fields/birthDate')
      .flush({ message: 'bad' }, { status: 400, statusText: 'Bad Request' });

    expect(component.errorKey()).toBe('fieldUpdateFailed');
    expect(component.failed()).toBe('birthDate');
    expect(component.editor()).not.toBeNull();
  });

  it('offers no organizer area first and sorts the countries in Danish order', async () => {
    const { component } = await setup();

    const areas = choices(component, 'organizerAreaId');
    expect(areas[0]).toMatchObject({ value: null, labelKey: 'participants.noOrganizerArea' });
    const countries = choices(component, 'countryCode').map(o => o.label);
    expect(countries).toEqual(['Danmark', 'Åland']);
  });

  it('goes to the previous and next participant with the arrow keys', async () => {
    const { component, navigate } = await setup();

    component.onKeyUp(new KeyboardEvent('keyup', { key: 'ArrowLeft' }));
    component.onKeyUp(new KeyboardEvent('keyup', { key: 'ArrowRight' }));

    expect(navigate).toHaveBeenCalledWith(['/participants', 3]);
    expect(navigate).toHaveBeenCalledWith(['/participants', 9]);
  });

  it('loads the next participant when the id in the URL changes', async () => {
    const { component, params } = await setup();

    params.next(convertToParamMap({ id: '9' }));
    httpMock.expectOne('/api/participants/9').flush({ ...participant, id: 9, firstName: 'Næste' });

    expect(component.participant()?.firstName).toBe('Næste');
  });

  it('shows that the participant was not found', async () => {
    const params = new BehaviorSubject(convertToParamMap({ id: '404' }));
    await TestBed.configureTestingModule({
      imports: [ParticipantDetailComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { paramMap: params } },
      ],
    }).compileComponents();
    httpMock = TestBed.inject(HttpTestingController);
    TestBed.inject(ParticipantFlashService);
    const fixture = TestBed.createComponent(ParticipantDetailComponent);
    fixture.detectChanges();
    httpMock.match('/assets/i18n/en.json').forEach(req => req.flush({}));
    httpMock
      .expectOne('/api/participants/404')
      .flush({ message: 'Participant not found' }, { status: 404, statusText: 'Not Found' });

    expect(fixture.componentInstance.notFound()).toBe(true);
  });
});
