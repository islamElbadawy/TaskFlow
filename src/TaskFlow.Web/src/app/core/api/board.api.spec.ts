import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { envelopeInterceptor } from '../http/envelope.interceptor';
import { errorInterceptor } from '../http/error.interceptor';
import { BoardApi } from './board.api';
import { TaskListItem } from './models';

const sampleTask = (withPosition: boolean): TaskListItem => ({
  id: 't1',
  title: 'Write docs',
  status: 'Todo',
  priority: 'High',
  dueDate: null,
  isOverdue: false,
  createdById: 'u1',
  createdByName: 'Manager',
  assignedToId: null,
  assignedToName: null,
  commentsCount: 0,
  createdAt: '2026-11-20T00:00:00Z',
  updatedAt: null,
  ...(withPosition ? { position: 1000 } : {}),
});

describe('BoardApi', () => {
  let api: BoardApi;
  let controller: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([envelopeInterceptor, errorInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    api = TestBed.inject(BoardApi);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('uses sort=position and PATCH /move directly', () => {
    let loaded = 0;
    api.loadColumn('Todo', {}).subscribe((res) => {
      loaded = res.items.length;
    });

    const req = controller.expectOne(
      (r) => r.url === '/api/tasks' && r.params.get('sort') === 'position',
    );
    req.flush({
      isSuccess: true,
      data: {
        items: [sampleTask(true)],
        pageNumber: 1,
        pageSize: 100,
        totalCount: 1,
        totalPages: 1,
        hasPreviousPage: false,
        hasNextPage: false,
      },
    });
    expect(loaded).toBe(1);

    let movedStatus = '';
    api.moveTask('t1', 'InProgress', null).subscribe((task) => {
      movedStatus = task.status;
    });

    const moveReq = controller.expectOne('/api/tasks/t1/move');
    expect(moveReq.request.method).toBe('PATCH');
    moveReq.flush({
      isSuccess: true,
      data: { ...sampleTask(true), status: 'InProgress', description: '' },
    });
    expect(movedStatus).toBe('InProgress');
  });

  it('propagates 404 on PATCH /move instead of silently disabling position mode', () => {
    let errorStatus = 0;
    api.moveTask('missing', 'Done', null).subscribe({
      error: (err: { status: number }) => {
        errorStatus = err.status;
      },
    });

    const moveReq = controller.expectOne('/api/tasks/missing/move');
    moveReq.flush({ title: 'NotFound', status: 404 }, { status: 404, statusText: 'Not Found' });
    expect(errorStatus).toBe(404);
  });
});
