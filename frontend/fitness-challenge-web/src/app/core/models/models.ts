// Wire types shared across the app. These mirror the backend DTOs (camelCase JSON).

/** Wire sport values (Daily Steps is expressed by omitting `sport`). */
export type Sport = 'running' | 'walking' | 'cycling' | 'gym' | 'swimming';

/** Normalized activity type as returned by the API. */
export type ActivityType = Sport | 'dailySteps';

export type Trend = 'up' | 'down' | 'same' | 'new';

export interface User {
  id: string;
  firstName: string;
  lastName: string;
}

/** Registration payload — `userName` and `password` are optional but must go together (they enable login). */
export interface RegisterUserRequest {
  firstName: string;
  lastName: string;
  userName?: string;
  password?: string;
}

export interface IngestActivityRequest {
  userId: string;
  datetime: string;
  sport?: Sport;
  steps?: number;
  distance?: number;
  duration?: string;
}

export interface ActivityIngestResponse {
  id: string;
  userId: string;
  activityType: ActivityType;
  points: number;
  datetime: string;
}

export interface Activity {
  id: string;
  datetime: string;
  activityType: ActivityType;
  distanceKm: number | null;
  durationSeconds: number | null;
  steps: number | null;
  points: number;
}

/** Paged list envelope: current page plus the pre-paging total (drives "X–Y of Z" controls). */
export interface Paged<T> {
  items: T[];
  totalCount: number;
}

export interface LeaderboardEntry {
  rank: number;
  userId: string;
  firstName: string;
  lastName: string;
  totalPoints: number;
  previousRank: number | null;
  rankDelta: number | null;
  trend: Trend;
}

/**
 * One user's line in the cumulative points race, aligned index-for-index with `RaceChartData.dates`.
 * Buckets before the user's first-ever activity are null — the line starts mid-air on their first
 * scoring day instead of a misleading zero run-up.
 */
export interface RaceSeries {
  userId: string;
  firstName: string;
  lastName: string;
  cumulativePoints: (number | null)[];
}

export interface RaceChartData {
  dates: string[];
  series: RaceSeries[];
}

export interface PointsOverTimePoint {
  date: string;
  points: number;
  activityCount: number;
}

export interface SportBreakdownItem {
  activityType: ActivityType;
  points: number;
  activityCount: number;
}

export interface DashboardData {
  userId: string;
  firstName: string;
  lastName: string;
  totalPoints: number;
  activityCount: number;
  pointsOverTime: PointsOverTimePoint[];
  sportBreakdown: SportBreakdownItem[];
}

/** All selectable options in the activity-entry form (adds the sport-less Daily Steps). */
export type ActivityKind = Sport | 'dailySteps';

export interface ActivityKindOption {
  value: ActivityKind;
  label: string;
  metric: 'distance' | 'duration' | 'steps';
  /** Smallest value worth >= 1 point, mirroring the backend: km / total seconds / step count. */
  min: number;
  /** Per-entry cap mirroring the backend (`ActivityInterpreter`): km (distance), total seconds (duration), count (steps). */
  max: number;
}

export const ACTIVITY_KINDS: ActivityKindOption[] = [
  { value: 'running', label: 'Running', metric: 'distance', min: 0.01, max: 100 },
  { value: 'walking', label: 'Walking', metric: 'distance', min: 0.02, max: 80 },
  { value: 'cycling', label: 'Cycling', metric: 'distance', min: 0.04, max: 300 },
  { value: 'gym', label: 'Gym', metric: 'duration', min: 60, max: 18 * 3600 },
  { value: 'swimming', label: 'Swimming', metric: 'duration', min: 60, max: 18 * 3600 },
  { value: 'dailySteps', label: 'Daily Steps', metric: 'steps', min: 100, max: 100_000 },
];

export const ACTIVITY_LABELS: Record<ActivityType, string> = {
  running: 'Running',
  walking: 'Walking',
  cycling: 'Cycling',
  gym: 'Gym',
  swimming: 'Swimming',
  dailySteps: 'Daily Steps',
};
