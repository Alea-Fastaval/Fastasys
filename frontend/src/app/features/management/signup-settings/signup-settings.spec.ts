import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { SignupSettings } from '@shared/types';
import { SignupSettingsComponent, signupStatusKey } from './signup-settings';

const url = '/api/management/signup-settings';

const settings = (overrides: Partial<SignupSettings> = {}, status: Partial<SignupSettings['status']> = {}) =>
  ({
    isOpen: false,
    ...overrides,
    status: {
      isSignupOpen: false,
      isLateSignup: false,
      isJuniorSignupOpen: false,
      signupStart: '2027-02-01',
      signupEnd: '2027-02-28',
      juniorSignupStart: null,
      conventionStart: '2027-03-24',
      conventionEnd: '2027-03-29',
      payByDay: '2027-03-30',
      ...status,
    },
  }) satisfies SignupSettings;

describe('signupStatusKey', () => {
  it('says why the signup is open or closed', () => {
    expect(signupStatusKey(settings({ isOpen: true }, { isSignupOpen: true }))).toBe('open');
    expect(signupStatusKey(settings({ isOpen: true }, { isSignupOpen: true, isLateSignup: true }))).toBe('openLate');
    expect(signupStatusKey(settings({ isOpen: false }))).toBe('closedSwitchedOff');
    // Switched on but closed: the opening day hasn't begun, or it isn't set
    expect(signupStatusKey(settings({ isOpen: true }))).toBe('closedBeforeStart');
    expect(signupStatusKey(settings({ isOpen: true }, { signupStart: null }))).toBe('closedNoStart');
  });
});

describe('SignupSettingsComponent', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SignupSettingsComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const render = (loaded: SignupSettings | null = settings()) => {
    const fixture = TestBed.createComponent(SignupSettingsComponent);
    fixture.detectChanges();
    http.match('/assets/i18n/en.json').forEach(request => request.flush({}));
    if (loaded) {
      http.expectOne(url).flush(loaded);
      fixture.detectChanges();
    }
    return fixture;
  };

  const text = (element: HTMLElement, selector: string) => element.querySelector(selector)?.textContent?.trim();

  it('shows the switch, the status and the dates', () => {
    const fixture = render(settings({}, { juniorSignupStart: '2027-01-15T09:00:00Z' }));
    const element = fixture.nativeElement as HTMLElement;

    expect(fixture.componentInstance.isOpen).toBe(false);
    expect(text(element, '.signup-status')).toBe('signupSettings.status.closedSwitchedOff');
    expect(text(element, '.junior-status')).toBe('signupSettings.status.juniorClosed');
    expect(text(element, '.signup-end')).toBe('2027-02-28');
    // The junior opening is shown in Danish time
    expect(text(element, '.junior-start')).toBe('2027-01-15 10:00');
    expect(text(element, '.convention')).toBe('2027-03-24 – 2027-03-29');
    expect(text(element, '.pay-by-day')).toBe('2027-03-30');
  });

  it('says "not set" for missing dates', () => {
    const element = render(settings({}, { signupStart: null, juniorSignupStart: null, payByDay: null }))
      .nativeElement as HTMLElement;

    expect(text(element, '.signup-start')).toBe('signupSettings.notSet');
    expect(text(element, '.junior-start')).toBe('signupSettings.notSet');
    expect(text(element, '.pay-by-day')).toBe('signupSettings.notSet');
  });

  it('sends only a change of the switch, and shows the new status', () => {
    const fixture = render();
    const component = fixture.componentInstance;
    expect(component.canSubmit()).toBe(false);

    component.isOpen = true;
    expect(component.canSubmit()).toBe(true);
    component.submit();
    const request = http.expectOne(url);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ isOpen: true });
    expect(component.canSubmit()).toBe(false);

    request.flush(settings({ isOpen: true }, { isSignupOpen: true }));
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(component.updated()).toBe(true);
    expect(element.textContent).toContain('signupSettings.updated');
    expect(text(element, '.signup-status')).toBe('signupSettings.status.open');
    expect(component.canSubmit()).toBe(false);
  });

  it('keeps the choice after a failed update so it can be retried', () => {
    const component = render().componentInstance;
    component.isOpen = true;
    component.submit();
    http.expectOne(url).flush({}, { status: 500, statusText: 'Server error' });

    expect(component.isOpen).toBe(true);
    expect(component.error()).toBe('updateFailed');
    expect(component.updated()).toBe(false);
    expect(component.canSubmit()).toBe(true);
  });

  it('offers a retry when loading fails', () => {
    const fixture = render(null);
    http.expectOne(url).flush({}, { status: 500, statusText: 'Server error' });
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('form')).toBeNull();

    element.querySelector('button')?.click();
    http.expectOne(url).flush(settings());
    fixture.detectChanges();
    expect(fixture.componentInstance.loaded()).toBe(true);
  });

  it('shows an access error without the switch for forbidden reads', () => {
    const fixture = render(null);
    http.expectOne(url).flush({}, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.textContent).toContain('signupSettings.forbidden');
    expect(element.querySelector('form')).toBeNull();
    expect(element.querySelector('button')).toBeNull();
    expect(fixture.componentInstance.canSubmit()).toBe(false);
  });
});
