import { User } from '../api/models';
import {
  canDeactivateUser,
  canDeleteComment,
  canDeleteTask,
  canEditComment,
  canEditTask,
  canMoveTask,
  isLastActiveAdmin,
} from './permissions';

const makeUser = (id: string, role: User['role'], isActive = true): User => ({
  id,
  firstName: role,
  lastName: 'User',
  fullName: `${role} User`,
  email: `${id}@taskflow.dev`,
  role,
  isActive,
  createdAt: '2026-11-01T00:00:00Z',
});

describe('permissions', () => {
  const admin = makeUser('a1', 'Admin');
  const manager = makeUser('m1', 'Manager');
  const creator = makeUser('u1', 'User');
  const assignee = makeUser('u2', 'User');
  const outsider = makeUser('u3', 'User');
  const task = { createdById: creator.id, assignedToId: assignee.id };

  it('allows creator, Manager, and Admin to edit a task', () => {
    expect(canEditTask(task, creator)).toBe(true);
    expect(canEditTask(task, manager)).toBe(true);
    expect(canEditTask(task, admin)).toBe(true);
    expect(canEditTask(task, assignee)).toBe(false);
    expect(canEditTask(task, outsider)).toBe(false);
    expect(canEditTask(task, null)).toBe(false);
  });

  it('allows assignee to move a task in addition to editors', () => {
    expect(canMoveTask(task, assignee)).toBe(true);
    expect(canMoveTask(task, creator)).toBe(true);
    expect(canMoveTask(task, manager)).toBe(true);
    expect(canMoveTask(task, admin)).toBe(true);
    expect(canMoveTask(task, outsider)).toBe(false);
  });

  it('allows only creator and Admin to delete a task', () => {
    expect(canDeleteTask(task, creator)).toBe(true);
    expect(canDeleteTask(task, admin)).toBe(true);
    expect(canDeleteTask(task, manager)).toBe(false);
    expect(canDeleteTask(task, assignee)).toBe(false);
  });

  it('enforces comment edit (author only) and delete (author or Admin)', () => {
    const comment = { userId: creator.id };
    expect(canEditComment(comment, creator)).toBe(true);
    expect(canEditComment(comment, admin)).toBe(false);
    expect(canDeleteComment(comment, creator)).toBe(true);
    expect(canDeleteComment(comment, admin)).toBe(true);
    expect(canDeleteComment(comment, manager)).toBe(false);
  });

  it('enforces last active Admin and self-deactivation rules', () => {
    const all = [admin, manager, creator];
    expect(isLastActiveAdmin(admin, all)).toBe(true);
    expect(canDeactivateUser(admin, admin, all)).toBe(false);
    expect(canDeactivateUser(manager, admin, all)).toBe(true);
    expect(canDeactivateUser(manager, manager, all)).toBe(false);

    const admin2 = makeUser('a2', 'Admin');
    expect(isLastActiveAdmin(admin, [...all, admin2])).toBe(false);
    expect(canDeactivateUser(admin2, admin, [...all, admin2])).toBe(true);
  });
});
