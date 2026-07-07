import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { App } from './app';
import { environment } from '../environments/environment';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
      ],
    }).compileComponents();
  });

  it('creates the shell and loads users on init', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const httpMock = TestBed.inject(HttpTestingController);
    const request = httpMock.expectOne(`${environment.apiBaseUrl}/users`);
    request.flush([]);

    expect(fixture.componentInstance).toBeTruthy();
    httpMock.verify();
  });

  it('navigates to the new user dashboard after a successful registration auto-login', () => {
    const fixture = TestBed.createComponent(App);
    const dialog = fixture.debugElement.injector.get(MatDialog);
    spyOn(dialog, 'open').and.returnValue({ afterClosed: () => of('user-123') } as any);
    const navigate = spyOn(TestBed.inject(Router), 'navigate');

    fixture.componentInstance.openRegistration();

    expect(navigate).toHaveBeenCalledWith(['/dashboard', 'user-123']);
  });

  it('does not navigate when registration is cancelled (dialog closes with an empty string)', () => {
    const fixture = TestBed.createComponent(App);
    const dialog = fixture.debugElement.injector.get(MatDialog);
    // Bare `mat-dialog-close` closes with '' — the guard must not treat it as a userId.
    spyOn(dialog, 'open').and.returnValue({ afterClosed: () => of('') } as any);
    const navigate = spyOn(TestBed.inject(Router), 'navigate');

    fixture.componentInstance.openRegistration();

    expect(navigate).not.toHaveBeenCalled();
  });
});
