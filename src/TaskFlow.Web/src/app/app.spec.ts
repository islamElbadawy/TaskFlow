import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { RealtimeClient } from './core/realtime/realtime-client';
import { AppStorage, MemoryAppStorage } from './core/storage/storage';

class FakeRealtimeClient extends RealtimeClient {
  override start(): Promise<void> {
    return Promise.resolve();
  }
  override stop(): Promise<void> {
    return Promise.resolve();
  }
  override onNotification(): () => void {
    return () => undefined;
  }
  override onReconnected(): () => void {
    return () => undefined;
  }
}

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AppStorage, useValue: new MemoryAppStorage() },
        { provide: RealtimeClient, useClass: FakeRealtimeClient },
      ],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });
});
