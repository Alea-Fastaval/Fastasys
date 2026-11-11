import { Injectable, signal } from '@angular/core';

export interface RunFlash {
  type: 'success' | 'error';
  // Translation key under runs.messages
  key: string;
}

// Infosys shows a success/error message on the page it redirects to after an update or delete of a hold.
// The message is kept here until the next runs page takes it.
@Injectable({
  providedIn: 'root',
})
export class RunFlashService {
  private readonly pending = signal<RunFlash | null>(null);

  public set(type: RunFlash['type'], key: string): void {
    this.pending.set({ type, key });
  }

  public take(): RunFlash | null {
    const flash = this.pending();
    this.pending.set(null);
    return flash;
  }
}
