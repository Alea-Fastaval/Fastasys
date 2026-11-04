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

  public systemDomains: SystemDomain[] = [
    { key: 'users', name: 'Users' },
    { key: 'boardgames', name: 'Boardgames' },
    { key: 'participants', name: 'Participants' },
    { key: 'activities', name: 'Activities' },
    { key: 'hero_force', name: 'Hero Force' },
    { key: 'food', name: 'Food' },
    { key: 'wear', name: 'Wear' },
    { key: 'rooms', name: 'Rooms' },
  ];

  // todo: get all the new cool roles to appear in the matrix,
  // by naming them consistently, and creating more SystemDomains
  // perhaps we need a third col of the matrix.

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
        return 'restaurant';
      case 'wear':
        return 'checkroom';
      case 'rooms':
        return 'meeting_room';
      default:
        return 'security';
    }
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
