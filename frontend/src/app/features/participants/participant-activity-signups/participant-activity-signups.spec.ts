import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { describe, afterEach, it, expect, vi } from 'vitest';
import { ParticipantActivitySignup, ParticipantSignups } from '@shared/types';
import { ParticipantActivitySignupsComponent, signupErrorKey } from './participant-activity-signups';

// 24 March 2027 is a Wednesday; Copenhagen is UTC+1 that week
const player: ParticipantActivitySignup = {
  activityId: 1,
  title: 'Zombier',
  titleEnglish: 'Zombies',
  scheduleId: 10,
  start: '2027-03-24T09:00:00Z',
  end: '2027-03-24T13:00:00Z',
  priority: 2,
  role: 'Player',
};
const gameMaster: ParticipantActivitySignup = {
  ...player,
  activityId: 2,
  title: 'Natspil',
  titleEnglish: 'Night game',
  scheduleId: 20,
  start: '2027-03-24T21:00:00Z',
  end: '2027-03-25T01:00:00Z',
  priority: 0,
  role: 'GameMaster',
};
const options: ParticipantSignups = {
  participantId: 7,
  signups: [player, gameMaster],
  activities: [
    {
      id: 1,
      title: 'Zombier',
      titleEnglish: 'Zombies',
      canSignUp: true,
      hasGameMasters: false,
      schedules: [
        { id: 10, start: '2027-03-24T09:00:00Z', end: '2027-03-24T13:00:00Z' },
        { id: 11, start: '2027-03-25T09:00:00Z', end: '2027-03-25T13:00:00Z' },
      ],
    },
  ],
};

describe('signupErrorKey', () => {
  const err = (reason?: string) => new HttpErrorResponse({ status: 409, error: reason ? { reason } : {} });

  it("maps the API's reasons to Infosys's messages", () => {
    expect(signupErrorKey(err('locked'), 'x')).toBe('activitiesLocked');
    expect(signupErrorKey(err('alreadySignedUp'), 'x')).toBe('signupAlreadySignedUp');
    expect(signupErrorKey(err('notSignedUp'), 'x')).toBe('signupNotFound');
    expect(signupErrorKey(err('noGameMasters'), 'x')).toBe('signupNoGameMasters');
    expect(signupErrorKey(err(), 'signupUpdateFailed')).toBe('signupUpdateFailed');
  });
});

describe('ParticipantActivitySignupsComponent (Infosys Aktivitetstilmeldinger)', () => {
  let httpMock: HttpTestingController;
  let fixture: ComponentFixture<ParticipantActivitySignupsComponent>;

  afterEach(() => {
    httpMock.verify();
    vi.restoreAllMocks();
  });

  const render = async (signups: ParticipantActivitySignup[], canEdit = true) => {
    await TestBed.configureTestingModule({
      imports: [ParticipantActivitySignupsComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();
    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ParticipantActivitySignupsComponent);
    fixture.componentRef.setInput('signups', signups);
    fixture.componentRef.setInput('participantId', 7);
    fixture.componentRef.setInput('canEdit', canEdit);
    const changed = vi.fn();
    fixture.componentInstance.changed.subscribe(changed);
    fixture.detectChanges();
    httpMock
      .match('/assets/i18n/en.json')
      .forEach(req => req.flush({ common: { weekdays: { Wednesday: 'Onsdag', Thursday: 'Torsdag' } } }));
    fixture.detectChanges();
    return { element: fixture.nativeElement as HTMLElement, component: fixture.componentInstance, changed };
  };
  const rows = (element: HTMLElement) =>
    [...element.querySelectorAll('tbody tr.signup')].map(tr =>
      [...tr.querySelectorAll('td')].map(td => td.textContent?.replace(/\s+/g, ' ').trim()),
    );
  // Clicks the first row and answers the options request
  const openEditor = (element: HTMLElement) => {
    element.querySelector<HTMLElement>('tbody tr.signup')?.click();
    httpMock.expectOne('/api/participants/7/signups').flush(options);
    fixture.detectChanges();
  };

  it('shows Aktivitet, Tid, Prio and S / SL for each wish', async () => {
    const { element } = await render([player, gameMaster]);

    expect(rows(element)).toEqual([
      ['Zombier / Zombies', 'Onsdag 10:00 -> Onsdag 14:00', '2', 'participants.activities.roles.Player'],
      ['Natspil / Night game', 'Onsdag 22:00 -> Torsdag 02:00', '0', 'participants.activities.roles.GameMaster'],
    ]);
    expect(element.querySelector('tbody a')?.getAttribute('href')).toBe('/activities/1');
  });

  it('shows the headers and no rows for a participant without wishes, like Infosys', async () => {
    const { element } = await render([]);

    expect(element.querySelectorAll('thead th').length).toBe(4);
    expect(rows(element)).toEqual([]);
  });

  it('links "Ret/slet" to the edit page, unless the participant is activity-locked', async () => {
    const { element } = await render([player]);
    expect(element.querySelector('a.edit-signups')?.getAttribute('href')).toBe('/participants/7/signups');

    TestBed.resetTestingModule();
    const locked = await render([player], false);
    expect(locked.element.querySelector('a.edit-signups')).toBeNull();
    // Fixed: Infosys's dialog opened even for a locked participant
    locked.element.querySelector<HTMLElement>('tbody tr.signup')?.click();
    httpMock.expectNone('/api/participants/7/signups');
    expect(locked.component.editor()).toBeNull();
  });

  it('opens the activity, not the editor, when the activity link is clicked', async () => {
    const { element, component } = await render([player]);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);

    element.querySelector<HTMLAnchorElement>('tbody a')?.click();

    expect(navigate).toHaveBeenCalled();

    httpMock.expectNone('/api/participants/7/signups');
    expect(component.editor()).toBeNull();
  });

  it("turns a clicked row into an editor with the activity's schedules and saves it", async () => {
    const { element, component, changed } = await render([player]);
    openEditor(element);

    const times = [...element.querySelectorAll('tr.editor select[name=scheduleId] option')].map(o =>
      o.textContent?.trim(),
    );
    expect(times).toEqual(['Onsdag 10:00-14:00', 'Torsdag 10:00-14:00']);
    // The activity has no game masters, so the wish can't become a game master wish
    expect(element.querySelector<HTMLOptionElement>('tr.editor option[value=GameMaster]')?.disabled).toBe(true);

    const editor = component.editor();
    if (!editor) throw new Error('No editor');
    editor.scheduleId = 11;
    editor.priority = 1;
    component.update();
    const req = httpMock.expectOne({ method: 'PUT', url: '/api/participants/7/signups/10/Player' });
    expect(req.request.body).toEqual({ scheduleId: 11, role: 'Player', priority: 1 });
    req.flush({ status: 'fine', conflicts: [] });

    expect(changed).toHaveBeenCalledTimes(1);
    expect(component.editor()).toBeNull();
  });

  it('asks to sort the wishes at the same time when they share a priority, and saves the order', async () => {
    const { element, component, changed } = await render([player]);
    openEditor(element);
    const other = { ...player, scheduleId: 30, title: 'Samtidig', priority: 1 };

    component.update();
    httpMock
      .expectOne({ method: 'PUT', url: '/api/participants/7/signups/10/Player' })
      .flush({ status: 'conflict', conflicts: [other, player] });
    fixture.detectChanges();

    expect(element.querySelector('.conflict-sort h4')?.textContent).toContain('participants.signups.sortTitle');
    expect(
      [...element.querySelectorAll('.conflict-sort li')].map(li =>
        li.textContent?.replace('drag_indicator', '').trim(),
      ),
    ).toEqual(['Samtidig', 'Zombier']);
    expect(changed).not.toHaveBeenCalled();

    component.drop({ previousIndex: 1, currentIndex: 0 } as never);
    component.savePriorities();
    const req = httpMock.expectOne({ method: 'PUT', url: '/api/participants/7/signups/priorities' });
    expect(req.request.body).toEqual({ scheduleIds: [10, 30] });
    req.flush(null, { status: 204, statusText: 'No Content' });

    expect(changed).toHaveBeenCalledTimes(1);
    expect(component.conflicts()).toBeNull();
  });

  it('deletes the wish after "Er du sikker?"', async () => {
    const { element, component, changed } = await render([gameMaster]);
    element.querySelector<HTMLElement>('tbody tr.signup')?.click();
    httpMock.expectOne('/api/participants/7/signups').flush(options);
    const confirm = vi.spyOn(window, 'confirm').mockReturnValueOnce(false).mockReturnValueOnce(true);

    component.remove();
    httpMock.expectNone({ method: 'DELETE' });
    component.remove();
    httpMock
      .expectOne({ method: 'DELETE', url: '/api/participants/7/signups/20/GameMaster' })
      .flush(null, { status: 204, statusText: 'No Content' });

    expect(confirm).toHaveBeenCalledWith('participants.signups.confirmDelete');
    expect(changed).toHaveBeenCalledTimes(1);
  });

  it('shows why a change was refused', async () => {
    const { element, component } = await render([player]);
    openEditor(element);

    component.update();
    httpMock
      .expectOne({ method: 'PUT', url: '/api/participants/7/signups/10/Player' })
      .flush({ reason: 'alreadySignedUp' }, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();

    expect(element.querySelector('.error')?.textContent).toContain('participants.messages.signupAlreadySignedUp');
  });
});
