import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService, TranslationService } from '@shared/services';
import { ActivitySchedule, ActivitySignup, AuthUser, ParticipantKarma } from '@shared/types';
import { ParticipantPlanningComponent } from './participant-planning';

const signup: ActivitySignup = {
  id: 1,
  participantId: 7,
  isCancelled: false,
  scheduleId: 40,
  activityId: 3,
  activityTitle: 'Scenario',
  startTime: '2027-03-24T09:00:00Z',
  endTime: '2027-03-24T13:00:00Z',
  role: 'Player',
  priority: 1,
  signedUpAt: '2026-11-01T12:00:00Z',
};
const report: ParticipantKarma = {
  participantId: 7,
  participantName: 'Anna Spiller',
  karma: {
    total: -2,
    signups: [{ priority: 1, count: 1, points: -12 }],
    assignments: [{ karmaType: 'Big', count: 1, points: 10 }],
    exclusions: {
      nonTimeExclusiveSignups: 1,
      unrankedPlayerSignups: 0,
      nonTimeExclusiveAssignments: 0,
      gameMasterAssignments: 0,
      unknownSignupRoles: 0,
      cancelledSignups: 0,
      cancelledAssignments: 0,
    },
  },
  excludedInputs: [
    {
      source: 'Signup',
      scheduleId: 90,
      activityId: 9,
      activityTitle: 'Drop-in',
      runId: null,
      role: 'Player',
      priority: 1,
      reason: 'NonTimeExclusive',
    },
  ],
};
const schedule: ActivitySchedule = {
  id: 41,
  activityId: 3,
  startTime: '2027-03-25T09:00:00Z',
  endTime: '2027-03-25T13:00:00Z',
  roomId: null,
  roomName: null,
  runs: [],
  isCancelled: false,
  cancelledAt: null,
};

describe('ParticipantPlanningComponent', () => {
  let http: HttpTestingController;
  let user: ReturnType<typeof signal<AuthUser | null>>;
  let params: BehaviorSubject<ReturnType<typeof convertToParamMap>>;

  beforeEach(async () => {
    const planner: AuthUser = {
      id: 1,
      username: 'planner',
      email: '',
      firstName: '',
      lastName: '',
      roles: [],
      privileges: ['activities_edit'],
    };
    params = new BehaviorSubject(convertToParamMap({ id: '7' }));
    await TestBed.configureTestingModule({
      imports: [ParticipantPlanningComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { paramMap: params } },
        { provide: TranslationService, useValue: { translations: signal({}), translate: (key: string) => key } },
      ],
    }).compileComponents();
    user = TestBed.inject(AuthService).currentUser;
    user.set(planner);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    vi.restoreAllMocks();
  });

  const flushLoad = (data: ParticipantKarma = report, signups = [signup], id = 7) => {
    http.expectOne(`/api/participants/${id}/karma`).flush(data);
    http.expectOne(`/api/participants/${id}/activity-signups`).flush(signups);
  };
  const render = (data: ParticipantKarma = report, signups = [signup]) => {
    const fixture = TestBed.createComponent(ParticipantPlanningComponent);
    fixture.detectChanges();
    flushLoad(data, signups);
    fixture.detectChanges();
    return fixture;
  };
  const options = () =>
    http.expectOne('/api/activities').flush([{ id: 3, title: 'Scenario', titleEnglish: '', type: 'Scenario' }]);

  it('shows a negative total, grouped points, exclusions and a link to the activity', () => {
    const fixture = render();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('.total')?.textContent).toContain('-2');
    expect(element.querySelector('.breakdown')?.textContent).toContain('-12');
    expect(element.textContent).toContain('karma.reasons.NonTimeExclusive');
    expect(element.querySelector('a[href="/activities/3"]')).not.toBeNull();
  });

  it('recalculates from authoritative reads without writing or accumulating points', () => {
    const fixture = render();
    const component = fixture.componentInstance;
    for (let i = 0; i < 2; i++) {
      component.refresh();
      expect(component.loading()).toBe(true);
      expect(component.message()).toBeNull();
      flushLoad();
      expect(component.report()?.karma.total).toBe(-2);
      expect(component.message()).toBe('refreshed');
    }
  });

  it('keeps editing intact when refresh is requested with an open form', () => {
    const fixture = render();
    fixture.componentInstance.edit(signup);
    options();
    http.expectOne('/api/activities/3/schedules').flush([{ ...schedule, id: 40 }]);
    fixture.componentInstance.form.priority = 3;
    fixture.componentInstance.refresh();
    http.expectNone('/api/participants/7/karma');
    expect(fixture.componentInstance.form.priority).toBe(3);
    expect(fixture.componentInstance.formOpen()).toBe(true);
  });

  it('does not present an old total as current when recalculation fails', () => {
    const fixture = render();
    fixture.componentInstance.refresh();
    const pending = http.expectOne('/api/participants/7/activity-signups');
    http.expectOne('/api/participants/7/karma').flush({}, { status: 500, statusText: 'Server error' });
    expect(pending.cancelled).toBe(true);
    fixture.detectChanges();
    expect(fixture.componentInstance.report()).toBeNull();
    expect(fixture.componentInstance.message()).toBeNull();
    expect((fixture.nativeElement as HTMLElement).querySelector('.total')).toBeNull();
  });

  it('labels retained cancelled preferences and prevents saving to a cancelled occurrence', () => {
    const cancelled = { ...signup, isCancelled: true };
    const fixture = render(
      { ...report, excludedInputs: [{ ...report.excludedInputs[0], reason: 'CancelledSchedule' }] },
      [cancelled],
    );
    expect(fixture.nativeElement.textContent).toContain('karma.cancelled');
    expect(fixture.nativeElement.textContent).toContain('karma.reasons.CancelledSchedule');
    const component = fixture.componentInstance;
    component.edit(cancelled);
    options();
    http.expectOne('/api/activities/3/schedules').flush([{ ...schedule, id: 40, isCancelled: true }]);
    expect(component.canSave()).toBe(false);
    component.save();
    http.expectNone('/api/participants/7/activity-signups/1');
  });

  it('renders zero as a number and preserves unranked legacy role text', () => {
    const fixture = render({ ...report, karma: { ...report.karma, total: 0 } }, [
      { ...signup, role: 'LegacyObserver', priority: null },
    ]);
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('.total strong')?.textContent).toBe('0');
    expect(element.querySelector('.preferences')?.textContent).toContain('LegacyObserver');
    expect(element.querySelector('.preferences')?.textContent).toContain('karma.unranked');
  });

  it('requires an explicit player priority and reloads both preferences and karma after creation', () => {
    const fixture = render();
    const component = fixture.componentInstance;
    component.create();
    options();
    component.form.activityId = 3;
    component.selectActivity();
    http.expectOne('/api/activities/3/schedules').flush([schedule]);
    component.form.scheduleId = 41;
    expect(component.form.priority).toBeNull();
    expect(component.canSave()).toBe(false);
    component.form.priority = 1;
    expect(component.canSave()).toBe(true);
    component.save();
    const request = http.expectOne('/api/participants/7/activity-signups');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ scheduleId: 41, role: 'Player', priority: 1 });
    expect(component.saving()).toBe(true);
    const created = { ...signup, id: 2, scheduleId: 41 };
    request.flush(created);
    expect(component.loading()).toBe(true);
    flushLoad({ ...report, karma: { ...report.karma, total: -10 } }, [signup, created]);
    fixture.detectChanges();
    expect(component.formOpen()).toBe(false);
    expect(component.signups()).toHaveLength(2);
    expect((fixture.nativeElement as HTMLElement).querySelector('.total')?.textContent).toContain('-10');
  });

  it('edits an unranked preference without inventing priority or role', () => {
    const fixture = render();
    const component = fixture.componentInstance;
    component.edit({ ...signup, priority: null, role: 'LegacyObserver' });
    options();
    http.expectOne('/api/activities/3/schedules').flush([{ ...schedule, id: 40 }]);
    expect(component.form.priority).toBeNull();
    expect(component.form.role).toBeNull();
    expect(component.canSave()).toBe(false);
    component.form.role = 'Player';
    component.form.priority = 2;
    component.save();
    const request = http.expectOne('/api/participants/7/activity-signups/1');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ scheduleId: 40, role: 'Player', priority: 2 });
    request.flush({ ...signup, priority: 2 });
    flushLoad({ ...report, karma: { ...report.karma, total: 9 } }, [{ ...signup, priority: 2 }]);
    expect(component.report()?.karma.total).toBe(9);
  });

  it('keeps the form and shows a conflict when a duplicate is rejected', () => {
    const fixture = render();
    const component = fixture.componentInstance;
    component.edit(signup);
    options();
    http.expectOne('/api/activities/3/schedules').flush([{ ...schedule, id: 40 }]);
    component.save();
    http.expectOne('/api/participants/7/activity-signups/1').flush({}, { status: 409, statusText: 'Conflict' });
    expect(component.formOpen()).toBe(true);
    expect(component.saveError()).toBe('conflict');
    expect(component.report()?.karma.total).toBe(-2);
    expect(component.saving()).toBe(false);
  });

  it('cancels obsolete schedule requests when the selected activity changes', () => {
    const fixture = render();
    const component = fixture.componentInstance;
    component.edit(signup);
    options();
    const previous = http.expectOne('/api/activities/3/schedules');
    component.form.activityId = 5;
    component.selectActivity();
    expect(previous.cancelled).toBe(true);
    expect(component.form.scheduleId).toBeNull();
    http.expectOne('/api/activities/5/schedules').flush([]);
    expect(component.canSave()).toBe(false);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('karma.noSchedules');
  });

  it('uses priority zero for a newly selected GM role and clears it when switching back to player', () => {
    const fixture = render();
    const component = fixture.componentInstance;
    component.create();
    options();
    component.form.role = 'GameMaster';
    component.selectRole();
    expect(component.form.priority).toBe(0);
    component.form.role = 'Player';
    component.selectRole();
    expect(component.form.priority).toBeNull();
  });

  it('removes only the preference and fetches current karma again', () => {
    vi.spyOn(globalThis, 'confirm').mockReturnValue(true);
    const fixture = render();
    const component = fixture.componentInstance;
    component.remove(signup);
    const request = http.expectOne('/api/participants/7/activity-signups/1');
    expect(request.request.method).toBe('DELETE');
    request.flush(null);
    flushLoad({ ...report, karma: { ...report.karma, total: 10, signups: [] } }, []);
    expect(component.signups()).toEqual([]);
    expect(component.report()?.karma.total).toBe(10);
  });

  it('does not send a delete when removal is declined', () => {
    vi.spyOn(globalThis, 'confirm').mockReturnValue(false);
    render().componentInstance.remove(signup);
    http.expectNone('/api/participants/7/activity-signups/1');
  });

  it('shows the report to viewers without exposing edit controls or sending writes', () => {
    user.update(value => value && { ...value, privileges: ['activities_view'] });
    const fixture = render();
    const component = fixture.componentInstance;
    component.create();
    component.edit(signup);
    component.save();
    component.remove(signup);
    expect(component.canEdit()).toBe(false);
    expect(component.formOpen()).toBe(false);
    expect(fixture.nativeElement.textContent).not.toContain('karma.add');
    expect(fixture.nativeElement.textContent).not.toContain('common.delete');
    component.refresh();
    flushLoad();
    expect(component.message()).toBe('refreshed');
  });

  it('allows admins to edit without an explicit activity-edit privilege', () => {
    user.update(value => value && { ...value, roles: ['Admin'], privileges: [] });
    const fixture = render();
    expect(fixture.componentInstance.canEdit()).toBe(true);
  });

  it('shows a permission error instead of a stale report after a forbidden response', () => {
    const fixture = TestBed.createComponent(ParticipantPlanningComponent);
    fixture.detectChanges();
    const pending = http.expectOne('/api/participants/7/activity-signups');
    http.expectOne('/api/participants/7/karma').flush({}, { status: 403, statusText: 'Forbidden' });
    expect(pending.cancelled).toBe(true);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('karma.messages.forbidden');
    expect(fixture.componentInstance.report()).toBeNull();
  });

  it('loads the next participant when the route is reused and clears the previous form', () => {
    const fixture = render();
    fixture.componentInstance.edit(signup);
    options();
    const pending = http.expectOne('/api/activities/3/schedules');
    params.next(convertToParamMap({ id: '8' }));
    expect(pending.cancelled).toBe(true);
    expect(fixture.componentInstance.report()).toBeNull();
    expect(fixture.componentInstance.formOpen()).toBe(false);
    flushLoad({ ...report, participantId: 8, participantName: 'Next participant' }, [], 8);
    expect(fixture.componentInstance.report()?.participantId).toBe(8);
  });
});
