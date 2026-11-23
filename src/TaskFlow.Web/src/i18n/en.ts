const enPlural = new Intl.PluralRules('en-GB');
const enNum = new Intl.NumberFormat('en-GB');

function pickEn(n: number, forms: { one: string; other: string }): string {
  return enPlural.select(n) === 'one' ? forms.one : forms.other;
}

export const EN = {
  locale: 'en-GB',
  otherLang: 'العربية',
  skipToContent: 'Skip to content',
  theme: 'Theme',
  system: 'System',
  light: 'Light',
  dark: 'Dark',
  mainNav: 'Main navigation',
  today: 'Today',
  tasks: 'Tasks',
  board: 'Board',
  users: 'Users',
  notifications: 'Notifications',
  markAllRead: 'Mark all as read',
  seeAllNotifications: 'See all notifications',
  unreadBadge: (n: number) => `Notifications, ${enNum.format(n)} unread`,
  roleUser: 'User',
  roleManager: 'Manager',
  roleAdmin: 'Admin',
  profile: 'Profile',
  logOut: 'Log out',
  you: 'You',

  // Status & Priority
  statusTodo: 'To do',
  statusInProgress: 'In progress',
  statusDone: 'Done',
  priorityLow: 'Low',
  priorityMedium: 'Medium',
  priorityHigh: 'High',

  // Dashboard
  dashboardHeadline: (myInProgress: number, teamOverdue: number) => {
    const mine =
      myInProgress > 0
        ? pickEn(myInProgress, {
            one: `You have ${enNum.format(myInProgress)} task in progress.`,
            other: `You have ${enNum.format(myInProgress)} tasks in progress.`,
          })
        : 'Nothing in progress for you right now.';
    const late =
      teamOverdue > 0
        ? `The team has ${enNum.format(teamOverdue)} overdue.`
        : 'Nothing is overdue.';
    return `${mine} ${late}`;
  },
  teamTasks: (n: number) => `The team’s ${enNum.format(n)} tasks`,
  openBoard: 'Open the board',
  stripLabel: (todo: number, inProgress: number, done: number) =>
    `${enNum.format(todo)} to do, ${enNum.format(inProgress)} in progress, ${enNum.format(done)} done`,
  dueThisWeek: 'Due this week',
  overdueCount: (n: number) => `${enNum.format(n)} overdue`,
  yourWork: 'Your work',
  openCreated: 'Open tasks you created',
  unreadNotifications: 'Unread notifications',
  recentTasks: 'Your recent tasks',
  nothingDue: 'Nothing due in the next 7 days.',
  noRecentTasks: 'No recent activity yet.',

  // Board & Task List
  allTasks: 'All tasks',
  newTask: 'New task',
  boardHint: 'Drag a task to another column to change its status, or up and down to reorder it.',
  boardHintStatusOnly: 'Drag a task to another column to change its status.',
  reordered: (title: string, status: string) => `Reordered “${title}” in ${status}`,
  moved: (title: string, status: string) => `Moved “${title}” to ${status}`,
  undo: 'Undo',
  lockedTooltip: 'Only the creator, assignee, a Manager or an Admin can move this task',
  lockedShort: 'Locked',
  moveTaskAria: (title: string) => `Move “${title}” to another column`,
  moveToColumn: (status: string) => `Move to ${status}`,
  open: 'Open',
  noDueDate: 'No due date',
  loadMore: 'Load more',
  overdueToday: 'Overdue today',
  daysOverdue: (n: number) =>
    pickEn(n, {
      one: `${enNum.format(n)} day overdue`,
      other: `${enNum.format(n)} days overdue`,
    }),
  any: 'Any',
  anyone: 'Anyone',
  unassigned: 'Unassigned',
  overdueOnly: 'Overdue only',
  searchTasks: 'Search tasks',
  clearFilters: 'Clear filters',
  colTask: 'Task',
  showingRange: (from: number, to: number, total: number) =>
    `Showing ${enNum.format(from)} to ${enNum.format(to)} of ${enNum.format(total)}`,
  previous: 'Previous',
  next: 'Next',
  rows: 'Rows',
  emptyFiltered: 'No tasks match these filters.',
  emptyTasks: 'No tasks yet. Create the first one.',

  // Task page
  edit: 'Edit',
  delete: 'Delete',
  commentsCount: (n: number) =>
    n > 0
      ? pickEn(n, {
          one: `${enNum.format(n)} comment`,
          other: `${enNum.format(n)} comments`,
        })
      : 'No comments yet',
  commentsCol: 'Comments',
  loadOlderComments: 'Load older comments',
  addComment: 'Add a comment',
  postComment: 'Post comment',
  commentPosted: 'Comment posted',
  edited: 'edited',
  save: 'Save',
  cancel: 'Cancel',
  status: 'Status',
  assignedTo: 'Assigned to',
  due: 'Due',
  priority: 'Priority',
  createdBy: 'Created by',
  createdVal: (who: string, ago: string) => `${who}, ${ago}`,
  noDescription: 'No description',
  statusChanged: (status: string) => `Status changed to ${status}`,
  assigneeChanged: 'Assignee updated',
  taskNotFound: 'This task was deleted or never existed.',
  backToTasks: 'Back to tasks',
  deleteTaskTitle: (title: string) => `Delete “${title}”?`,
  deleteTaskBody: (comments: number) =>
    comments > 0
      ? pickEn(comments, {
          one: `Its ${enNum.format(comments)} comment is deleted too.`,
          other: `Its ${enNum.format(comments)} comments are deleted too.`,
        })
      : 'This can’t be undone.',
  taskDeleted: 'Task deleted',
  deleteCommentTitle: 'Delete this comment?',
  deleteCommentBody: 'This can’t be undone.',
  commentDeleted: 'Comment deleted',

  // Task Form
  newTaskTitle: 'New task',
  editTaskTitle: 'Edit task',
  title: 'Title',
  description: 'Description',
  optional: '(optional)',
  dueDate: 'Due date',
  clear: 'Clear',
  assignee: 'Assignee',
  createTask: 'Create task',
  saveChanges: 'Save changes',
  titleRequired: 'Title is required.',
  titleTooLong: 'Title must be 200 characters or fewer.',
  descriptionTooLong: 'Description must be 2000 characters or fewer.',
  taskCreated: 'Task created',
  changesSaved: 'Changes saved',
  newStartsTodo: 'New tasks start in To do.',
  discardTitle: 'Discard changes?',
  discardBody: 'Your edits to this task will be lost.',
  discard: 'Discard',
  keepEditing: 'Keep editing',

  // Auth
  tagline: 'Your team’s work, moving.',
  taglineSub: 'Assign tasks, move them to done and see what’s due, together.',
  logIn: 'Log in',
  welcomeSub: 'Welcome back to your team’s board.',
  email: 'Email',
  password: 'Password',
  newHere: 'New here?',
  createAccount: 'Create an account',
  showPassword: 'Show password',
  hidePassword: 'Hide password',
  sessionEnded: 'Your session ended. Please log in again.',
  badCredentials: 'Email or password is incorrect.',
  tooManyAttempts: (n: number) => `Too many attempts. Try again in ${enNum.format(n)} seconds.`,
  createAccountTitle: 'Create your account',
  registerSub: 'Join your team’s board in a minute.',
  firstName: 'First name',
  lastName: 'Last name',
  confirmPassword: 'Confirm password',
  passwordHint: 'At least 6 characters.',
  createAccountBtn: 'Create account',
  haveAccount: 'Already have an account?',
  fieldRequired: 'This field is required.',
  passwordShort: 'Use at least 6 characters.',
  passwordMismatch: 'Passwords don’t match.',
  emailInvalid: 'Enter a valid email address.',
  emailTaken: 'This email is already registered.',
  welcomeToast: 'Welcome to Taskaty',

  // Notifications
  filterAll: 'All',
  filterUnread: 'Unread',
  filterRead: 'Read',
  caughtUp: 'You’re all caught up.',
  markedReadCount: (n: number) => `Marked ${enNum.format(n)} as read`,
  allRead: 'All notifications marked as read',
  notifAssigned: (actor: string, task: string) => `${actor} assigned you “${task}”`,
  notifUpdated: (actor: string, task: string) => `${actor} updated “${task}”`,
  notifCompleted: (actor: string, task: string) => `${actor} completed “${task}”`,
  notifComment: (actor: string, task: string) => `${actor} commented on “${task}”`,
  notifDeadline: (task: string) => `“${task}” deadline is approaching`,

  // Profile
  yourDetails: 'Your details',
  profileSaved: 'Profile saved',
  passwordSection: 'Password',
  currentPassword: 'Current password',
  newPassword: 'New password',
  changePasswordBtn: 'Change password',
  passwordChanged: 'Password changed. Please log in again.',
  wrongCurrentPassword: 'The current password is incorrect.',
  preferences: 'Preferences',
  themeAndLang: 'Theme and language',
  dangerZone: 'Danger zone',
  deleteAccount: 'Delete my account',
  deleteAccountBody:
    'Deleting your account can’t be undone. If you created tasks, ask an Admin instead.',
  deleteAccountTitle: 'Delete your account?',
  typeEmailConfirm: (email: string) => `Type ${email} to confirm.`,
  deleteForever: 'Delete account',
  cantDeleteOwnAccount: 'You created tasks, so your account can’t be deleted. Ask an Admin.',

  // Users (Admin)
  addUser: 'Add user',
  createUser: 'Create user',
  userCreated: 'User created',
  runReminders: 'Run deadline reminders now',
  filterActive: 'Active',
  filterDeactivated: 'Deactivated',
  searchUsers: 'Search name or email',
  colName: 'Name',
  colRole: 'Role',
  colJoined: 'Joined',
  active: 'Active',
  deactivated: 'Deactivated',
  rowActionsAria: (name: string) => `Actions for ${name}`,
  editUser: 'Edit name and email',
  deactivate: 'Deactivate',
  reactivate: 'Reactivate',
  roleChangeTitle: (name: string, role: string) => `Change ${name} to ${role}?`,
  roleChangeBody: 'They’ll be signed out of every session.',
  changeRole: 'Change role',
  deactivateTitle: (name: string) => `Deactivate ${name}?`,
  deactivateBody: 'They can’t log in and are signed out immediately.',
  reactivateTitle: (name: string) => `Reactivate ${name}?`,
  reactivateBody: 'They can log in again.',
  deleteUserTitle: (name: string) => `Delete ${name}?`,
  deleteUserBody: 'Users who created tasks can’t be deleted.',
  userDeleted: 'User deleted',
  userSaved: 'User saved',
  roleChanged: 'Role changed',
  userDeactivated: 'User deactivated',
  userReactivated: 'User reactivated',
  lastAdminRule: 'The last active Admin can’t be demoted, deactivated or deleted.',
  selfDeactivateRule: 'You can’t deactivate yourself.',
  remindersSent: (n: number) =>
    pickEn(n, {
      one: `Sent ${enNum.format(n)} reminder`,
      other: `Sent ${enNum.format(n)} reminders`,
    }),
  noRemindersDue: 'No reminders were due',
  emptyUsers: 'No users match.',

  // Common / Errors / Not found
  notFoundTitle: 'This page doesn’t exist.',
  notFoundSub: 'The link may be old, or the page was moved.',
  goToday: 'Go to Today',
  errorTitle: 'Couldn’t load this.',
  errorBody: 'Check your connection and try again.',
  retry: 'Retry',
  genericError: 'Something went wrong. Please try again.',
  offlineToast: 'You’re offline. Changes will fail until you reconnect.',
  forbiddenError: 'You don’t have permission to do that.',
  loading: 'Loading…',
} as const;

export type Dictionary = {
  readonly [K in keyof typeof EN]: (typeof EN)[K] extends (...args: infer A) => string
    ? (...args: A) => string
    : string;
};

export type TranslationKey = keyof Dictionary;
