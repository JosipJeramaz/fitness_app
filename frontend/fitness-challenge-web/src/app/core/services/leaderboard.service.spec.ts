import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { LeaderboardEntry, Paged } from '../models/models';
import { LeaderboardService } from './leaderboard.service';

describe('LeaderboardService', () => {
  let service: LeaderboardService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [LeaderboardService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(LeaderboardService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('omits the days param in since-first-activity race mode', () => {
    service.getRace().subscribe();

    const request = httpMock.expectOne((req) => req.url === `${environment.apiBaseUrl}/leaderboard/race`);
    expect(request.request.params.has('days')).toBeFalse();
    expect(request.request.params.get('top')).toBe('5');
    request.flush({ dates: [], series: [] });
  });

  it('sends the days param for fixed race windows', () => {
    service.getRace(7).subscribe();

    const request = httpMock.expectOne((req) => req.url === `${environment.apiBaseUrl}/leaderboard/race`);
    expect(request.request.params.get('days')).toBe('7');
    request.flush({ dates: [], series: [] });
  });

  it('requests the leaderboard with the trend window', () => {
    const sample: Paged<LeaderboardEntry> = {
      items: [
        { rank: 1, userId: 'u1', firstName: 'Ada', lastName: 'Lovelace', totalPoints: 100, previousRank: 2, rankDelta: 1, trend: 'up' },
      ],
      totalCount: 1,
    };

    let received: Paged<LeaderboardEntry> | undefined;
    service.getLeaderboard(14).subscribe((page) => (received = page));

    const request = httpMock.expectOne((req) => req.url === `${environment.apiBaseUrl}/leaderboard`);
    expect(request.request.params.get('windowDays')).toBe('14');
    expect(request.request.params.has('limit')).toBeFalse();
    request.flush(sample);

    expect(received).toEqual(sample);
  });

  it('passes limit and offset as paging params', () => {
    service.getLeaderboard(7, 10, 20).subscribe();

    const request = httpMock.expectOne((req) => req.url === `${environment.apiBaseUrl}/leaderboard`);
    expect(request.request.params.get('limit')).toBe('10');
    expect(request.request.params.get('offset')).toBe('20');
    request.flush({ items: [], totalCount: 0 });
  });
});
