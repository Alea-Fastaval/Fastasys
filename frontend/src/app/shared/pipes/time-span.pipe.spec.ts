import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { describe, beforeEach, afterEach, it, expect } from 'vitest';
import { TimeSpanPipe } from './time-span.pipe';

describe('TimeSpanPipe', () => {
  let pipe: TimeSpanPipe;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    globalThis.localStorage?.removeItem('fastasys_lang');
    TestBed.configureTestingModule({
      providers: [TimeSpanPipe, provideHttpClient(), provideHttpClientTesting()],
    });

    pipe = TestBed.inject(TimeSpanPipe);
    httpMock = TestBed.inject(HttpTestingController);
    httpMock.expectOne('/assets/i18n/en.json').flush({
      common: { weekdays: { Wednesday: 'Onsdag', Thursday: 'Torsdag', Saturday: 'Lørdag', Sunday: 'Søndag' } },
    });
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('shows a run like Infosys: day and start, then end time', () => {
    expect(pipe.transform('2027-03-24T09:00:00Z', '2027-03-24T13:00:00Z')).toBe('Onsdag 10:00-14:00');
  });

  it('shows a schedule like the Infosys activity page: day on both ends', () => {
    expect(pipe.transform('2027-03-24T21:00:00Z', '2027-03-25T01:00:00Z', true)).toBe('Onsdag 22:00-Torsdag 02:00');
  });

  it('shows an activity like the Infosys participant page: day on both ends, joined by " -> "', () => {
    expect(pipe.transform('2027-03-24T09:00:00Z', '2027-03-24T13:00:00Z', true, ' -> ')).toBe(
      'Onsdag 10:00 -> Onsdag 14:00',
    );
  });

  it('shows each end in its own offset across the switch to summer time (28 March 2027)', () => {
    // 22:00 UTC is 23:00 winter time; 02:00 UTC is 04:00 summer time. The run is four hours long.
    expect(pipe.transform('2027-03-27T22:00:00Z', '2027-03-28T02:00:00Z')).toBe('Lørdag 23:00-04:00');
    expect(pipe.transform('2027-03-27T22:00:00Z', '2027-03-28T02:00:00Z', true)).toBe('Lørdag 23:00-Søndag 04:00');
  });
});
