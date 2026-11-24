// Whether the public signup is open, with the dates behind it (GET /api/signup/status). Dates are Danish calendar days
// ("2027-02-01"); juniorSignupStart is a UTC instant.
export interface SignupStatus {
  isSignupOpen: boolean;
  isLateSignup: boolean;
  isJuniorSignupOpen: boolean;
  signupStart: string | null;
  signupEnd: string | null;
  juniorSignupStart: string | null;
  conventionStart: string | null;
  conventionEnd: string | null;
  payByDay: string | null;
}

// The manual switch (isOpen) and the resulting status (GET /api/management/signup-settings)
export interface SignupSettings {
  isOpen: boolean;
  status: SignupStatus;
}

export interface UpdateSignupSettings {
  isOpen: boolean;
}
