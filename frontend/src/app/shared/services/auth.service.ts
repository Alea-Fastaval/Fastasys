import { Injectable, signal, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { AuthUser as UserInfo, AuthResponse } from '@shared/types';

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private readonly TOKEN_KEY = 'fastasys_auth_token';
  private readonly USER_KEY = 'fastasys_auth_user';

  private readonly http = inject(HttpClient);

  public currentUser = signal<UserInfo | null>(this.getStoredUser());
  public isAuthenticated = signal<boolean>(!!this.getToken());

  public login(credentials: { username: string; password: string }): Observable<AuthResponse> {
    return this.http.post<AuthResponse>('/api/auth/login', credentials).pipe(
      tap(res => {
        if (typeof localStorage !== 'undefined' && typeof localStorage.setItem === 'function') {
          localStorage.setItem(this.TOKEN_KEY, res.token);
          localStorage.setItem(this.USER_KEY, JSON.stringify(res.user));
        }
        this.currentUser.set(res.user);
        this.isAuthenticated.set(true);
      }),
    );
  }

  public logout(): void {
    if (typeof localStorage !== 'undefined') {
      localStorage.removeItem(this.TOKEN_KEY);
      localStorage.removeItem(this.USER_KEY);
    }
    this.currentUser.set(null);
    this.isAuthenticated.set(false);
  }

  // Whether the user has one of the privileges. The Admin role may do everything, as in the API's policies. This
  // only decides what the page offers; the API checks every request.
  public hasPrivilege(...keys: string[]): boolean {
    const user = this.currentUser();
    if (!user) return false;
    return user.roles.includes('Admin') || keys.some(key => user.privileges.includes(key));
  }

  public getToken(): string | null {
    if (typeof localStorage === 'undefined' || typeof localStorage.getItem !== 'function') return null;
    return localStorage.getItem(this.TOKEN_KEY);
  }

  private getStoredUser(): UserInfo | null {
    if (typeof localStorage === 'undefined' || typeof localStorage.getItem !== 'function') return null;
    const raw = localStorage.getItem(this.USER_KEY);
    return raw ? (JSON.parse(raw) as UserInfo) : null;
  }
}
