import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { ActivityEntryDialog } from './activity-entry-dialog';

function configureTestBed(): Promise<void> {
  return TestBed.configureTestingModule({
    imports: [ActivityEntryDialog],
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      provideNoopAnimations(),
      { provide: MatDialogRef, useValue: { close: jasmine.createSpy('close') } },
    ],
  }).compileComponents();
}

describe('ActivityEntryDialog (metric field switching)', () => {
  let fixture: ComponentFixture<ActivityEntryDialog>;
  let component: ActivityEntryDialog;

  beforeEach(async () => {
    await configureTestBed();
    fixture = TestBed.createComponent(ActivityEntryDialog);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('defaults to the distance metric for running', () => {
    expect(component.selectedMetric()).toBe('distance');
    expect(component.form.controls.distance.enabled).toBeTrue();
    expect(component.form.controls.duration.enabled).toBeFalse();
    expect(component.form.controls.steps.enabled).toBeFalse();
  });

  it('switches to the duration metric for a duration sport', () => {
    component.form.controls.kind.setValue('swimming');

    expect(component.selectedMetric()).toBe('duration');
    expect(component.form.controls.duration.enabled).toBeTrue();
    expect(component.form.controls.distance.enabled).toBeFalse();
  });

  it('switches to the steps metric for daily steps', () => {
    component.form.controls.kind.setValue('dailySteps');

    expect(component.selectedMetric()).toBe('steps');
    expect(component.form.controls.steps.enabled).toBeTrue();
    expect(component.form.controls.distance.enabled).toBeFalse();
    expect(component.form.controls.duration.enabled).toBeFalse();
  });

  it('closes itself when handing off to the login dialog', () => {
    // The component resolves MatDialog from its own injector (it imports MatDialogModule).
    const dialog = fixture.debugElement.injector.get(MatDialog);
    spyOn(dialog, 'open').and.returnValue({ afterClosed: () => of(undefined) } as any);
    const dialogRef = TestBed.inject(MatDialogRef);

    component.openLogin();

    expect(dialogRef.close).toHaveBeenCalled();
    expect(dialog.open).toHaveBeenCalled();
  });
});

describe('ActivityEntryDialog (per-entry caps)', () => {
  let fixture: ComponentFixture<ActivityEntryDialog>;
  let component: ActivityEntryDialog;

  beforeEach(async () => {
    await configureTestBed();
    fixture = TestBed.createComponent(ActivityEntryDialog);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('applies the per-sport distance cap', () => {
    const distance = component.form.controls.distance;
    const cases: { kind: 'running' | 'walking' | 'cycling'; max: number }[] = [
      { kind: 'running', max: 100 },
      { kind: 'walking', max: 80 },
      { kind: 'cycling', max: 300 },
    ];

    for (const { kind, max } of cases) {
      component.form.controls.kind.setValue(kind);
      expect(component.selectedMax()).toBe(max);

      distance.setValue(max);
      expect(distance.hasError('max')).toBeFalse();

      distance.setValue(max + 0.001);
      expect(distance.hasError('max')).toBeTrue();
    }
  });

  it('rejects a distance worth less than 1 point (per-sport minimum)', () => {
    const distance = component.form.controls.distance;
    const cases: { kind: 'running' | 'walking' | 'cycling'; min: number }[] = [
      { kind: 'running', min: 0.01 },
      { kind: 'walking', min: 0.02 },
      { kind: 'cycling', min: 0.04 },
    ];

    for (const { kind, min } of cases) {
      component.form.controls.kind.setValue(kind);
      expect(component.selectedMin()).toBe(min);

      distance.setValue(min); // exactly 1 point
      expect(distance.hasError('min')).toBeFalse();

      distance.setValue(min - 0.001); // scores 0
      expect(distance.hasError('min')).toBeTrue();
    }
  });

  it('requires at least one full minute of duration', () => {
    component.form.controls.kind.setValue('gym');
    const duration = component.form.controls.duration;

    duration.setValue('1:00'); // exactly 1 minute
    expect(duration.valid).toBeTrue();

    duration.setValue('0:59'); // 0 completed minutes -> 0 points
    expect(duration.hasError('minDuration')).toBeTrue();
  });

  it('requires at least 100 steps', () => {
    component.form.controls.kind.setValue('dailySteps');
    const steps = component.form.controls.steps;

    steps.setValue(100);
    expect(steps.hasError('min')).toBeFalse();

    steps.setValue(99);
    expect(steps.hasError('min')).toBeTrue();
  });

  it('caps duration at 18 hours and leaves format errors to the pattern validator', () => {
    component.form.controls.kind.setValue('swimming');
    const duration = component.form.controls.duration;

    duration.setValue('1080:00'); // exactly 18 h
    expect(duration.valid).toBeTrue();

    duration.setValue('1080:01'); // one second over
    expect(duration.hasError('maxDuration')).toBeTrue();

    duration.setValue('9999'); // bad format → pattern error only, no maxDuration
    expect(duration.hasError('pattern')).toBeTrue();
    expect(duration.hasError('maxDuration')).toBeFalse();
  });

  it('rejects a datetime more than one year in the past', () => {
    const datetime = component.form.controls.datetime;

    const thirteenMonthsAgo = new Date();
    thirteenMonthsAgo.setMonth(thirteenMonthsAgo.getMonth() - 13);
    datetime.setValue(thirteenMonthsAgo.toISOString().slice(0, 16));
    expect(datetime.hasError('minDatetime')).toBeTrue();

    const lastWeek = new Date();
    lastWeek.setDate(lastWeek.getDate() - 7);
    datetime.setValue(lastWeek.toISOString().slice(0, 16));
    expect(datetime.hasError('minDatetime')).toBeFalse();
  });

  it('rejects a datetime in the future', () => {
    const datetime = component.form.controls.datetime;

    const tomorrow = new Date();
    tomorrow.setDate(tomorrow.getDate() + 1);
    datetime.setValue(tomorrow.toISOString().slice(0, 16));
    expect(datetime.hasError('maxDatetime')).toBeTrue();

    const anHourAgo = new Date();
    anHourAgo.setHours(anHourAgo.getHours() - 1);
    datetime.setValue(anHourAgo.toISOString().slice(0, 16));
    expect(datetime.hasError('maxDatetime')).toBeFalse();
  });

  it('caps daily steps at 100000', () => {
    component.form.controls.kind.setValue('dailySteps');
    const steps = component.form.controls.steps;

    steps.setValue(100000);
    expect(steps.valid).toBeTrue();

    steps.setValue(100001);
    expect(steps.hasError('max')).toBeTrue();
  });
});
