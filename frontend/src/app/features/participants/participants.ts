import { Component, OnInit, signal, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ParticipantSummary } from '@shared/types';
import { TranslatePipe } from '@shared/pipes';
import { ParticipantFlashService } from './participant-flash.service';

// Infosys deltager/visalle ("Alle deltagere"): the id and name of every participant, linking to the participant.
// The extra columns and the searches come later in the port.
@Component({
  selector: 'app-participants',
  imports: [RouterLink, MatButtonModule, MatIconModule, TranslatePipe],
  templateUrl: './participants.html',
  styleUrl: './participants.scss',
})
export class ParticipantsComponent implements OnInit {
  private readonly http = inject(HttpClient);

  public readonly flash = inject(ParticipantFlashService).take();
  public readonly participants = signal<ParticipantSummary[] | null>(null);

  public ngOnInit(): void {
    this.http.get<ParticipantSummary[]>('/api/participants').subscribe({
      next: data => this.participants.set(data),
      error: err => console.error('Failed to load participants:', err),
    });
  }
}
