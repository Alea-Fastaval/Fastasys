import { Component, inject } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { describe, beforeEach, afterEach, it, expect, vi, MockInstance } from 'vitest';
import { QueryParamsService, UnknownQueryParamError, parseOneOf } from './query-params.service';

const COLORS = ['red', 'blue'] as const;
const SIZES = ['small', 'large'] as const;

@Component({
  template: '',
  providers: [QueryParamsService],
})
class HostComponent {
  public readonly query = inject(QueryParamsService);
  public readonly color = this.query.oneOf('color', COLORS);
  public readonly size = this.query.oneOf('size', SIZES);
}

describe('parseOneOf', () => {
  it('returns a known value', () => {
    expect(parseOneOf('color', 'blue', COLORS)).toBe('blue');
  });

  it('returns null when the parameter is missing', () => {
    expect(parseOneOf('color', null, COLORS)).toBeNull();
  });

  it.each(['green', 'Blue', '', ' red'])('throws for the unknown value "%s"', value => {
    expect(() => parseOneOf('color', value, COLORS)).toThrow(UnknownQueryParamError);
    try {
      parseOneOf('color', value, COLORS);
    } catch (error) {
      expect(error).toMatchObject({ param: 'color', value, name: 'UnknownQueryParamError' });
      expect((error as Error).message).toBe(`Unknown value "${value}" for the query parameter "color"`);
    }
  });
});

describe('QueryParamsService', () => {
  let consoleError: MockInstance<typeof console.error>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([{ path: 'host', component: HostComponent }])],
    });
    consoleError = vi.spyOn(console, 'error').mockImplementation(() => undefined);
  });

  afterEach(() => {
    consoleError.mockRestore();
  });

  const open = async (url: string) => {
    const harness = await RouterTestingHarness.create();
    const host = await harness.navigateByUrl(url, HostComponent);
    await settle(harness);
    return { harness, host, router: TestBed.inject(Router) };
  };

  // Lets the effect run and the navigation it starts finish
  const settle = async (harness: RouterTestingHarness) => {
    harness.detectChanges();
    await harness.fixture.whenStable();
    harness.detectChanges();
  };

  it('reads known values, and null for missing parameters', async () => {
    const { host, router } = await open('/host?color=red');

    expect(host.color()).toBe('red');
    expect(host.size()).toBeNull();
    expect(host.query.unknownValues()).toEqual([]);
    expect(router.url).toBe('/host?color=red');
    expect(consoleError).not.toHaveBeenCalled();
  });

  it('logs an unknown value, removes it from the URL and keeps the other parameters', async () => {
    const { host, router } = await open('/host?color=green&size=large&page=2');

    expect(host.color()).toBeNull();
    expect(host.size()).toBe('large');
    expect(router.url).toBe('/host?size=large&page=2');
    expect(consoleError).toHaveBeenCalledTimes(1);
    expect(consoleError.mock.calls[0][0]).toBeInstanceOf(UnknownQueryParamError);
    expect(host.query.unknownValues()).toEqual([expect.objectContaining({ param: 'color', value: 'green' })]);
  });

  it('reports every unknown value', async () => {
    const { host, router } = await open('/host?color=green&size=huge');

    expect(router.url).toBe('/host');
    expect(host.query.unknownValues().map(error => `${error.param}=${error.value}`)).toEqual([
      'color=green',
      'size=huge',
    ]);
    expect(consoleError).toHaveBeenCalledTimes(2);
  });

  it('merges values into the URL, removes null ones and replaces the history entry', async () => {
    const { harness, host, router } = await open('/host?color=red');
    const navigate = vi.spyOn(router, 'navigate');

    host.query.update({ size: 'small' });
    await settle(harness);
    expect(router.url).toBe('/host?color=red&size=small');
    expect(host.size()).toBe('small');
    expect(navigate).toHaveBeenCalledWith(
      [],
      expect.objectContaining({ queryParamsHandling: 'merge', replaceUrl: true }),
    );

    host.query.update({ color: null });
    await settle(harness);
    expect(router.url).toBe('/host?size=small');
    expect(host.color()).toBeNull();
  });

  it('forgets the reported values when the page changes its parameters', async () => {
    const { harness, host } = await open('/host?color=green');
    expect(host.query.unknownValues().length).toBe(1);

    host.query.update({ color: 'blue' });
    await settle(harness);

    expect(host.query.unknownValues()).toEqual([]);
    expect(host.color()).toBe('blue');
  });
});
