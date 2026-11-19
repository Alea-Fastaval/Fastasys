import { Component, OnInit, signal, inject, computed } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '@shared/services';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { Participant } from '@shared/types';
import { Badge } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';

@Component({
  selector: 'app-participants',
  imports: [
    CommonModule,
    RouterLink,
    FormsModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatInputModule,
    MatFormFieldModule,
    Badge,
    TranslatePipe,
  ],
  templateUrl: './participants.html',
  styleUrl: './participants.scss',
})
export class ParticipantsComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  public readonly canViewKarma = computed(() => this.auth.hasAnyPrivilege('activities_view', 'activities_edit'));

  public readonly participants = signal<Participant[]>([]);
  public searchQuery = '';
  public displayedColumns = ['id', 'name', 'email', 'barcode', 'status', 'actions'];

  ngOnInit(): void {
    this.loadParticipants();
  }

  public loadParticipants(): void {
    const url = this.searchQuery
      ? `/api/participants?search=${encodeURIComponent(this.searchQuery)}`
      : '/api/participants';
    this.http.get<Participant[]>(url).subscribe({
      next: data => this.participants.set(data),
      error: err => console.error('Failed to load participants:', err),
    });
  }

  public checkIn(id: number): void {
    this.http.post(`/api/participants/${id}/checkin`, {}).subscribe({
      next: () => this.loadParticipants(),
      error: err => console.error('Failed to check in participant:', err),
    });
  }
}
