/// <reference types="vitest/jsdom" />

import { getTestBed } from '@angular/core/testing';
import { BrowserTestingModule, platformBrowserTesting } from '@angular/platform-browser/testing';
import { beforeEach } from 'vitest';

// Use browser storage from jsdom, rather than Node 26's file-backed global Web Storage API.
Object.defineProperty(globalThis, 'localStorage', {
  value: jsdom.window.localStorage,
  configurable: true,
  writable: true,
});
beforeEach(() => localStorage.clear());

getTestBed().initTestEnvironment(BrowserTestingModule, platformBrowserTesting());
