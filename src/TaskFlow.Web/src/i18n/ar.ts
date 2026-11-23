import type { Dictionary } from './en';

const arPlural = new Intl.PluralRules('ar-EG');
const arNum = new Intl.NumberFormat('ar-EG');

interface ArabicPluralForms {
  zero?: string;
  one?: string;
  two?: string;
  few?: string;
  many?: string;
  other: string;
}

export function pickAr(n: number, forms: ArabicPluralForms): string {
  const category = arPlural.select(n);
  return forms[category] ?? forms.other;
}

export const AR = {
  locale: 'ar-EG',
  otherLang: 'English',
  skipToContent: 'تخطَّ إلى المحتوى',
  theme: 'المظهر',
  system: 'حسب النظام',
  light: 'فاتح',
  dark: 'داكن',
  mainNav: 'القائمة الرئيسية',
  today: 'اليوم',
  tasks: 'المهام',
  board: 'اللوحة',
  users: 'المستخدمون',
  notifications: 'الإشعارات',
  markAllRead: 'تعليم الكل كمقروء',
  seeAllNotifications: 'عرض كل الإشعارات',
  unreadBadge: (n: number) => `الإشعارات، ${arNum.format(n)} غير مقروءة`,
  roleUser: 'مستخدم',
  roleManager: 'مدير',
  roleAdmin: 'مسؤول',
  profile: 'الملف الشخصي',
  logOut: 'تسجيل الخروج',
  you: 'أنت',

  // Status & Priority
  statusTodo: 'للتنفيذ',
  statusInProgress: 'قيد التنفيذ',
  statusDone: 'منجزة',
  priorityLow: 'منخفضة',
  priorityMedium: 'متوسطة',
  priorityHigh: 'عالية',

  // Dashboard
  dashboardHeadline: (myInProgress: number, teamOverdue: number) => {
    const mine =
      myInProgress > 0
        ? pickAr(myInProgress, {
            one: 'لديك مهمة واحدة قيد التنفيذ.',
            two: 'لديك مهمتان قيد التنفيذ.',
            few: `لديك ${arNum.format(myInProgress)} مهام قيد التنفيذ.`,
            many: `لديك ${arNum.format(myInProgress)} مهمة قيد التنفيذ.`,
            other: `لديك ${arNum.format(myInProgress)} مهمة قيد التنفيذ.`,
          })
        : 'لا شيء قيد التنفيذ لك الآن.';
    const late =
      teamOverdue > 0
        ? pickAr(teamOverdue, {
            one: 'لدى الفريق مهمة متأخرة.',
            two: 'لدى الفريق مهمتان متأخرتان.',
            few: `لدى الفريق ${arNum.format(teamOverdue)} مهام متأخرة.`,
            many: `لدى الفريق ${arNum.format(teamOverdue)} مهمة متأخرة.`,
            other: `لدى الفريق ${arNum.format(teamOverdue)} مهمة متأخرة.`,
          })
        : 'لا توجد مهام متأخرة.';
    return `${mine} ${late}`;
  },
  teamTasks: (n: number) => `مهام الفريق: ${arNum.format(n)}`,
  openBoard: 'افتح اللوحة',
  stripLabel: (todo: number, inProgress: number, done: number) =>
    `${arNum.format(todo)} للتنفيذ، ${arNum.format(inProgress)} قيد التنفيذ، ${arNum.format(done)} منجزة`,
  dueThisWeek: 'المستحق هذا الأسبوع',
  overdueCount: (n: number) => `${arNum.format(n)} متأخرة`,
  yourWork: 'عملك',
  openCreated: 'مهام أنشأتها ولم تُنجز',
  unreadNotifications: 'إشعارات غير مقروءة',
  recentTasks: 'مهامك الأخيرة',
  nothingDue: 'لا شيء مستحق خلال الأيام السبعة القادمة.',
  noRecentTasks: 'لا يوجد نشاط حديث بعد.',

  // Board & Task List
  allTasks: 'كل المهام',
  newTask: 'مهمة جديدة',
  boardHint: 'اسحب مهمة إلى عمود آخر لتغيير حالتها، أو لأعلى ولأسفل لإعادة ترتيبها.',
  boardHintStatusOnly: 'اسحب مهمة إلى عمود آخر لتغيير حالتها.',
  reordered: (title: string, status: string) => `أُعيد ترتيب «${title}» في ${status}`,
  moved: (title: string, status: string) => `نُقلت «${title}» إلى ${status}`,
  undo: 'تراجع',
  lockedTooltip: 'لا ينقل هذه المهمة إلا منشئها أو المسند إليه أو مدير أو مسؤول',
  lockedShort: 'مقفلة',
  moveTaskAria: (title: string) => `نقل «${title}» إلى عمود آخر`,
  moveToColumn: (status: string) => `نقل إلى ${status}`,
  open: 'فتح',
  noDueDate: 'بلا موعد',
  loadMore: 'تحميل المزيد',
  overdueToday: 'متأخرة اليوم',
  daysOverdue: (n: number) =>
    pickAr(n, {
      one: 'متأخرة يومًا',
      two: 'متأخرة يومين',
      few: `متأخرة ${arNum.format(n)} أيام`,
      many: `متأخرة ${arNum.format(n)} يومًا`,
      other: `متأخرة ${arNum.format(n)} يوم`,
    }),
  any: 'الكل',
  anyone: 'أي شخص',
  unassigned: 'غير مسندة',
  overdueOnly: 'المتأخرة فقط',
  searchTasks: 'ابحث في المهام',
  clearFilters: 'مسح عوامل التصفية',
  colTask: 'المهمة',
  showingRange: (from: number, to: number, total: number) =>
    `عرض ${arNum.format(from)} إلى ${arNum.format(to)} من ${arNum.format(total)}`,
  previous: 'السابق',
  next: 'التالي',
  rows: 'الصفوف',
  emptyFiltered: 'لا توجد مهام تطابق عوامل التصفية.',
  emptyTasks: 'لا توجد مهام بعد. أنشئ أول مهمة.',

  // Task page
  edit: 'تعديل',
  delete: 'حذف',
  commentsCount: (n: number) =>
    n > 0
      ? pickAr(n, {
          one: 'تعليق واحد',
          two: 'تعليقان',
          few: `${arNum.format(n)} تعليقات`,
          many: `${arNum.format(n)} تعليقًا`,
          other: `${arNum.format(n)} تعليق`,
        })
      : 'لا تعليقات بعد',
  commentsCol: 'التعليقات',
  loadOlderComments: 'تحميل التعليقات الأقدم',
  addComment: 'أضف تعليقًا',
  postComment: 'نشر التعليق',
  commentPosted: 'تم نشر التعليق',
  edited: 'معدَّل',
  save: 'حفظ',
  cancel: 'إلغاء',
  status: 'الحالة',
  assignedTo: 'مسندة إلى',
  due: 'الموعد',
  priority: 'الأولوية',
  createdBy: 'أنشأها',
  createdVal: (who: string, ago: string) => `${who}، ${ago}`,
  noDescription: 'بلا وصف',
  statusChanged: (status: string) => `تغيّرت الحالة إلى ${status}`,
  assigneeChanged: 'تم تحديث المسند إليه',
  taskNotFound: 'هذه المهمة حُذفت أو لم تكن موجودة.',
  backToTasks: 'العودة إلى المهام',
  deleteTaskTitle: (title: string) => `حذف «${title}»؟`,
  deleteTaskBody: (comments: number) =>
    comments > 0
      ? pickAr(comments, {
          one: 'سيُحذف تعليقها أيضًا.',
          two: 'سيُحذف تعليقاها أيضًا.',
          few: `ستُحذف تعليقاتها الـ${arNum.format(comments)} أيضًا.`,
          many: `ستُحذف تعليقاتها الـ${arNum.format(comments)} أيضًا.`,
          other: `ستُحذف تعليقاتها الـ${arNum.format(comments)} أيضًا.`,
        })
      : 'لا يمكن التراجع عن ذلك.',
  taskDeleted: 'تم حذف المهمة',
  deleteCommentTitle: 'حذف هذا التعليق؟',
  deleteCommentBody: 'لا يمكن التراجع عن ذلك.',
  commentDeleted: 'تم حذف التعليق',

  // Task Form
  newTaskTitle: 'مهمة جديدة',
  editTaskTitle: 'تعديل المهمة',
  title: 'العنوان',
  description: 'الوصف',
  optional: '(اختياري)',
  dueDate: 'تاريخ الاستحقاق',
  clear: 'مسح',
  assignee: 'المسند إليه',
  createTask: 'إنشاء المهمة',
  saveChanges: 'حفظ التغييرات',
  titleRequired: 'العنوان مطلوب.',
  titleTooLong: 'يجب ألا يتجاوز العنوان ٢٠٠ حرف.',
  descriptionTooLong: 'يجب ألا يتجاوز الوصف ٢٠٠٠ حرف.',
  taskCreated: 'تم إنشاء المهمة',
  changesSaved: 'تم حفظ التغييرات',
  newStartsTodo: 'تبدأ المهام الجديدة في «للتنفيذ».',
  discardTitle: 'تجاهل التغييرات؟',
  discardBody: 'ستفقد تعديلاتك على هذه المهمة.',
  discard: 'تجاهل',
  keepEditing: 'متابعة التعديل',

  // Auth
  tagline: 'عمل فريقك، في حركة.',
  taglineSub: 'أسنِد المهام، وانقلها إلى الإنجاز، وتابِع ما يقترب موعده، معًا.',
  logIn: 'تسجيل الدخول',
  welcomeSub: 'مرحبًا بعودتك إلى لوحة فريقك.',
  email: 'البريد الإلكتروني',
  password: 'كلمة المرور',
  newHere: 'جديد هنا؟',
  createAccount: 'أنشئ حسابًا',
  showPassword: 'إظهار كلمة المرور',
  hidePassword: 'إخفاء كلمة المرور',
  sessionEnded: 'انتهت جلستك. يُرجى تسجيل الدخول مرة أخرى.',
  badCredentials: 'البريد الإلكتروني أو كلمة المرور غير صحيحة.',
  tooManyAttempts: (n: number) => `محاولات كثيرة. حاول مرة أخرى بعد ${arNum.format(n)} ثانية.`,
  createAccountTitle: 'أنشئ حسابك',
  registerSub: 'انضم إلى لوحة فريقك في دقيقة.',
  firstName: 'الاسم الأول',
  lastName: 'اسم العائلة',
  confirmPassword: 'تأكيد كلمة المرور',
  passwordHint: '٦ أحرف على الأقل.',
  createAccountBtn: 'إنشاء الحساب',
  haveAccount: 'لديك حساب بالفعل؟',
  fieldRequired: 'هذا الحقل مطلوب.',
  passwordShort: 'استخدم ٦ أحرف على الأقل.',
  passwordMismatch: 'كلمتا المرور غير متطابقتين.',
  emailInvalid: 'أدخل بريدًا إلكترونيًا صحيحًا.',
  emailTaken: 'هذا البريد مسجّل بالفعل.',
  welcomeToast: 'مرحبًا بك في Taskaty',

  // Notifications
  filterAll: 'الكل',
  filterUnread: 'غير المقروءة',
  filterRead: 'المقروءة',
  caughtUp: 'لا جديد، أنت على اطلاع بكل شيء.',
  markedReadCount: (n: number) => `تم تعليم ${arNum.format(n)} كمقروءة`,
  allRead: 'تم تعليم كل الإشعارات كمقروءة',
  notifAssigned: (actor: string, task: string) => `أسند إليك ${actor} «${task}»`,
  notifUpdated: (actor: string, task: string) => `حدّث ${actor} «${task}»`,
  notifCompleted: (actor: string, task: string) => `أنجز ${actor} «${task}»`,
  notifComment: (actor: string, task: string) => `علّق ${actor} على «${task}»`,
  notifDeadline: (task: string) => `يقترب موعد «${task}»`,

  // Profile
  yourDetails: 'بياناتك',
  profileSaved: 'تم حفظ الملف الشخصي',
  passwordSection: 'كلمة المرور',
  currentPassword: 'كلمة المرور الحالية',
  newPassword: 'كلمة المرور الجديدة',
  changePasswordBtn: 'تغيير كلمة المرور',
  passwordChanged: 'تم تغيير كلمة المرور. يُرجى تسجيل الدخول مرة أخرى.',
  wrongCurrentPassword: 'كلمة المرور الحالية غير صحيحة.',
  preferences: 'التفضيلات',
  themeAndLang: 'المظهر واللغة',
  dangerZone: 'منطقة الخطر',
  deleteAccount: 'حذف حسابي',
  deleteAccountBody:
    'لا يمكن التراجع عن حذف حسابك. إن كنت أنشأت مهامًا، فاطلب ذلك من مسؤول.',
  deleteAccountTitle: 'حذف حسابك؟',
  typeEmailConfirm: (email: string) => `اكتب ${email} للتأكيد.`,
  deleteForever: 'حذف الحساب',
  cantDeleteOwnAccount: 'لا يمكن حذف حسابك لأنك أنشأت مهامًا. اطلب ذلك من مسؤول.',

  // Users (Admin)
  addUser: 'إضافة مستخدم',
  createUser: 'إنشاء المستخدم',
  userCreated: 'تم إنشاء المستخدم',
  runReminders: 'إرسال تذكيرات المواعيد الآن',
  filterActive: 'النشطون',
  filterDeactivated: 'المعطَّلون',
  searchUsers: 'ابحث بالاسم أو البريد',
  colName: 'الاسم',
  colRole: 'الدور',
  colJoined: 'تاريخ الانضمام',
  active: 'نشط',
  deactivated: 'معطَّل',
  rowActionsAria: (name: string) => `إجراءات ${name}`,
  editUser: 'تعديل الاسم والبريد',
  deactivate: 'تعطيل',
  reactivate: 'إعادة التفعيل',
  roleChangeTitle: (name: string, role: string) => `تغيير دور ${name} إلى ${role}؟`,
  roleChangeBody: 'سيُسجَّل خروجه من كل الجلسات.',
  changeRole: 'تغيير الدور',
  deactivateTitle: (name: string) => `تعطيل ${name}؟`,
  deactivateBody: 'لن يتمكن من تسجيل الدخول وسيُسجَّل خروجه فورًا.',
  reactivateTitle: (name: string) => `إعادة تفعيل ${name}؟`,
  reactivateBody: 'سيتمكن من تسجيل الدخول مجددًا.',
  deleteUserTitle: (name: string) => `حذف ${name}؟`,
  deleteUserBody: 'لا يمكن حذف من أنشأ مهامًا.',
  userDeleted: 'تم حذف المستخدم',
  userSaved: 'تم حفظ المستخدم',
  roleChanged: 'تم تغيير الدور',
  userDeactivated: 'تم تعطيل المستخدم',
  userReactivated: 'تمت إعادة تفعيل المستخدم',
  lastAdminRule: 'لا يمكن خفض دور آخر مسؤول نشط أو تعطيله أو حذفه.',
  selfDeactivateRule: 'لا يمكنك تعطيل حسابك.',
  remindersSent: (n: number) =>
    pickAr(n, {
      one: 'تم إرسال تذكير واحد',
      two: 'تم إرسال تذكيرين',
      few: `تم إرسال ${arNum.format(n)} تذكيرات`,
      many: `تم إرسال ${arNum.format(n)} تذكيرًا`,
      other: `تم إرسال ${arNum.format(n)} تذكير`,
    }),
  noRemindersDue: 'لا توجد تذكيرات مستحقة',
  emptyUsers: 'لا يوجد مستخدمون مطابقون.',

  // Common / Errors / Not found
  notFoundTitle: 'هذه الصفحة غير موجودة.',
  notFoundSub: 'ربما الرابط قديم، أو نُقلت الصفحة.',
  goToday: 'اذهب إلى اليوم',
  errorTitle: 'تعذّر التحميل.',
  errorBody: 'تحقق من اتصالك وحاول مرة أخرى.',
  retry: 'إعادة المحاولة',
  genericError: 'حدث خطأ ما. يُرجى المحاولة مرة أخرى.',
  offlineToast: 'أنت غير متصل. ستفشل التغييرات حتى يعود الاتصال.',
  forbiddenError: 'ليست لديك صلاحية للقيام بذلك.',
  loading: 'جارٍ التحميل…',
} as const satisfies Dictionary;
