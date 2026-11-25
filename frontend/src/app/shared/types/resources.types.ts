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
  description: string;
  size?: number;
}

export interface OrderWearDto {
  participantId: number;
  wearItemId: number;
  quantity?: number;
}
