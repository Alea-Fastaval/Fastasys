import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { describe, beforeEach, afterEach, it, expect } from 'vitest';
import { ActivitySummary } from '@shared/types';
import { ActivitiesComponent, groupByType } from './activities';
import { ActivityFlashService } from './activity-flash.service';

describe('groupByType', () => {
  it('groups in the configured type order and keeps the API order inside each group', () => {
    const activities: ActivitySummary[] = [
      { id: 3, title: 'Alfa', titleEnglish: 'Alpha', type: 'BoardGame' },
      { id: 1, title: 'Beta', titleEnglish: 'Beta', type: 'Scenario' },
      { id: 2, title: 'Gamma', titleEnglish: 'Gamma', type: 'BoardGame' },
      { id: 4, title: 'Delta', titleEnglish: 'Delta', type: 'Quest' },
    ];

    const groups = groupByType(activities);

    expect(groups.map(g => g.type)).toEqual(['Scenario', 'Quest', 'BoardGame']);
    expect(groups[2].activities.map(a => a.id)).toEqual([3, 2]);
  });

  it('leaves out types without activities', () => {
    expect(groupByType([])).toEqual([]);
  });
});

describe('ActivitiesComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ActivitiesComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  const render = () => {
    const fixture = TestBed.createComponent(ActivitiesComponent);
    fixture.detectChanges();
    httpMock.match('/assets/i18n/en.json').forEach(req => req.flush({}));
    return fixture;
  };

  it('loads all activities on initialization', () => {
    const fixture = render();

    const req = httpMock.expectOne('/api/activities');
    expect(req.request.method).toBe('GET');
    req.flush([{ id: 7, title: 'Skjult spil', titleEnglish: 'Hidden game', type: 'Junior' }]);
    fixture.detectChanges();

    expect(fixture.componentInstance.groups()).toHaveLength(1);
    expect(fixture.nativeElement.textContent).toContain('Skjult spil / Hidden game');
    expect(fixture.nativeElement.textContent).toContain('ID 7');
  });

  it('shows the Infosys no-results text when there are no activities', () => {
    const fixture = render();

    httpMock.expectOne('/api/activities').flush([]);
    fixture.detectChanges();

    // Translations are flushed empty, so the pipe renders the key
    expect(fixture.nativeElement.textContent).toContain('activities.messages.notFound');
  });

  it('shows the message left by the previous page once', () => {
    const flash = TestBed.inject(ActivityFlashService);
    flash.set('success', 'deleted');

    const fixture = render();
    httpMock.expectOne('/api/activities').flush([]);

    expect(fixture.componentInstance.flash).toEqual({ type: 'success', key: 'deleted' });
    expect(flash.take()).toBeNull();
  });
});
