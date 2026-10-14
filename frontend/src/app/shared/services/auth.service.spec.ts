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
});
