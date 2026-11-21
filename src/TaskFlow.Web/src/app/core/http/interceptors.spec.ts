import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { AuthSessionBridge } from '../auth/auth-session';
import { APP_CONFIG } from '../config/app-config.token';
import { apiBaseInterceptor } from './api-base.interceptor';
import { ApiError } from './api-error';
import { authInterceptor } from './auth.interceptor';
import { envelopeInterceptor } from './envelope.interceptor';
import { errorInterceptor } from './error.interceptor';

describe('HTTP interceptors', () => {
  let http: HttpClient;
  let controller: HttpTestingController;
  let session: AuthSessionBridge;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        { provide: APP_CONFIG, useValue: { apiUrl: 'https://api.example.com' } },
        provideHttpClient(
          withInterceptors([
            apiBaseInterceptor,
            authInterceptor,
            envelopeInterceptor,
            errorInterceptor,
          ]),
        ),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
    session = TestBed.inject(AuthSessionBridge);
  });

  afterEach(() => {
    controller.verify();
  });

  it('prepends base URL, attaches Bearer token, and unwraps ApiResult envelope', async () => {
    session.setToken('jwt-123');
    const promise = firstValueFrom(http.get<{ id: string }>('/api/users/me'));

    const req = controller.expectOne('https://api.example.com/api/users/me');
    expect(req.request.headers.get('Authorization')).toBe('Bearer jwt-123');
    req.flush({ isSuccess: true, data: { id: 'u1' } });

    expect(await promise).toEqual({ id: 'u1' });
  });

  it('maps RFC 9457 ProblemDetails with case-insensitive field errors and Retry-After', async () => {
    const promise = firstValueFrom(http.post('/api/auth/login', {}));

    const req = controller.expectOne('https://api.example.com/api/auth/login');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush(
      {
        title: 'Validation',
        status: 400,
        detail: 'One or more validation errors occurred.',
        errors: { Title: ['Title is required'] },
      },
      { status: 400, statusText: 'Bad Request', headers: { 'Retry-After': '45' } },
    );

    try {
      await promise;
      expect.fail('Expected error');
    } catch (err) {
      expect(err).toBeInstanceOf(ApiError);
      const apiErr = err as ApiError;
      expect(apiErr.status).toBe(400);
      expect(apiErr.fieldError('title')).toBe('Title is required');
      expect(apiErr.retryAfterSeconds).toBe(45);
    }
  });

  it('calls unauthorized handler on 401 for non-login requests only', async () => {
    let called = 0;
    session.registerUnauthorizedHandler(() => {
      called++;
    });

    const p1 = firstValueFrom(http.get('/api/tasks')).catch((e) => e);
    controller
      .expectOne('https://api.example.com/api/tasks')
      .flush({ title: 'Unauthorized', status: 401 }, { status: 401, statusText: 'Unauthorized' });
    await p1;
    expect(called).toBe(1);

    const p2 = firstValueFrom(http.post('/api/auth/login', {})).catch((e) => e);
    controller
      .expectOne('https://api.example.com/api/auth/login')
      .flush({ title: 'Unauthorized', status: 401 }, { status: 401, statusText: 'Unauthorized' });
    await p2;
    expect(called).toBe(1);
  });
});
