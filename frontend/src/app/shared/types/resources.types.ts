import { ActivitySchedule } from './activities.types';

export interface FoodType {
  id: number;
  name: string;
  nameEnglish: string;
  price: number;
  isActive: boolean;
}

export interface WearItem {
  id: number;
  name: string;
  description: string;
  price: number;
  size: string;
  stock: number;
}

export interface Room {
  id: number;
  name: string;
  location: string;
  capacity: number;
  isSleepingRoom: boolean;
  canBook: boolean;
  description: string;
  size?: number;
  schedules: ActivitySchedule[];
}

export interface CreateRoomDto {
  name: string;
  location: string;
  capacity: number;
  isSleepingRoom: boolean;
  canBook: boolean;
  description: string;
}

export interface UpdateRoomDto {
  name: string | undefined;
  location: string | undefined;
  capacity: number | undefined;
  isSleepingRoom: boolean | undefined;
  canBook: boolean | undefined;
  description: string | undefined;
}

export interface OrderFoodDto {
  participantId: number;
  foodTypeId: number;
  date: string;
  quantity?: number;
}

export interface OrderWearDto {
  participantId: number;
  wearItemId: number;
  quantity?: number;
}
