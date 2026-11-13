import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { describe, it, expect } from 'vitest';
import { ParticipantActivity } from '@shared/types';
import { ParticipantActivitiesComponent, activityRoomText } from './participant-activities';

// 24 March 2027 is a Wednesday; Copenhagen is UTC+1 that week until Sunday
const seat: ParticipantActivity = {
  activityId: 4,
  title: 'Andet spil',
  start: '2027-03-24T09:00:00Z',
  end: '2027-03-24T13:00:00Z',
  role: 'Player',
  runId: 400,
  runNumber: 2,
  roomName: 'Lokale A1',
  roomLocation: 'Skolen',
};
const gameMaster: ParticipantActivity = {
  ...seat,
  activityId: 5,
  title: 'Natspil',
  start: '2027-03-24T21:00:00Z',
  end: '2027-03-25T01:00:00Z',
  role: 'GameMaster',
  runId: 500,
  runNumber: 1,
  roomLocation: '',
};
const ownGame: ParticipantActivity = {
  ...seat,
  activityId: 1,
  title: 'Mit spil',
  role: 'Designer',
  runId: null,
  runNumber: null,
  roomName: null,
  roomLocation: null,
};

describe('activityRoomText (the Sted column of the Infosys Aktiviteter box)', () => {
  it('shows the room and its location, like Infosys\'s "beskrivelse, skole"', () => {
    expect(activityRoomText(seat)).toBe('Lokale A1, Skolen');
  });

  it('trims the ", " of a room without a location (Infosys keeps it; trimming was agreed)', () => {
    expect(activityRoomText(gameMaster)).toBe('Lokale A1');
  });

  it('is empty for a row without a room', () => {
    expect(activityRoomText(ownGame)).toBe('');
  });
});

describe('ParticipantActivitiesComponent', () => {
  const render = async (activities: ParticipantActivity[]) => {
    await TestBed.configureTestingModule({
      imports: [ParticipantActivitiesComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();
    const fixture = TestBed.createComponent(ParticipantActivitiesComponent);
    fixture.componentRef.setInput('activities', activities);
    fixture.detectChanges();
    TestBed.inject(HttpTestingController)
      .match('/assets/i18n/en.json')
      .forEach(req => req.flush({ common: { weekdays: { Wednesday: 'Onsdag', Thursday: 'Torsdag' } } }));
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };
  const cells = (row: Element) =>
    [...row.querySelectorAll('td')].map(td => td.textContent?.replace(/\s+/g, ' ').trim());

  it('shows one row per activity with the Infosys columns', async () => {
    const element = await render([seat, gameMaster]);

    const headers = [...element.querySelectorAll('.activity-table th')].map(th => th.textContent?.trim());
    expect(headers).toEqual([
      'participants.activities.activity',
      'participants.activities.time',
      'participants.activities.role',
      'participants.activities.run',
      'participants.activities.room',
    ]);
    const rows = [...element.querySelectorAll('.activity-table tbody tr')].map(cells);
    expect(rows).toEqual([
      ['Andet spil', 'Onsdag 10:00 -> Onsdag 14:00', 'participants.activities.roles.Player', '2', 'Lokale A1, Skolen'],
      ['Natspil', 'Onsdag 22:00 -> Torsdag 02:00', 'participants.activities.roles.GameMaster', '1', 'Lokale A1'],
    ]);
  });

  it('links the activity and the run', async () => {
    const element = await render([seat]);

    const links = [...element.querySelectorAll('.activity-table tbody a')].map(a => a.getAttribute('href'));
    expect(links).toEqual(['/activities/4', '/runs/400']);
    expect(element.querySelector('.title strong')?.textContent).toBe('Andet spil');
  });

  it('shows a designer row without a run or a room', async () => {
    const element = await render([ownGame]);

    const row = element.querySelector('.activity-table tbody tr');
    expect(row && cells(row)).toEqual([
      'Mit spil',
      'Onsdag 10:00 -> Onsdag 14:00',
      'participants.activities.roles.Designer',
      '',
      '',
    ]);
    expect(element.querySelector('.run a')).toBeNull();
  });

  it('shows no table when the participant has no activities, like Infosys', async () => {
    const element = await render([]);

    expect(element.querySelector('app-card')).not.toBeNull();
    expect(element.querySelector('.activity-table')).toBeNull();
  });
});
