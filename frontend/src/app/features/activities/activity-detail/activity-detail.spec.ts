import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { describe, beforeEach, afterEach, it, expect } from 'vitest';
import { Activity } from '@shared/types';
import { ActivityDetailComponent } from './activity-detail';

const activity: Activity = {
  id: 5,
  title: 'Testscenarie',
  titleEnglish: 'Test scenario',
  author: 'Test Forfatter',
  type: 'Scenario',
  description: 'Linje 1\nLinje 2',
  descriptionEnglish: '',
  teaserDanish: '',
  teaserEnglish: '',
  note: '',
  durationHours: 4.5,
  minPlayersPerTeam: 3,
  maxPlayersPerTeam: 5,
  gameMastersPerTeam: 1,
  price: 30,
  maxSignups: 0,
  minAge: 13,
  maxAge: null,
  canSignUp: true,
  isHidden: false,
  isRoomExclusive: true,
  isTimeExclusive: true,
  isReplayable: false,
  playerLanguage: 'Danish',
  gameMasterLanguage: 'DanishAndEnglish',
  karmaType: 'Big',
  autoSignupCategory: 'Author',
  wordPressId: 0,
  updatedAt: '2026-10-29T08:00:00Z',
};

describe('ActivityDetailComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ActivityDetailComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '5' }) } } },
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  const render = () => {
    const fixture = TestBed.createComponent(ActivityDetailComponent);
    fixture.detectChanges();
    httpMock.match('/assets/i18n/en.json').forEach(req => req.flush({}));
    return fixture;
  };

  it('loads and shows the activity', () => {
    const fixture = render();

    httpMock.expectOne('/api/activities/5').flush(activity);
    fixture.detectChanges();

    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('Testscenarie');
    expect(text).toContain('activities.types.Scenario');
    expect(text).toContain('activities.karmaTypes.Big');
    expect(text).toContain('activities.autoSignupCategories.Author');
  });

  it('shows the Infosys not-found text for an unknown id', () => {
    const fixture = render();

    httpMock
      .expectOne('/api/activities/5')
      .flush({ message: 'Activity not found' }, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();

    expect(fixture.componentInstance.notFound()).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('activities.messages.notFound');
  });
});
