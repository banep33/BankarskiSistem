import { del, download, get, post, put, REFRESH_HEADER, withIdempotency } from './http'
import type {
  Account,
  AccountStatement,
  SavingsGoal,
  BudgetItem,
  SpendingCategory,
  SpendingSummary,
  AccountStatistics,
  AccountStatus,
  AppNotification,
  AuditLogEntry,
  Beneficiary,
  BeneficiaryRequest,
  ComplaintCategory,
  ComplaintDetail,
  ComplaintStatus,
  ComplaintSummary,
  ComplianceAlert,
  ComplianceAlertStatus,
  ConsentEvent,
  ConsentRecord,
  CursorResponse,
  CustomerDashboard,
  DataAccessEntry,
  FaqArticle,
  Fee,
  FeedbackEntry,
  FeedbackSummary,
  FinancialOperationResponse,
  KycStatus,
  LedgerAccountBalance,
  LegalDocument,
  LoginResponse,
  PagedResponse,
  PaymentOrder,
  PaymentOrderStatus,
  PaymentQuote,
  PasswordResetRequest,
  PasswordResetStatus,
  PaymentRequest,
  Preferences,
  PrivacyRequest,
  PrivacyRequestStatus,
  PrivacyRequestType,
  Profile,
  ReconciliationLine,
  ReferenceData,
  RiskLevel,
  SecurityEvent,
  Session,
  StatisticsPeriod,
  SupportContact,
  SurveyType,
  SystemOverview,
  Transaction,
  TransactionStatus,
  TransactionType,
  CurrencyExchange,
  ExchangeQuote,
  ExchangeRate,
  ReversalStatus,
  StandingOrder,
  TransactionReversal,
  StandingOrderDetail,
  StandingOrderRequest,
  TrialBalanceLine,
  TwoFactorChallengeResponse,
  TwoFactorStatus,
  User,
  UserRole,
} from '@/types/api'

export type TransactionFilters = {
  accountId?: number
  type?: TransactionType
  status?: TransactionStatus
  from?: string
  to?: string
  search?: string
}

export const authApi = {
  login: (email: string, password: string) => post<LoginResponse | TwoFactorChallengeResponse>('/auth/login', { email, password }),
  completeTwoFactor: (challengeToken: string, code: string) => post<LoginResponse>('/auth/login/two-factor', { challengeToken, code }),
  refresh: () => post<LoginResponse>('/auth/refresh', undefined, { headers: { [REFRESH_HEADER]: '1' } }),
  logout: () => post<void>('/auth/logout'),
  requestPasswordReset: (email: string, newPassword: string, confirmNewPassword: string) =>
    post<{ message: string }>('/auth/password-reset', { email, newPassword, confirmNewPassword }),
}

export const meApi = {
  profile: () => get<Profile>('/me'),
  updateProfile: (firstName: string, lastName: string) => put<User>('/me', { firstName, lastName }),
  changePassword: (currentPassword: string, newPassword: string, confirmNewPassword: string) =>
    put<LoginResponse>('/me/password', { currentPassword, newPassword, confirmNewPassword }),
  dashboard: () => get<CustomerDashboard>('/me/dashboard'),
  accounts: () => get<Account[]>('/me/accounts'),
  transactions: (filters: TransactionFilters, cursor?: string | null, limit = 20) =>
    get<CursorResponse<Transaction>>('/me/transactions', { ...filters, cursor, limit }),
}

export const accountsApi = {
  get: (id: number) => get<Account>(`/accounts/${id}`),
  list: (search?: string, page = 1, pageSize = 20) => get<PagedResponse<Account>>('/accounts', { search, page, pageSize }),
  statistics: (id: number, period: StatisticsPeriod) => get<AccountStatistics>(`/accounts/${id}/statistics`, { period }),
  transactions: (id: number, filters: TransactionFilters, cursor?: string | null, limit = 20) =>
    get<CursorResponse<Transaction>>(`/accounts/${id}/transactions`, { ...filters, cursor, limit }),
  open: (userId: number, currency: string) => post<Account>('/accounts', { userId, currency }),
  deposit: (id: number, amount: number, description: string | null, key: string) =>
    post<FinancialOperationResponse>(`/accounts/${id}/deposits`, { amount, description }, withIdempotency(key)),
  withdraw: (id: number, amount: number, description: string | null, key: string) =>
    post<FinancialOperationResponse>(`/accounts/${id}/withdrawals`, { amount, description }, withIdempotency(key)),
  changeStatus: (id: number, status: AccountStatus) => put<Account>(`/accounts/${id}/status`, { status }),
}

export const paymentsApi = {
  quote: (request: PaymentRequest) => post<PaymentQuote>('/payments/quote', request),
  create: (request: PaymentRequest, key: string) => post<PaymentOrder>('/payments', request, withIdempotency(key)),
  list: (status?: PaymentOrderStatus, page = 1, pageSize = 20) =>
    get<PagedResponse<PaymentOrder>>('/payments', { status, page, pageSize }),
  get: (id: number) => get<PaymentOrder>(`/payments/${id}`),
  cancel: (id: number) => post<PaymentOrder>(`/payments/${id}/cancel`),
}

export interface ExchangeRequest {
  fromAccountId: number
  toAccountId: number
  amount: number
  amountIsTarget: boolean
  expectedRateId: number | null
}

export const exchangeApi = {
  rates: () => get<ExchangeRate[]>('/exchange-rates'),
  history: (currency: string, days = 30) => get<ExchangeRate[]>('/exchange-rates/history', { currency, days }),
  quote: (request: ExchangeRequest) => post<ExchangeQuote>('/exchange/quote', request),
  execute: (request: ExchangeRequest, key: string) => post<CurrencyExchange>('/exchange', request, withIdempotency(key)),
  mine: (page = 1) => get<PagedResponse<CurrencyExchange>>('/exchange', { page }),
  publish: (rates: { currency: string; buyRate: number; middleRate: number; sellRate: number }[]) =>
    post<ExchangeRate[]>('/admin/exchange-rates', { rates }),
}

export const savingsApi = {
  list: () => get<SavingsGoal[]>('/me/savings-goals'),
  create: (name: string, targetAmount: number, targetDate: string | null, currency: string) =>
    post<SavingsGoal>('/me/savings-goals', { name, targetAmount, targetDate, currency }),
  update: (id: number, name: string, targetAmount: number, targetDate: string | null) =>
    put<SavingsGoal>(`/me/savings-goals/${id}`, { name, targetAmount, targetDate }),
  contribute: (id: number, accountId: number, amount: number, key: string) =>
    post<SavingsGoal>(`/me/savings-goals/${id}/contributions`, { accountId, amount }, withIdempotency(key)),
  withdraw: (id: number, accountId: number, amount: number, key: string) =>
    post<SavingsGoal>(`/me/savings-goals/${id}/withdrawals`, { accountId, amount }, withIdempotency(key)),
  close: (id: number, accountId: number) => post<SavingsGoal>(`/me/savings-goals/${id}/close`, { accountId }),
}

export const budgetsApi = {
  categories: () => get<SpendingCategory[]>('/categories'),
  spending: (month?: string) => get<SpendingSummary>('/me/spending', { month }),
  assign: (transactionId: number, categoryId: number, rememberForCounterparty: boolean) =>
    put<void>(`/me/transactions/${transactionId}/category`, { categoryId, rememberForCounterparty }),
  list: () => get<BudgetItem[]>('/me/budgets'),
  set: (categoryId: number, monthlyLimit: number) => put<BudgetItem>('/me/budgets', { categoryId, monthlyLimit }),
  remove: (id: number) => del(`/me/budgets/${id}`),
}

export const statementsApi = {
  get: (accountId: number, from: string, to: string) => get<AccountStatement>(`/accounts/${accountId}/statement`, { from, to }),
  csv: (accountId: number, from: string, to: string) =>
    download(`/accounts/${accountId}/statement/csv`, { from, to }, `izvod-${accountId}-${from}-${to}.csv`),
}

export const reversalsApi = {
  request: (transactionId: number, reason: string) => post<TransactionReversal>(`/transactions/${transactionId}/reversals`, { reason }),
  list: (status?: ReversalStatus, page = 1) => get<PagedResponse<TransactionReversal>>('/reversals', { status, page }),
  approve: (id: number, note: string | null) => post<TransactionReversal>(`/reversals/${id}/approve`, { note }),
  reject: (id: number, note: string) => post<TransactionReversal>(`/reversals/${id}/reject`, { note }),
}

export const standingOrdersApi = {
  list: () => get<StandingOrder[]>('/standing-orders'),
  get: (id: number) => get<StandingOrderDetail>(`/standing-orders/${id}`),
  create: (request: StandingOrderRequest) => post<StandingOrder>('/standing-orders', request),
  update: (
    id: number,
    request: { amount: number; purpose: string; referenceModel: string | null; referenceNumber: string | null; endDate: string | null; maxExecutions: number | null },
  ) => put<StandingOrder>(`/standing-orders/${id}`, request),
  pause: (id: number) => post<StandingOrder>(`/standing-orders/${id}/pause`),
  resume: (id: number) => post<StandingOrder>(`/standing-orders/${id}/resume`),
  cancel: (id: number) => post<StandingOrder>(`/standing-orders/${id}/cancel`),
}

export const beneficiariesApi = {
  list: () => get<Beneficiary[]>('/me/beneficiaries'),
  create: (request: BeneficiaryRequest) => post<Beneficiary>('/me/beneficiaries', request),
  update: (id: number, request: BeneficiaryRequest) => put<Beneficiary>(`/me/beneficiaries/${id}`, request),
  remove: (id: number) => del(`/me/beneficiaries/${id}`),
}

export const notificationsApi = {
  list: (unreadOnly = false, page = 1, pageSize = 20) =>
    get<PagedResponse<AppNotification>>('/me/notifications', { unreadOnly, page, pageSize }),
  unreadCount: () => get<{ count: number }>('/me/notifications/unread-count'),
  markRead: (id: number) => post<void>(`/me/notifications/${id}/read`),
  markAllRead: () => post<{ count: number }>('/me/notifications/read-all'),
}

export const securityApi = {
  sessions: () => get<Session[]>('/me/security/sessions'),
  revokeSession: (id: string) => del(`/me/security/sessions/${id}`),
  revokeOthers: () => post<{ revoked: number }>('/me/security/sessions/revoke-others'),
  reauthenticate: (password: string) => post<void>('/me/security/reauthenticate', { password }),
  events: (page = 1, pageSize = 20) => get<PagedResponse<SecurityEvent>>('/me/security/events', { page, pageSize }),
  twoFactorStatus: () => get<TwoFactorStatus>('/me/security/two-factor'),
  beginTwoFactorSetup: () => post<{ secret: string; otpAuthUri: string }>('/me/security/two-factor/setup'),
  enableTwoFactor: (code: string) => post<{ codes: string[] }>('/me/security/two-factor/enable', { code }),
  disableTwoFactor: (code: string) => post<void>('/me/security/two-factor/disable', { code }),
  regenerateRecoveryCodes: (code: string) => post<{ codes: string[] }>('/me/security/two-factor/recovery-codes', { code }),
}

export const privacyApi = {
  preferences: () => get<Preferences>('/me/preferences'),
  updatePreferences: (preferences: Omit<Preferences, 'notifySecurityEvents'>) => put<Preferences>('/me/preferences', preferences),
  consentHistory: () => get<ConsentEvent[]>('/me/preferences/consent-history'),
  consents: () => get<ConsentRecord[]>('/me/privacy/consents'),
  pendingDocuments: () => get<LegalDocument[]>('/me/privacy/pending-documents'),
  acceptDocuments: (documentIds: number[]) => post<void>('/me/privacy/consents', { documentIds }),
  dataAccess: (page = 1, pageSize = 20) => get<PagedResponse<DataAccessEntry>>('/me/privacy/data-access', { page, pageSize }),
  requests: () => get<PrivacyRequest[]>('/me/privacy/requests'),
  submitRequest: (requestType: PrivacyRequestType, details: string) =>
    post<PrivacyRequest>('/me/privacy/requests', { requestType, details }),
  exportData: () => get<Record<string, unknown>>('/me/privacy/data-export'),
}

export const complaintsApi = {
  mine: (page = 1, pageSize = 20) => get<PagedResponse<ComplaintSummary>>('/me/complaints', { page, pageSize }),
  mineDetail: (id: number) => get<ComplaintDetail>(`/me/complaints/${id}`),
  submit: (request: {
    category: ComplaintCategory
    subject: string
    description: string
    relatedTransactionId?: number | null
    relatedAccountId?: number | null
  }) => post<ComplaintDetail>('/me/complaints', request),
  reply: (id: number, message: string) => post<ComplaintDetail>(`/me/complaints/${id}/messages`, { message }),
  queue: (filters: { status?: ComplaintStatus; onlyOverdue?: boolean; onlyUnassigned?: boolean; page?: number }) =>
    get<PagedResponse<ComplaintSummary>>('/complaints', filters),
  staffDetail: (id: number) => get<ComplaintDetail>(`/complaints/${id}`),
  assign: (id: number, assigneeUserId: number | null) => post<ComplaintDetail>(`/complaints/${id}/assign`, { assigneeUserId }),
  changeStatus: (id: number, status: ComplaintStatus, note: string | null) =>
    post<ComplaintDetail>(`/complaints/${id}/status`, { status, note }),
  extendDeadline: (id: number, reason: string) => post<ComplaintDetail>(`/complaints/${id}/extend-deadline`, { reason }),
  staffMessage: (id: number, message: string, isInternalNote: boolean) =>
    post<ComplaintDetail>(`/complaints/${id}/messages`, { message, isInternalNote }),
}

export const feedbackApi = {
  submit: (surveyType: SurveyType, score: number, comment: string | null, context: string) =>
    post<FeedbackEntry>('/me/feedback', { surveyType, score, comment, context }),
  summary: (days = 90) => get<FeedbackSummary>('/admin/feedback/summary', { days }),
  recent: (type?: SurveyType, page = 1) => get<PagedResponse<FeedbackEntry>>('/admin/feedback', { type, page }),
}

export const helpApi = {
  faq: () => get<FaqArticle[]>('/help/faq'),
  contact: () => get<SupportContact>('/help/contact'),
  fees: () => get<Fee[]>('/help/fees'),
  legalDocuments: () => get<LegalDocument[]>('/legal/documents'),
  referenceData: () => get<ReferenceData>('/reference-data'),
}

export const usersApi = {
  list: (filters: { role?: UserRole; isActive?: boolean; search?: string; page?: number; pageSize?: number }) =>
    get<PagedResponse<User>>('/users', filters),
  get: (id: number) => get<User>(`/users/${id}`),
  accounts: (id: number) => get<Account[]>(`/users/${id}/accounts`),
  create: (request: { firstName: string; lastName: string; email: string; password: string; confirmPassword: string; role: UserRole }) =>
    post<User>('/users', request),
  update: (id: number, request: { firstName: string; lastName: string; email: string; role: UserRole }) =>
    put<User>(`/users/${id}`, request),
  deactivate: (id: number) => post<User>(`/users/${id}/deactivate`),
  activate: (id: number) => post<User>(`/users/${id}/activate`),
  unlock: (id: number) => post<User>(`/users/${id}/unlock`),
  remove: (id: number) => del(`/users/${id}`),
  updateKyc: (id: number, status: KycStatus, riskRating: RiskLevel) => post<User>(`/users/${id}/kyc`, { status, riskRating }),
  resetTwoFactor: (id: number) => post<void>(`/users/${id}/two-factor/reset`),
  standingOrders: (id: number) => get<StandingOrder[]>(`/users/${id}/standing-orders`),
}

export const transactionsApi = {
  list: (filters: TransactionFilters & { page?: number; pageSize?: number }) => get<PagedResponse<Transaction>>('/transactions', filters),
}

export const adminApi = {
  overview: () => get<SystemOverview>('/admin/overview'),
  auditLogs: (filters: { onlyFailures?: boolean; search?: string; from?: string; to?: string; page?: number }) =>
    get<PagedResponse<AuditLogEntry>>('/admin/audit-logs', filters),
  trialBalance: () => get<TrialBalanceLine[]>('/admin/ledger/trial-balance'),
  ledgerAccounts: () => get<LedgerAccountBalance[]>('/admin/ledger/accounts'),
  reconciliation: (onlyDifferences = false) => get<ReconciliationLine[]>('/admin/ledger/reconciliation', { onlyDifferences }),
  alerts: (status?: ComplianceAlertStatus, page = 1) => get<PagedResponse<ComplianceAlert>>('/admin/compliance/alerts', { status, page }),
  reviewAlert: (id: number, status: ComplianceAlertStatus, note: string) =>
    post<ComplianceAlert>(`/admin/compliance/alerts/${id}/review`, { status, note }),
  privacyRequests: (status?: PrivacyRequestStatus, page = 1) =>
    get<PagedResponse<PrivacyRequest>>('/admin/privacy-requests', { status, page }),
  respondPrivacyRequest: (id: number, status: PrivacyRequestStatus, response: string) =>
    post<PrivacyRequest>(`/admin/privacy-requests/${id}/respond`, { status, response }),
  faq: () => get<FaqArticle[]>('/admin/faq'),
  saveFaq: (article: Omit<FaqArticle, 'id'>, id?: number) =>
    id ? put<FaqArticle>(`/admin/faq/${id}`, article) : post<FaqArticle>('/admin/faq', article),
  deleteFaq: (id: number) => del(`/admin/faq/${id}`),
  passwordResets: (status?: PasswordResetStatus, page = 1) =>
    get<PagedResponse<PasswordResetRequest>>('/admin/password-resets', { status, page }),
  pendingPasswordResets: () => get<{ count: number }>('/admin/password-resets/pending-count'),
  approvePasswordReset: (id: number, note: string | null) => post<PasswordResetRequest>(`/admin/password-resets/${id}/approve`, { note }),
  rejectPasswordReset: (id: number, note: string) => post<PasswordResetRequest>(`/admin/password-resets/${id}/reject`, { note }),
  runRetention: () => post<{ readNotificationsDeleted: number; expiredSessionsDeleted: number }>('/admin/retention/run'),
}
