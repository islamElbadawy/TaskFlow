export type TaskStatus = 'Todo' | 'InProgress' | 'Done';
export type TaskPriority = 'Low' | 'Medium' | 'High';
export type UserRole = 'User' | 'Manager' | 'Admin';
export type NotificationType =
  | 'TaskAssigned'
  | 'TaskUpdated'
  | 'TaskCompleted'
  | 'CommentAdded'
  | 'DeadlineApproaching';

export const TASK_STATUSES: readonly TaskStatus[] = ['Todo', 'InProgress', 'Done'];
export const TASK_PRIORITIES: readonly TaskPriority[] = ['Low', 'Medium', 'High'];
export const USER_ROLES: readonly UserRole[] = ['User', 'Manager', 'Admin'];

export interface ApiResult<T> {
  isSuccess: true;
  data: T;
}

export interface ProblemDetails {
  type?: string;
  title: string;
  status: number;
  detail?: string;
  errors?: Record<string, string[]>;
  traceId?: string;
}

export interface Paginated<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface User {
  id: string;
  firstName: string;
  lastName: string;
  fullName: string;
  email: string;
  role: UserRole;
  isActive: boolean;
  createdAt: string;
}

export interface LoginResponse {
  token: string;
  user: User;
  expiresAt: string;
}

export interface Task {
  id: string;
  title: string;
  description: string;
  status: TaskStatus;
  priority: TaskPriority;
  dueDate: string | null;
  createdById: string;
  createdByName: string;
  assignedToId: string | null;
  assignedToName: string | null;
  createdAt: string;
  updatedAt: string | null;
  commentsCount: number;
  position?: number;
}

export interface TaskListItem {
  id: string;
  title: string;
  status: TaskStatus;
  priority: TaskPriority;
  dueDate: string | null;
  isOverdue: boolean;
  createdById: string;
  createdByName: string;
  assignedToId: string | null;
  assignedToName: string | null;
  commentsCount: number;
  createdAt: string;
  updatedAt: string | null;
  position?: number;
}

export interface Comment {
  id: string;
  content: string;
  taskId: string;
  userId: string;
  userName: string;
  createdAt: string;
  updatedAt: string | null;
}

export interface Notification {
  id: string;
  message: string;
  type: NotificationType;
  isRead: boolean;
  relatedTaskId: string | null;
  createdAt: string;
}

export interface StatusCounts {
  total: number;
  todo: number;
  inProgress: number;
  done: number;
}

export interface Dashboard {
  tasks: StatusCounts;
  overdueTasks: number;
  dueSoonTasks: number;
  myAssignedTasks: StatusCounts;
  myOpenCreatedTasks: number;
  unreadNotifications: number;
  recentTasks: TaskListItem[];
}

export interface RemindersResult {
  remindersCreated: number;
  taskIds: string[];
}

export interface RegisterRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface UpdateUserRequest {
  firstName: string;
  lastName: string;
  email: string;
}

export interface CreateTaskRequest {
  title: string;
  description: string;
  priority: TaskPriority;
  dueDate: string | null;
  assignedToId: string | null;
}

export interface UpdateTaskRequest extends CreateTaskRequest {
  status: TaskStatus;
}

export interface MoveTaskRequest {
  status: TaskStatus;
  beforeTaskId: string | null;
}
