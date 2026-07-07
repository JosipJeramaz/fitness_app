import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { PointsOverTimePoint } from '../../core/models/models';
import { CalendarHeatmap } from './calendar-heatmap';

describe('CalendarHeatmap', () => {
  let fixture: ComponentFixture<CalendarHeatmap>;
  let component: CalendarHeatmap;

  // 2026-07-06 is a Monday, so the window is exactly 52 full weeks + one final one-day column.
  const endDate = '2026-07-06';

  function create(points: PointsOverTimePoint[]): void {
    fixture = TestBed.createComponent(CalendarHeatmap);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('points', points);
    fixture.componentRef.setInput('endDate', endDate);
    fixture.detectChanges();
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CalendarHeatmap],
      providers: [provideNoopAnimations()],
    }).compileComponents();
  });

  it('builds a Monday-first grid spanning the last 12 months', () => {
    create([]);
    const { weeks, monthLabels } = component.view();

    expect(weeks.length).toBe(53);
    expect(weeks[0][0].date).toBe('2025-07-07'); // Monday one year back
    expect(weeks[52][0].date).toBe(endDate); // last column starts on the end day
    expect(weeks[52][0].future).toBeFalse();
    expect(weeks[52][1].future).toBeTrue(); // rest of the final week is in the future
    expect(monthLabels.length).toBeGreaterThanOrEqual(12); // every month gets a label
  });

  it('scales cell levels relative to the best day and marks empty days as level 0', () => {
    create([
      { date: '2026-07-06', points: 100, activityCount: 1 },
      { date: '2026-07-01', points: 25, activityCount: 2 },
      { date: '2026-06-29', points: 60, activityCount: 1 },
    ]);
    const cells = component
      .view()
      .weeks.flat()
      .filter((cell) => !cell.future);
    const byDate = new Map(cells.map((cell) => [cell.date, cell]));

    expect(byDate.get('2026-07-06')!.level).toBe(4); // best day
    expect(byDate.get('2026-07-01')!.level).toBe(1); // 25% of max
    expect(byDate.get('2026-06-29')!.level).toBe(3); // 60% of max
    expect(byDate.get('2026-07-05')!.level).toBe(0); // no activity
    expect(byDate.get('2026-07-05')!.tooltip).toContain('No activity');
    expect(byDate.get('2026-07-06')!.tooltip).toContain('100 points · 1 activity');
  });

  it('computes streaks, active days and best day', () => {
    create([
      // 3-day run ending yesterday (a quiet "today" must not break the current streak).
      { date: '2026-07-05', points: 100, activityCount: 1 },
      { date: '2026-07-04', points: 50, activityCount: 1 },
      { date: '2026-07-03', points: 25, activityCount: 1 },
      // Older 4-day run — the longest.
      { date: '2026-06-10', points: 10, activityCount: 1 },
      { date: '2026-06-09', points: 10, activityCount: 1 },
      { date: '2026-06-08', points: 10, activityCount: 1 },
      { date: '2026-06-07', points: 200, activityCount: 2 },
    ]);
    const stats = component.stats();

    expect(stats.currentStreak).toBe(3);
    expect(stats.longestStreak).toBe(4);
    expect(stats.activeDays).toBe(7);
    expect(stats.bestValue).toBe('200 pts');
    expect(stats.bestLabel).toBe('Best day · Jun 7');
  });

  it('reports a zero current streak after a gap and an em dash when there is no best day', () => {
    create([{ date: '2026-07-01', points: 0, activityCount: 1 }]); // 0-point activity, 5 days ago
    const stats = component.stats();

    expect(stats.currentStreak).toBe(0);
    expect(stats.longestStreak).toBe(1);
    expect(stats.activeDays).toBe(1); // an active day even though it scored 0 points
    expect(stats.bestValue).toBe('—');
  });

  it('ignores activity outside the window when scaling', () => {
    create([
      { date: '2024-01-01', points: 100000, activityCount: 5 }, // outside the 12-month window
      { date: '2026-07-06', points: 10, activityCount: 1 },
    ]);
    const cells = component
      .view()
      .weeks.flat()
      .filter((cell) => !cell.future);

    // The in-window day is the best day of the window, so it gets the top level.
    expect(cells.find((cell) => cell.date === '2026-07-06')!.level).toBe(4);
  });
});
