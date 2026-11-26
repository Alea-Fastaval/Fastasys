import { Injectable, signal } from '@angular/core';

export interface WearFlash {
  type: 'success' | 'error';
  // Translation key under wear.messages
  key: string;
  // Values for the message, e.g. the number of orders
  params?: Record<string, string>;
}

// Infosys shows a success/error message on the page it redirects to after a create, update or delete of a wear type.
// The message is kept here until the next wear page takes it.
@Injectable({
  providedIn: 'root',
})
export class WearFlashService {
  private readonly pending = signal<WearFlash | null>(null);

  public set(type: WearFlash['type'], key: string, params?: Record<string, string>): void {
    this.pending.set({ type, key, params });
  }

  public take(): WearFlash | null {
    const flash = this.pending();
    this.pending.set(null);
    return flash;
  }
}
