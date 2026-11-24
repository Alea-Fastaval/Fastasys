import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { describe, beforeEach, afterEach, it, expect } from 'vitest';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [AuthService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it.each([
    { roles: [], privileges: [], expected: false },
    { roles: [], privileges: ['participant_edit'], expected: false },
    { roles: [], privileges: ['activity_view'], expected: true },
    { roles: [], privileges: ['activity_edit'], expected: true },
    { roles: ['Admin'], privileges: [], expected: true },
  ])('checks planning access for $roles / $privileges', ({ roles, privileges, expected }) => {
    service.currentUser.set({ id: 1, username: 'planner', email: '', firstName: '', lastName: '', roles, privileges });
    expect(service.hasPrivilege('activity_view', 'activity_edit')).toBe(expected);
    service.currentUser.set(null);
    expect(service.hasPrivilege('activity_view', 'activity_edit')).toBe(false);
  });

  it('should store token and user on successful login', () => {
    const mockResponse = {
      token: 'fake-jwt-token',
      refreshToken: 'fake-refresh-token',
      expiresAt: '2026-08-05T00:00:00Z',
      user: {
        id: 1,
        username: 'admin',
        email: 'admin@fastaval.dk',
        firstName: 'Admin',
        lastName: 'User',
        roles: ['Admin'],
        privileges: ['all'],
      },
    };

    service.login({ username: 'admin', password: 'password' }).subscribe(res => {
      expect(res.token).toBe('fake-jwt-token');
      expect(service.getToken()).toBe('fake-jwt-token');
      expect(service.isAuthenticated()).toBe(true);
    });

    const req = httpMock.expectOne('/api/auth/login');
    expect(req.request.method).toBe('POST');
    req.flush(mockResponse);
  });

  it('knows the privileges of the user, and that Admin may do everything', () => {
    const user = {
      id: 2,
      username: 'u',
      email: '',
      firstName: '',
      lastName: '',
      roles: ['Infonaut'],
      privileges: ['participant_view'],
    };

    service.currentUser.set(null);
    expect(service.hasPrivilege('participant_view')).toBe(false);

    service.currentUser.set(user);
    expect(service.hasPrivilege('participant_view')).toBe(true);
    expect(service.hasPrivilege('participant_edit')).toBe(false);
    expect(service.hasPrivilege('participant_edit', 'participant_view')).toBe(true);

    service.currentUser.set({ ...user, roles: ['Admin'], privileges: [] });
    expect(service.hasPrivilege('participant_edit')).toBe(true);
  });
});
