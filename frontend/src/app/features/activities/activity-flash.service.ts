import { Injectable, signal } from '@angular/core';

export interface ActivityFlash {
  type: 'success' | 'error';
  // Translation key under activities.messages
  key: string;
}

// Infosys shows a success/error message on the page it redirects to after a create, update or delete.
// The message is kept here until the next activities page takes it.
@Injectable({
  providedIn: 'root',
})
export class ActivityFlashService {
  private readonly pending = signal<ActivityFlash | null>(null);

  public set(type: ActivityFlash['type'], key: string): void {
    this.pending.set({ type, key });
  }

  public take(): ActivityFlash | null {
    const flash = this.pending();
    this.pending.set(null);
    return flash;
  }
}
