import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { ActivitySettingsComponent } from './activity-settings';

const url = '/api/management/activity-settings';
const defaults = { smallKarma: 7, mediumKarma: 0, bigKarma: 10 };

describe('ActivitySettingsComponent', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ActivitySettingsComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const render = (load = true) => {
    const fixture = TestBed.createComponent(ActivitySettingsComponent);
    fixture.detectChanges();
    http.match('/assets/i18n/en.json').forEach(request => request.flush({}));
    if (load) {
      http.expectOne(url).flush({ ...defaults });
      fixture.detectChanges();
    }
    return fixture;
  };

  it('loads the three assignment values in a compact form', async () => {
    const fixture = render();
    await fixture.whenStable();
    const inputs = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('input'));
    expect(inputs.map(input => input.name)).toEqual(['smallKarma', 'mediumKarma', 'bigKarma']);
    expect(inputs.map(input => input.value)).toEqual(['7', '0', '10']);
    expect(fixture.componentInstance.canSave()).toBe(true);
  });

  it('saves changed values and confirms the persisted response', () => {
    const fixture = render();
    const component = fixture.componentInstance;
    component.form = { smallKarma: 2, mediumKarma: 4, bigKarma: 9 };
    component.save();
    const request = http.expectOne(url);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual(component.form);
    expect(component.canSave()).toBe(false);
    request.flush(component.form);
    fixture.detectChanges();
    expect(component.saved()).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('activitySettings.saved');
  });

  it('rejects missing, fractional and out of range values before sending a request', () => {
    const component = render().componentInstance;
    for (const value of [null, -1, 1.5, 1001]) {
      component.form = { ...defaults, smallKarma: value as number };
      expect(component.canSave()).toBe(false);
      component.save();
    }
    http.expectNone(url);
    component.form = { smallKarma: 0, mediumKarma: 1000, bigKarma: 0 };
    expect(component.canSave()).toBe(true);
  });

  it('retains edits after a failed save so it can be retried', () => {
    const component = render().componentInstance;
    component.form.mediumKarma = 4;
    component.save();
    http.expectOne(url).flush({}, { status: 500, statusText: 'Server error' });
    expect(component.form.mediumKarma).toBe(4);
    expect(component.error()).toBe('saveFailed');
    expect(component.saved()).toBe(false);
    expect(component.canSave()).toBe(true);
  });

  it('offers a retry instead of an editable form when loading fails', () => {
    const fixture = render(false);
    http.expectOne(url).flush({}, { status: 500, statusText: 'Server error' });
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('form')).toBeNull();
    const button = (fixture.nativeElement as HTMLElement).querySelector('button');
    expect(button).not.toBeNull();
    button?.click();
    http.expectOne(url).flush({ ...defaults });
    fixture.detectChanges();
    expect(fixture.componentInstance.loaded()).toBe(true);
  });

  it('shows an access error without editable settings for forbidden reads', () => {
    const fixture = render(false);
    http.expectOne(url).flush({}, { status: 403, statusText: 'Forbidden' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('activitySettings.forbidden');
    expect((fixture.nativeElement as HTMLElement).querySelector('form')).toBeNull();
    expect(fixture.componentInstance.canSave()).toBe(false);
  });
});
