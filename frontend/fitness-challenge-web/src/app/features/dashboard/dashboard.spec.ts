import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { provideCharts, withDefaultRegisterables } from 'ng2-charts';
import { environment } from '../../../environments/environment';
import { Activity } from '../../core/models/models';
import { AuthService } from '../../core/services/auth.service';
import { Dashboard } from './dashboard';

describe('Dashboard (paging + sorting requests)', () => {
  const userId = 'user-1';
  let fixture: ComponentFixture<Dashboard>;
  let component: Dashboard;
  let httpMock: HttpTestingController;

  const activitiesUrl = `${environment.apiBaseUrl}/users/${userId}/activities`;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Dashboard],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideCharts(withDefaultRegisterables()),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(Dashboard);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('userId', userId);
    fixture.detectChanges(); // runs the load effect

    httpMock = TestBed.inject(HttpTestingController);

    // Initial load: dashboard + first activities page.
    httpMock.expectOne((r) => r.url === `${environment.apiBaseUrl}/users/${userId}/dashboard`).flush({
      userId,
      firstName: 'Ada',
      lastName: 'Lovelace',
      totalPoints: 0,
      activityCount: 0,
      pointsOverTime: [],
      sportBreakdown: [],
    });
    const first = httpMock.expectOne((r) => r.url === activitiesUrl);
    expect(first.request.params.get('offset')).toBe('0');
    first.flush({ items: [], totalCount: 6 });
  });

  afterEach(() => httpMock.verify());

  it('requests the next page with the right offset and stays on it (no reset back to page 0)', () => {
    component.onPageChange({ pageIndex: 1, pageSize: 10, length: 6 } as any);
    fixture.detectChanges(); // would re-run the load effect if paging signals were (wrongly) tracked

    const request = httpMock.expectOne((r) => r.url === activitiesUrl);
    expect(request.request.params.get('offset')).toBe('10');
    expect(request.request.params.get('limit')).toBe('10');
    request.flush({ items: [], totalCount: 6 });

    expect(component.pageIndex()).toBe(1); // still on page 2
    // httpMock.verify() in afterEach proves no extra page-0 request was fired by the effect.
  });

  it('changing the sort resets to the first page', () => {
    component.onPageChange({ pageIndex: 1, pageSize: 10, length: 6 } as any);
    httpMock.expectOne((r) => r.url === activitiesUrl).flush({ items: [], totalCount: 6 });

    component.setSort('points', 'desc');
    fixture.detectChanges();

    const request = httpMock.expectOne((r) => r.url === activitiesUrl);
    expect(request.request.params.get('sortBy')).toBe('points');
    expect(request.request.params.get('offset')).toBe('0'); // the classic trap, covered
    request.flush({ items: [], totalCount: 6 });

    expect(component.pageIndex()).toBe(0);
  });

  it('shows the actions column only on one\'s own dashboard', () => {
    const auth = TestBed.inject(AuthService);

    auth.userId.set(userId);
    expect(component.activityColumns()).toContain('actions');

    auth.userId.set('someone-else');
    expect(component.activityColumns()).not.toContain('actions');
  });

  it('deleting the sole row of a later page steps back before refetching', () => {
    const activity: Activity = {
      id: 'act-1',
      datetime: '2026-06-30T10:00:00Z',
      activityType: 'running',
      distanceKm: 5,
      durationSeconds: null,
      steps: null,
      points: 500,
    };

    // Land on page 2 holding a single row.
    component.onPageChange({ pageIndex: 1, pageSize: 10, length: 11 } as any);
    httpMock.expectOne((r) => r.url === activitiesUrl).flush({ items: [activity], totalCount: 11 });

    component.deleteActivity(activity);

    httpMock
      .expectOne((r) => r.method === 'DELETE' && r.url === `${environment.apiBaseUrl}/activities/${activity.id}`)
      .flush(null, { status: 204, statusText: 'No Content' });

    // Refetch lands on the previous page, plus the summary refresh — no full reload.
    const refetch = httpMock.expectOne((r) => r.url === activitiesUrl);
    expect(refetch.request.params.get('offset')).toBe('0');
    refetch.flush({ items: [], totalCount: 10 });
    httpMock.expectOne((r) => r.url === `${environment.apiBaseUrl}/users/${userId}/dashboard`).flush({
      userId,
      firstName: 'Ada',
      lastName: 'Lovelace',
      totalPoints: 0,
      activityCount: 0,
      pointsOverTime: [],
      sportBreakdown: [],
    });

    expect(component.pageIndex()).toBe(0);
  });
});
