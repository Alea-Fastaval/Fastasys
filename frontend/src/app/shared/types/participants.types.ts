export interface Participant {
  id: number;
  firstName: string;
  lastName: string;
  nickname: string;
  email: string;
  phoneNumber: string;
  birthDate?: string;
  address?: string;
  zipCode?: string;
  city?: string;
  country?: string;
  medicalInfo?: string;
  isCheckedIn: boolean;
  checkedInAt?: string;
  barcode: string;
  createdAt: string;
}

export interface CreateParticipantDto {
  firstName: string;
  lastName: string;
  nickname: string;
  email: string;
  phoneNumber: string;
  birthDate: string;
  address: string;
  zipCode: string;
  city: string;
  country: string;
  medicalInfo?: string;
}

export interface ParticipantScheduleItem {
  type: string;
  title: string;
  startTime: string;
  endTime: string;
  role: string;
}
