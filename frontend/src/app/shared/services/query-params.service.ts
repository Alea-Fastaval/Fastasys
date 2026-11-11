import { Injectable, Signal, computed, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';

// A query parameter whose value is not one the page knows
export class UnknownQueryParamError extends Error {
  constructor(
    public readonly param: string,
    public readonly value: string,
  ) {
    super(`Unknown value "${value}" for the query parameter "${param}"`);
    this.name = 'UnknownQueryParamError';
  }
}

// The value of a query parameter that must be one of a fixed list. A missing parameter gives null; any other
// value, the empty string included, throws an UnknownQueryParamError.
export function parseOneOf<T extends string>(param: string, value: string | null, allowed: readonly T[]): T | null {
  if (value === null) return null;
  const known = allowed.find(candidate => candidate === value);
  if (known === undefined) throw new UnknownQueryParamError(param, value);
  return known;
}

// A comma-separated list of values from a fixed list, e.g. "email,city". A missing parameter gives null and an
// empty one an empty list; any unknown item throws an UnknownQueryParamError for the whole value.
export function parseListOf<T extends string>(param: string, value: string | null, allowed: readonly T[]): T[] | null {
  if (value === null) return null;
  if (value === '') return [];
  const items = value.split(',');
  if (items.some(item => !(allowed as readonly string[]).includes(item)))
    throw new UnknownQueryParamError(param, value);
  return [...new Set(items as T[])];
}

interface Parsed<T> {
  value: T | null;
  error: UnknownQueryParamError | null;
}

// Reads and writes the query parameters of the page it is provided in, so every page treats them the same way.
// Add it to the component's providers and call oneOf() while the component is created.
//
// An unknown value throws while it is parsed. The service catches the error, logs it, removes the parameter
// from the URL so the page keeps working, and keeps the error in unknownValues for the page to show.
@Injectable()
export class QueryParamsService {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly params = toSignal(this.route.queryParamMap, { requireSync: true });
  private readonly errors: Signal<UnknownQueryParamError | null>[] = [];
  private readonly removed = signal<UnknownQueryParamError[]>([]);

  // The unknown values removed from the URL since the page was opened or last changed its parameters
  public readonly unknownValues = this.removed.asReadonly();

  constructor() {
    // Signals can't be written and the router can't navigate while a computed runs, so the errors are handled here
    effect(() => {
      const found = this.errors.map(error => error()).filter(error => error !== null);
      if (found.length === 0) return;

      found.forEach(error => console.error(error));
      this.removed.update(list => [...list, ...found]);
      this.navigate(Object.fromEntries(found.map(error => [error.param, null])));
    });
  }

  // The parameter's value; null when it is missing or unknown
  public oneOf<T extends string>(param: string, allowed: readonly T[]): Signal<T | null> {
    return this.parsed(param, value => parseOneOf(param, value, allowed));
  }

  // The parameter's items; null when it is missing or has an unknown item
  public listOf<T extends string>(param: string, allowed: readonly T[]): Signal<T[] | null> {
    return this.parsed(param, value => parseListOf(param, value, allowed));
  }

  // Free text, which is never unknown; null when it is missing
  public text(param: string): Signal<string | null> {
    return this.parsed(param, value => value);
  }

  // The parameter read by the page's own parser, which throws an UnknownQueryParamError for a value it doesn't
  // understand; null when the parameter is missing or not understood
  public parsed<T>(param: string, parse: (value: string) => T): Signal<T | null> {
    const parsed = computed<Parsed<T>>(() => {
      const raw = this.params().get(param);
      if (raw === null) return { value: null, error: null };
      try {
        return { value: parse(raw), error: null };
      } catch (error) {
        if (error instanceof UnknownQueryParamError) return { value: null, error };
        throw error;
      }
    });

    this.errors.push(computed(() => parsed().error));
    return computed(() => parsed().value);
  }

  // Merges the values into the URL; null removes a parameter. The URL is replaced, so the browser's back button
  // leaves the page instead of undoing each change.
  public update(values: Record<string, string | null>): void {
    this.removed.set([]);
    this.navigate(values);
  }

  private navigate(values: Record<string, string | null>): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: values,
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }
}
