import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { describe, afterEach, it, expect, vi } from 'vitest';
import { Activity } from '@shared/types';
import { ActivityFlashService } from '../activity-flash.service';
import { ActivityFormComponent, newActivityForm, toDto, toForm } from './activity-form';

const existing: Activity = {
  id: 5,
  title: 'Gammel titel',
  titleEnglish: 'Old title',
  author: '',
  type: 'Quest',
  description: '',
  descriptionEnglish: '',
  teaserDanish: 'Teaser',
  teaserEnglish: '',
  note: '',
  durationHours: 2,
  minPlayersPerTeam: 1,
  maxPlayersPerTeam: 10,
  gameMastersPerTeam: 0,
  price: 0,
  maxSignups: 0,
  minAge: null,
  maxAge: null,
  canSignUp: false,
  isHidden: false,
  isRoomExclusive: false,
  isTimeExclusive: true,
  isReplayable: false,
  playerLanguage: 'English',
  gameMasterLanguage: 'English',
  karmaType: 'Small',
  autoSignupCategory: 'None',
  wordPressId: 77,
  updatedAt: '2026-10-29T08:00:00Z',
};

describe('newActivityForm', () => {
  it('starts with the values the Infosys create form submits when untouched', () => {
    expect(newActivityForm()).toMatchObject({
      type: 'BoardGame',
      canSignUp: true,
      isHidden: true,
      isRoomExclusive: true,
      isTimeExclusive: true,
      isReplayable: true,
      playerLanguage: 'Danish',
      gameMasterLanguage: 'Danish',
      karmaType: 'None',
      autoSignupCategory: 'None',
      maxSignups: 0,
      minAge: null,
      maxAge: null,
    });
  });
});

describe('toDto', () => {
  it('sends empty number fields as 0 and empty ages as no requirement', () => {
    expect(toDto(newActivityForm())).toMatchObject({
      durationHours: 0,
      minPlayersPerTeam: 0,
      maxPlayersPerTeam: 0,
      gameMastersPerTeam: 0,
      price: 0,
      maxSignups: 0,
      wordPressId: 0,
      minAge: null,
      maxAge: null,
    });
  });

  it('does not send the teasers, which are not part of the form', () => {
    const dto = toDto(toForm(existing));
    expect(dto).not.toHaveProperty('teaserDanish');
    expect(dto).not.toHaveProperty('teaserEnglish');
  });
});

describe('ActivityFormComponent', () => {
  let httpMock: HttpTestingController;

  afterEach(() => {
    httpMock.verify();
  });

  const setup = async (id: string | null) => {
    await TestBed.configureTestingModule({
      imports: [ActivityFormComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap(id ? { id } : {}) } },
        },
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const flash = TestBed.inject(ActivityFlashService);
    const fixture = TestBed.createComponent(ActivityFormComponent);
    fixture.detectChanges();
    httpMock.match('/assets/i18n/en.json').forEach(req => req.flush({}));

    return { component: fixture.componentInstance, fixture, navigate, flash };
  };

  const loadedForm = (component: ActivityFormComponent) => {
    const form = component.form();
    if (!form) throw new Error('Form is not loaded');
    return form;
  };

  it('creates an activity and opens it with a success message', async () => {
    const { component, navigate, flash } = await setup(null);
    loadedForm(component).title = 'Nyt spil';

    component.save();

    const req = httpMock.expectOne('/api/activities');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toMatchObject({ title: 'Nyt spil', isHidden: true, type: 'BoardGame' });
    req.flush({ ...existing, id: 42 });

    expect(navigate).toHaveBeenCalledWith(['/activities', 42]);
    expect(flash.take()).toEqual({ type: 'success', key: 'created' });
  });

  it('stays on the form with an error when the create fails', async () => {
    const { component, navigate } = await setup(null);

    component.save();
    httpMock.expectOne('/api/activities').flush({ message: 'bad' }, { status: 400, statusText: 'Bad Request' });

    expect(component.errorKey()).toBe('createFailed');
    expect(component.saving()).toBe(false);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('cancelling a create goes to the list with "not updated"', async () => {
    const { component, navigate, flash } = await setup(null);

    component.cancel();

    expect(navigate).toHaveBeenCalledWith(['/activities']);
    expect(flash.take()).toEqual({ type: 'error', key: 'notUpdated' });
  });

  it('loads an existing activity and saves the changes', async () => {
    const { component, navigate, flash } = await setup('5');
    httpMock.expectOne('/api/activities/5').flush(existing);

    expect(component.form()).toMatchObject({ title: 'Gammel titel', type: 'Quest', wordPressId: 77 });
    loadedForm(component).title = 'Ny titel';
    component.save();

    const req = httpMock.expectOne('/api/activities/5');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toMatchObject({ title: 'Ny titel', type: 'Quest' });
    req.flush({ ...existing, title: 'Ny titel' });

    expect(navigate).toHaveBeenCalledWith(['/activities', 5]);
    expect(flash.take()).toEqual({ type: 'success', key: 'updated' });
  });

  it('cancelling an edit goes back to the activity with "not updated"', async () => {
    const { component, navigate, flash } = await setup('5');
    httpMock.expectOne('/api/activities/5').flush(existing);

    component.cancel();

    expect(navigate).toHaveBeenCalledWith(['/activities', 5]);
    expect(flash.take()).toEqual({ type: 'error', key: 'notUpdated' });
  });

  it('deletes after confirmation and goes to the list', async () => {
    const { component, navigate, flash } = await setup('5');
    httpMock.expectOne('/api/activities/5').flush(existing);

    component.askDelete();
    expect(component.confirmingDelete()).toBe(true);
    component.confirmDelete();

    const req = httpMock.expectOne('/api/activities/5');
    expect(req.request.method).toBe('DELETE');
    req.flush({ message: 'Activity deleted successfully' });

    expect(navigate).toHaveBeenCalledWith(['/activities']);
    expect(flash.take()).toEqual({ type: 'success', key: 'deleted' });
  });

  it('goes to the list with an error when the delete is refused', async () => {
    const { component, navigate, flash } = await setup('5');
    httpMock.expectOne('/api/activities/5').flush(existing);

    component.askDelete();
    component.confirmDelete();
    httpMock
      .expectOne('/api/activities/5')
      .flush({ message: 'Activity has schedules and cannot be deleted' }, { status: 409, statusText: 'Conflict' });

    expect(navigate).toHaveBeenCalledWith(['/activities']);
    expect(flash.take()).toEqual({ type: 'error', key: 'deleteFailed' });
  });

  it('cancelling the delete confirmation goes to the list without a message', async () => {
    const { component, navigate, flash } = await setup('5');
    httpMock.expectOne('/api/activities/5').flush(existing);

    component.askDelete();
    component.cancelDelete();

    expect(navigate).toHaveBeenCalledWith(['/activities']);
    expect(flash.take()).toBeNull();
  });

  it('shows the not-found text when the activity does not exist', async () => {
    const { component, fixture } = await setup('999');

    httpMock.expectOne('/api/activities/999').flush({}, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();
    // Nothing translated was rendered while loading, so the dictionary is only requested now
    httpMock.match('/assets/i18n/en.json').forEach(req => req.flush({}));

    expect(component.notFound()).toBe(true);
    expect(component.form()).toBeNull();
  });
});
