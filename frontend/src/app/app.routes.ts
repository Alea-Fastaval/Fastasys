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
import { ActivitySettingsComponent } from './features/management/activity-settings/activity-settings';
import { SignupSettingsComponent } from './features/management/signup-settings/signup-settings';
import { ParticipantDetailComponent } from './features/participants/participant-detail/participant-detail';
import { ParticipantFormComponent } from './features/participants/participant-form/participant-form';
import { ParticipantSeatsComponent } from './features/participants/participant-seats/participant-seats';
import { ParticipantSignupsComponent } from './features/participants/participant-signups/participant-signups';
import { ParticipantPhotoComponent } from './features/participants/participant-photo/participant-photo';
import { IdCardTemplatesComponent } from './features/id-cards/id-card-templates/id-card-templates';
import { IdCardsPrintComponent } from './features/id-cards/id-cards-print/id-cards-print';
import { FoodTypesComponent } from './features/food/food-types/food-types';
import { FoodTypeDetailComponent } from './features/food/food-type-detail/food-type-detail';
import { FoodTypeFormComponent } from './features/food/food-type-form/food-type-form';
import { FoodStatsComponent } from './features/food/food-stats/food-stats';
import { RoomsComponent } from './features/resources/rooms/rooms';
import { WearOverviewComponent } from './features/wear/wear-overview/wear-overview';
import { WearTypesComponent } from './features/wear/wear-types/wear-types';
import { WearTypeDetailComponent } from './features/wear/wear-type-detail/wear-type-detail';
import { WearTypeFormComponent } from './features/wear/wear-type-form/wear-type-form';
import { WearMiniListComponent } from './features/wear/wear-mini-list/wear-mini-list';
import { WearAttributesComponent } from './features/wear/wear-attributes/wear-attributes';
import { WearImageUploadComponent } from './features/wear/wear-image-upload/wear-image-upload';
import { WearBreakdownComponent } from './features/wear/wear-breakdown/wear-breakdown';
import { WearOrdersComponent } from './features/wear/wear-orders/wear-orders';
import { WearHandoutComponent } from './features/wear/wear-handout/wear-handout';
import { ShopComponent } from './features/shop/shop';
import { TicketTypesComponent } from './features/tickets/ticket-types/ticket-types';
import { TicketTypeDetailComponent } from './features/tickets/ticket-type-detail/ticket-type-detail';
import { TicketTypeFormComponent } from './features/tickets/ticket-type-form/ticket-type-form';
import { TicketStatsComponent } from './features/tickets/ticket-stats/ticket-stats';
import { ParticipantEntranceFoodWearComponent } from './features/participants/participant-entrance-food-wear/participant-entrance-food-wear';
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
  { path: 'management/activity-settings', component: ActivitySettingsComponent, canActivate: [authGuard] },
  { path: 'management/signup-settings', component: SignupSettingsComponent, canActivate: [authGuard] },
  { path: 'participants/new', component: ParticipantFormComponent, canActivate: [authGuard] },
  { path: 'participants/:id', component: ParticipantDetailComponent, canActivate: [authGuard] },
  { path: 'participants/:id/edit', component: ParticipantFormComponent, canActivate: [authGuard] },
  { path: 'participants/:id/activities', component: ParticipantSeatsComponent, canActivate: [authGuard] },
  { path: 'participants/:id/signups', component: ParticipantSignupsComponent, canActivate: [authGuard] },
  { path: 'participants/:id/photo', component: ParticipantPhotoComponent, canActivate: [authGuard] },
  {
    path: 'participants/:id/entrance-food-wear',
    component: ParticipantEntranceFoodWearComponent,
    canActivate: [authGuard],
  },
  { path: 'id-card-templates', component: IdCardTemplatesComponent, canActivate: [authGuard] },
  { path: 'id-cards', component: IdCardsPrintComponent, canActivate: [authGuard] },
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
  { path: 'resources/food', component: FoodTypesComponent, canActivate: [authGuard] },
  { path: 'resources/food/new', component: FoodTypeFormComponent, canActivate: [authGuard] },
  { path: 'resources/food/stats', component: FoodStatsComponent, canActivate: [authGuard] },
  { path: 'resources/food/:id', component: FoodTypeDetailComponent, canActivate: [authGuard] },
  { path: 'resources/food/:id/edit', component: FoodTypeFormComponent, canActivate: [authGuard] },
  { path: 'resources/wear', component: WearOverviewComponent, canActivate: [authGuard] },
  { path: 'resources/wear/types', component: WearTypesComponent, canActivate: [authGuard] },
  { path: 'resources/wear/types/new', component: WearTypeFormComponent, canActivate: [authGuard] },
  { path: 'resources/wear/types/:id', component: WearTypeDetailComponent, canActivate: [authGuard] },
  { path: 'resources/wear/types/:id/edit', component: WearTypeFormComponent, canActivate: [authGuard] },
  { path: 'resources/wear/types/:id/orders', component: WearMiniListComponent, canActivate: [authGuard] },
  { path: 'resources/wear/attributes', component: WearAttributesComponent, canActivate: [authGuard] },
  { path: 'resources/wear/images/upload', component: WearImageUploadComponent, canActivate: [authGuard] },
  { path: 'resources/wear/breakdown', component: WearBreakdownComponent, canActivate: [authGuard] },
  { path: 'resources/wear/orders', component: WearOrdersComponent, canActivate: [authGuard] },
  {
    path: 'resources/wear/orders/unfilled',
    component: WearOrdersComponent,
    canActivate: [authGuard],
    data: { unfilled: true },
  },
  { path: 'resources/wear/handout', component: WearHandoutComponent, canActivate: [authGuard] },
  { path: 'resources/rooms', component: RoomsComponent, canActivate: [authGuard] },
  { path: 'resources/tickets', component: TicketTypesComponent, canActivate: [authGuard] },
  { path: 'resources/tickets/new', component: TicketTypeFormComponent, canActivate: [authGuard] },
  { path: 'resources/tickets/stats', component: TicketStatsComponent, canActivate: [authGuard] },
  { path: 'resources/tickets/:id', component: TicketTypeDetailComponent, canActivate: [authGuard] },
  { path: 'resources/tickets/:id/edit', component: TicketTypeFormComponent, canActivate: [authGuard] },
  { path: 'resources', redirectTo: 'resources/food', pathMatch: 'full' },
  { path: 'boardgames', component: BoardgamesComponent, canActivate: [authGuard] },
  { path: 'communications', component: CommunicationsComponent, canActivate: [authGuard] },
  { path: 'user-management/users', component: UsersComponent, canActivate: [authGuard] },
  { path: 'user-management/roles', component: RolesComponent, canActivate: [authGuard] },
  { path: 'user-management', redirectTo: 'user-management/users', pathMatch: 'full' },
  { path: '**', redirectTo: 'participants' },
];
