import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { Badge, Card } from '@shared/components';
import { TranslatePipe } from '@shared/pipes';
import { CreateRoleDto, Privilege, Role, SystemDomain } from '@shared/types';

@Component({
  selector: 'app-roles',
  imports: [
    CommonModule,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatInputModule,
    MatFormFieldModule,
    MatCheckboxModule,
    Badge,
    Card,
    TranslatePipe,
  ],
  templateUrl: './roles.html',
  styleUrl: './roles.scss',
})
export class RolesComponent implements OnInit {
  private readonly http = inject(HttpClient);

  public readonly availableRoles = signal<Role[]>([]);
  public readonly allPrivileges = signal<Privilege[]>([]);
  public readonly selectedRole = signal<Role | null>(null);
  public readonly showRoleModal = signal<boolean>(false);

  // todo: Better and more systematic names
  // todo: make the the table still work, when there are hidden elements
  // todo: more domain icons

  public systemDomains: SystemDomain[] = [
    { key: 'accounting', name: 'Accounting' },
    { key: 'activity', name: 'Activity' },
    { key: 'activity_leader_list', name: 'Activity Leader List' },
    { key: 'boardgame', name: 'Boardgame' },
    { key: 'boardgame_distribute', name: 'Distribute Boardgame' },
    { key: 'checkin_stats', name: 'Check-in Statistics' },
    { key: 'entrance', name: 'Entrance' },
    { key: 'entrance_assignment_reset', name: 'Reset Entrance Assignments' },
    { key: 'entrance_participant', name: 'Assign entrance to participant' },
    { key: 'food', name: 'Food' },
    { key: 'food_distribute', name: 'Distribute Food' },
    { key: 'food_distribute_reset', name: 'Reset Food Distributions' },
    { key: 'food_marketplace', name: 'Food Marketplace' },
    { key: 'food_participant', name: 'Assign food to participant' },
    { key: 'food_stats', name: 'Food statistics' },
    { key: 'hero_force', name: 'Hero Force' },
    { key: 'hero_force_participant', name: 'Hero Force Shifts' },
    { key: 'hero_force_participant_list', name: 'Hero Force Assignments' },
    { key: 'karma_list', name: 'Karma list' },
    { key: 'nametag_participant', name: 'Name Tag for Participant' },
    { key: 'participant', name: 'Participant' },
    { key: 'participant_list', name: 'Participant list' },
    { key: 'participant_schedule', name: 'Participant schedule' },
    { key: 'payment_participant', name: 'Participant payments' },
    { key: 'role', name: 'Role' },
    { key: 'role_user', name: 'Assign Role' },
    { key: 'room', name: 'Room' },
    { key: 'room_assignment', name: 'Assign Room' },
    { key: 'run', name: 'Run' },
    { key: 'run_participant', name: 'Assign Run' },
    { key: 'run_start', name: 'Start Run' },
    { key: 'send_participant_email', name: 'Email to Participant' },
    { key: 'send_participant_notification', name: 'Notification to Participant' },
    { key: 'signup_form', name: 'Signup form' },
    { key: 'signup_stats', name: 'Signup Statistics' },
    { key: 'sleeping_assignment', name: 'Sleeping Assignments' },
    { key: 'sleeping_participant', name: 'Participant sleeping arrangement' },
    { key: 'syslog', name: 'System log' },
    { key: 'tag_template', name: 'Name tag Template' },
    { key: 'user', name: 'User' },
    { key: 'vote_list', name: 'Game Votes' },
    { key: 'wear', name: 'Wear' },
    { key: 'wear_distribute', name: 'Distribute wear' },
    { key: 'wear_list', name: 'Full Wear List' },
    { key: 'wear_participant', name: 'Wear for Participant' },
  ];

  public formRole: CreateRoleDto = {
    name: '',
    description: '',
    privilegeIds: [],
  };

  ngOnInit(): void {
    this.loadRoles();
    this.loadPrivileges();
  }

  public loadRoles(): void {
    this.http.get<Role[]>('/api/users/roles').subscribe({
      next: data => {
        this.availableRoles.set(data);
        if (data.length > 0 && !this.selectedRole()) {
          this.selectedRole.set(data[0]);
        }
      },
      error: err => console.error('Failed to load roles:', err),
    });
  }

  public loadPrivileges(): void {
    this.http.get<Privilege[]>('/api/users/privileges').subscribe({
      next: data => this.allPrivileges.set(data),
      error: err => console.error('Failed to load privileges:', err),
    });
  }

  public selectRole(role: Role): void {
    this.selectedRole.set(role);
  }

  public getDomainIcon(domainKey: string): string {
    switch (domainKey) {
      case 'users':
        return 'manage_accounts';
      case 'boardgames':
        return 'sports_esports';
      case 'participants':
        return 'people';
      case 'activities':
        return 'casino';
      case 'hero_force':
        return 'calendar_today';
      case 'food':
      case 'food_distribute':
        return 'restaurant';
      case 'wear':
        return 'checkroom';
      case 'rooms':
        return 'meeting_room';
      default:
        return 'security';
    }
  }

  public existsPrivilege(key: string): boolean {
    return this.allPrivileges().find(value => value.key === key) != undefined;
  }

  public hasPrivilege(role: Role, privilegeKey: string): boolean {
    if (role.name === 'Admin') return true;
    return role.privileges.some(p => p.key === privilegeKey);
  }
  public togglePrivilege(role: Role, privilegeKey: string, checked: boolean): void {
    const priv = this.allPrivileges().find(p => p.key === privilegeKey);
    if (!priv) return;

    if (checked) {
      if (!role.privileges.some(p => p.id === priv.id)) {
        role.privileges.push(priv);
      }
    } else {
      role.privileges = role.privileges.filter(p => p.id !== priv.id);
    }
  }

  public saveRolePermissions(role: Role): void {
    const privilegeIds = role.privileges.map(p => p.id);
    const updateDto = {
      name: role.name,
      description: role.description,
      privilegeIds,
    };

    this.http.put<Role>(`/api/users/roles/${role.id}`, updateDto).subscribe({
      next: updated => {
        this.selectedRole.set(updated);
        this.loadRoles();
      },
      error: err => console.error('Failed to save role permissions:', err),
    });
  }

  public openCreateRoleModal(): void {
    this.formRole = { name: '', description: '', privilegeIds: [] };
    this.showRoleModal.set(true);
  }

  public closeRoleModal(): void {
    this.showRoleModal.set(false);
  }

  public saveNewRole(): void {
    this.http.post<Role>('/api/users/roles', this.formRole).subscribe({
      next: created => {
        this.closeRoleModal();
        this.loadRoles();
        this.selectedRole.set(created);
      },
      error: err => console.error('Failed to create role:', err),
    });
  }

  public deleteRole(role: Role): void {
    if (confirm(`Are you sure you want to delete role '${role.name}'?`)) {
      this.http.delete(`/api/users/roles/${role.id}`).subscribe({
        next: () => {
          this.selectedRole.set(null);
          this.loadRoles();
        },
        error: err => console.error('Failed to delete role:', err),
      });
    }
  }
}
