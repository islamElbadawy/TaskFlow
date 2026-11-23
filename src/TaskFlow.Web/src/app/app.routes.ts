import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { guestGuard } from './core/auth/guest.guard';
import { roleGuard } from './core/auth/role.guard';
import { unsavedChangesGuard } from './core/auth/unsaved-changes.guard';

export const routes: Routes = [
  {
    path: 'login',
    title: 'logIn',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login.page').then((m) => m.LoginPage),
  },
  {
    path: 'register',
    title: 'createAccountTitle',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/register.page').then((m) => m.RegisterPage),
  },
  {
    path: '',
    loadComponent: () => import('./layout/shell').then((m) => m.AppShell),
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        title: 'today',
        loadComponent: () =>
          import('./features/dashboard/dashboard.page').then((m) => m.DashboardPage),
      },
      {
        path: 'tasks',
        title: 'allTasks',
        loadComponent: () => import('./features/tasks/task-list.page').then((m) => m.TaskListPage),
      },
      {
        path: 'tasks/board',
        title: 'board',
        loadComponent: () => import('./features/tasks/board.page').then((m) => m.BoardPage),
      },
      {
        path: 'tasks/new',
        title: 'newTaskTitle',
        canActivate: [roleGuard('Admin', 'Manager')],
        canDeactivate: [unsavedChangesGuard],
        loadComponent: () => import('./features/tasks/task-form.page').then((m) => m.TaskFormPage),
      },
      {
        path: 'tasks/:id/edit',
        title: 'editTaskTitle',
        canDeactivate: [unsavedChangesGuard],
        loadComponent: () => import('./features/tasks/task-form.page').then((m) => m.TaskFormPage),
      },
      {
        path: 'tasks/:id',
        title: 'tasks',
        loadComponent: () => import('./features/tasks/task.page').then((m) => m.TaskPage),
      },
      {
        path: 'notifications',
        title: 'notifications',
        loadComponent: () =>
          import('./features/notifications/notifications.page').then((m) => m.NotificationsPage),
      },
      {
        path: 'profile',
        title: 'profile',
        loadComponent: () => import('./features/profile/profile.page').then((m) => m.ProfilePage),
      },
      {
        path: 'admin/users',
        title: 'users',
        canActivate: [roleGuard('Admin')],
        loadComponent: () => import('./features/admin/users.page').then((m) => m.UsersPage),
      },
    ],
  },
  {
    path: '**',
    title: 'notFoundTitle',
    loadComponent: () => import('./features/not-found/not-found.page').then((m) => m.NotFoundPage),
  },
];
