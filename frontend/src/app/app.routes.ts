import { Routes } from '@angular/router';
import { authGuard } from '@shared/guards';
import { ActivitiesComponent } from './features/activities/activities';
import { ActivityDetailComponent } from './features/activities/activity-detail/activity-detail';
import { ActivityFormComponent } from './features/activities/activity-form/activity-form';
import { ScheduleFormComponent } from './features/activities/schedule-form/schedule-form';
import { BoardgamesComponent } from './features/boardgames/boardgames';
import { CommunicationsComponent } from './features/communications/communications';
import { HeroForceComponent } from './features/hero-force/hero-force';
import { LoginComponent } from './features/login/login';
import { ParticipantsComponent } from './features/participants/participants';
import { ParticipantDetailComponent } from './features/participants/participant-detail/participant-detail';
import { ParticipantFormComponent } from './features/participants/participant-form/participant-form';
import { ParticipantSeatsComponent } from './features/participants/participant-seats/participant-seats';
import { FoodComponent } from './features/resources/food/food';
import { RoomsComponent } from './features/resources/rooms/rooms';
import { WearComponent } from './features/resources/wear/wear';
import { ShopComponent } from './features/shop/shop';
import { SignupComponent } from './features/signup/signup';
import { RunDetailComponent } from './features/runs/run-detail/run-detail';
import { RunFormComponent } from './features/runs/run-form/run-form';
import { RunsComponent } from './features/runs/runs';
import { RolesComponent } from './features/user-management/roles/roles';
import { UsersComponent } from './features/user-management/users/users';

export const routes: Routes = [
  { path: '', redirectTo: 'participants', pathMatch: 'full' },
  { path: 'login', component: LoginComponent },
  { path: 'signup', component: SignupComponent },
  { path: 'participants', component: ParticipantsComponent, canActivate: [authGuard] },
  { path: 'participants/new', component: ParticipantFormComponent, canActivate: [authGuard] },
  { path: 'participants/:id', component: ParticipantDetailComponent, canActivate: [authGuard] },
  { path: 'participants/:id/edit', component: ParticipantFormComponent, canActivate: [authGuard] },
  { path: 'participants/:id/activities', component: ParticipantSeatsComponent, canActivate: [authGuard] },
  { path: 'activities', component: ActivitiesComponent, canActivate: [authGuard] },
  { path: 'activities/new', component: ActivityFormComponent, canActivate: [authGuard] },
  { path: 'activities/:id', component: ActivityDetailComponent, canActivate: [authGuard] },
  { path: 'activities/:id/edit', component: ActivityFormComponent, canActivate: [authGuard] },
  { path: 'activities/:id/schedules/:scheduleId/edit', component: ScheduleFormComponent, canActivate: [authGuard] },
  { path: 'runs', component: RunsComponent, canActivate: [authGuard] },
  { path: 'runs/:id', component: RunDetailComponent, canActivate: [authGuard] },
  { path: 'runs/:id/edit', component: RunFormComponent, canActivate: [authGuard] },
  { path: 'hero-force', component: HeroForceComponent, canActivate: [authGuard] },
  { path: 'shop', component: ShopComponent, canActivate: [authGuard] },
  { path: 'resources/food', component: FoodComponent, canActivate: [authGuard] },
  { path: 'resources/wear', component: WearComponent, canActivate: [authGuard] },
  { path: 'resources/rooms', component: RoomsComponent, canActivate: [authGuard] },
  { path: 'resources', redirectTo: 'resources/food', pathMatch: 'full' },
  { path: 'boardgames', component: BoardgamesComponent, canActivate: [authGuard] },
  { path: 'communications', component: CommunicationsComponent, canActivate: [authGuard] },
  { path: 'user-management/users', component: UsersComponent, canActivate: [authGuard] },
  { path: 'user-management/roles', component: RolesComponent, canActivate: [authGuard] },
  { path: 'user-management', redirectTo: 'user-management/users', pathMatch: 'full' },
  { path: '**', redirectTo: 'participants' },
];
