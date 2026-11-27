import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { describe, it, expect } from 'vitest';
import { ParticipantHeroTask } from '@shared/types';
import { ParticipantHeroTasksComponent, heroTaskText } from './participant-hero-tasks';

// 24 March 2027 is a Wednesday; Copenhagen is UTC+1 that week until Sunday
const dishes: ParticipantHeroTask = {
  name: 'Opvask',
  participantCount: 3,
  capacity: 4,
  start: '2027-03-24T09:00:00Z',
  end: '2027-03-24T13:00:00Z',
};
const nightWatch: ParticipantHeroTask = {
  name: 'Brandvagt',
  participantCount: 2,
  capacity: 2,
  start: '2027-03-24T22:00:00Z',
  end: '2027-03-25T05:00:00Z',
};

describe('heroTaskText (a shift in the Infosys Heltestyrke, Opgaver box)', () => {
  it('shows the task and how full the shift is', () => {
    expect(heroTaskText(dishes)).toBe('Opvask (3 / 4)');
  });
});

describe('ParticipantHeroTasksComponent', () => {
  const render = async (tasks: ParticipantHeroTask[] | null) => {
    await TestBed.configureTestingModule({
      imports: [ParticipantHeroTasksComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    const fixture = TestBed.createComponent(ParticipantHeroTasksComponent);
    fixture.componentRef.setInput('tasks', tasks);
    fixture.detectChanges();
    TestBed.inject(HttpTestingController)
      .match('/assets/i18n/en.json')
      .forEach(req => req.flush({ common: { weekdays: { Wednesday: 'Onsdag', Thursday: 'Torsdag' } } }));
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };

  it('says that the hero force is not ported yet while there is no data', async () => {
    const element = await render(null);

    expect(element.querySelector('.not-ported')?.textContent).toContain('participants.heroForceNotPorted');
    expect(element.querySelector('.hero-table')).toBeNull();
  });

  it('shows one row per shift with the task in bold and the time in Copenhagen', async () => {
    const element = await render([dishes, nightWatch]);

    const rows = [...element.querySelectorAll('.hero-table tr')].map(tr =>
      [...tr.querySelectorAll('td')].map(td => td.textContent?.trim()),
    );
    expect(rows).toEqual([
      ['Opvask (3 / 4)', 'Onsdag 10:00 -> Onsdag 14:00'],
      ['Brandvagt (2 / 2)', 'Onsdag 23:00 -> Torsdag 06:00'],
    ]);
    expect(element.querySelector('.task strong')?.textContent).toBe('Opvask (3 / 4)');
    expect(element.querySelector('.not-ported')).toBeNull();
  });

  it('shows no rows for a participant without shifts, like Infosys', async () => {
    const element = await render([]);

    expect(element.querySelectorAll('.hero-table tr').length).toBe(0);
    expect(element.querySelector('.not-ported')).toBeNull();
  });
});
