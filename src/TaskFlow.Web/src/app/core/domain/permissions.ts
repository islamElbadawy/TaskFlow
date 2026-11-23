import { Comment, Task, TaskListItem, User } from '../api/models';

type TaskLike = Pick<Task | TaskListItem, 'createdById' | 'assignedToId'>;

export function canCreateTask(user: User | null): boolean {
  if (!user) return false;
  return user.role === 'Admin' || user.role === 'Manager';
}

export function canEditTask(task: TaskLike, user: User | null): boolean {
  if (!user) return false;
  if (user.role === 'Admin' || user.role === 'Manager') return true;
  return task.createdById === user.id;
}

export function canMoveTask(task: TaskLike, user: User | null): boolean {
  if (!user) return false;
  if (canEditTask(task, user)) return true;
  return task.assignedToId === user.id;
}

export function canDeleteTask(task: TaskLike, user: User | null): boolean {
  if (!user) return false;
  return user.role === 'Admin' || task.createdById === user.id;
}

export function canEditComment(comment: Pick<Comment, 'userId'>, user: User | null): boolean {
  if (!user) return false;
  return comment.userId === user.id;
}

export function canDeleteComment(comment: Pick<Comment, 'userId'>, user: User | null): boolean {
  if (!user) return false;
  return user.role === 'Admin' || comment.userId === user.id;
}

export function isAdmin(user: User | null): boolean {
  return user?.role === 'Admin';
}

export function isLastActiveAdmin(target: User, allUsers: readonly User[]): boolean {
  if (target.role !== 'Admin' || !target.isActive) return false;
  const activeAdmins = allUsers.filter((u) => u.role === 'Admin' && u.isActive);
  return activeAdmins.length <= 1;
}

export function canDeactivateUser(
  target: User,
  currentUser: User | null,
  allUsers: readonly User[],
): boolean {
  if (!currentUser || currentUser.role !== 'Admin') return false;
  if (target.id === currentUser.id) return false;
  if (target.isActive && isLastActiveAdmin(target, allUsers)) return false;
  return true;
}
