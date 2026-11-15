export type UserRole = 'Administrator' | 'Korisnik' | 'Bankar'
export type AccountStatus = 'Aktivan' | 'Neaktivan' | 'Blokiran'
export type TransactionType = 'Uplata' | 'Isplata' | 'Transfer' | 'EksterniTransfer'
export type TransactionStatus = 'Uspesna' | 'Neuspesna' | 'NaCekanju'
export type PaymentOrderStatus = 'Zakazan' | 'UObradi' | 'Izvrsen' | 'Odbijen' | 'Otkazan'
export type KycStatus = 'NijePrimenljivo' | 'NijeVerifikovan' | 'Verifikovan' | 'Odbijen'
export type RiskLevel = 'Nizak' | 'Srednji' | 'Visok'
export type NotificationType =
  | 'Uplata'
  | 'Isplata'
  | 'PrimljenTransfer'
  | 'PoslatTransfer'
  | 'StatusRacuna'
  | 'NoviRacun'
  | 'Bezbednost'
  | 'Prigovor'
  | 'Privatnost'
export type ComplaintCategory =
  | 'PlatniPromet'
  | 'Racun'
  | 'Naknade'
  | 'Kartice'
  | 'ElektronskoBankarstvo'
  | 'ZastitaPodataka'
  | 'Ostalo'
export type ComplaintStatus = 'Podnet' | 'UObradi' | 'CekaKorisnika' | 'Resen' | 'Odbijen' | 'Zatvoren'
export type SurveyType = 'CSAT' | 'NPS' | 'CES'
export type PrivacyRequestType = 'Ispravka' | 'Brisanje' | 'Ogranicenje' | 'Prigovor' | 'Pristup'
export type PrivacyRequestStatus = 'Podnet' | 'UObradi' | 'Usvojen' | 'DelimicnoUsvojen' | 'Odbijen'
export type ComplianceAlertStatus = 'Otvoren' | 'UProveri' | 'ZatvorenBezNalaza' | 'EskaliranSimulacija'
export type StatisticsPeriod = 'Week' | 'Month' | 'Year'
export type LegalDocumentType = 'PolitikaPrivatnosti' | 'UsloviKoriscenja'

export interface ApiProblem {
  type: string
  title: string
  status: number
  detail: string
  instance: string
  traceId: string
  errors: string[]
}

export interface PagedResponse<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
  hasPrevious: boolean
  hasNext: boolean
}

export interface CursorResponse<T> {
  items: T[]
  nextCursor: string | null
  hasMore: boolean
}

export interface LegalDocumentSummary {
  id: number
  documentType: LegalDocumentType
  version: string
  title: string
  publishedAt: string
}

export interface LegalDocument extends LegalDocumentSummary {
  content: string
}

export interface LoginResponse {
  id: number
  token: string
  email: string
  firstName: string
  lastName: string
  role: UserRole
  expiresAt: string
  pendingDocuments: LegalDocumentSummary[]
}

export interface User {
  id: number
  firstName: string
  lastName: string
  email: string
  role: UserRole
  isActive: boolean
  isLocked: boolean
  lockedUntil: string | null
  failedLoginAttempts: number
  kycStatus: KycStatus
  riskRating: RiskLevel
  kycVerifiedAt: string | null
  createdAt: string
  updatedAt: string
}

export interface Profile {
  user: User
  pendingDocuments: LegalDocumentSummary[]
}

export interface Account {
  id: number
  userId: number
  accountNumber: string
  balance: number
  currency: string
  status: AccountStatus
  createdAt: string
  updatedAt: string
  ownerFullName: string | null
  ownerEmail: string | null
}

export interface Transaction {
  id: number
  referenceNumber: string
  fromAccountId: number | null
  fromAccountNumber: string | null
  toAccountId: number | null
  toAccountNumber: string | null
  amount: number
  feeAmount: number
  currency: string
  type: TransactionType
  status: TransactionStatus
  description: string | null
  performedBy: string | null
  createdAt: string
  fromBalanceAfter: number | null
  toBalanceAfter: number | null
  paymentOrderId: number | null
  counterpartyName: string | null
  counterpartyAccountNumber: string | null
  paymentCode: string | null
  paymentReferenceModel: string | null
  paymentReferenceNumber: string | null
}

export interface CurrencyAmount {
  currency: string
  amount: number
}

export interface CashFlowSummary {
  currency: string
  income: number
  expense: number
}

export interface CustomerDashboard {
  balancesByCurrency: CurrencyAmount[]
  monthCashFlow: CashFlowSummary[]
  activeAccounts: number
  totalAccounts: number
  unreadNotifications: number
  recentTransactions: Transaction[]
}

export interface CashFlowBucket {
  periodStart: string
  income: number
  expense: number
  count: number
}

export interface AccountStatistics {
  accountId: number
  currency: string
  period: StatisticsPeriod
  totalIncome: number
  totalExpense: number
  buckets: CashFlowBucket[]
}

export interface PaymentRequest {
  fromAccountId: number
  recipientName: string
  recipientAccountNumber: string
  recipientAddress?: string | null
  amount: number
  paymentCode: string
  purpose: string
  referenceModel?: string | null
  referenceNumber?: string | null
  scheduledFor?: string | null
  saveAsBeneficiary?: string | null
}

export interface PaymentQuote {
  amount: number
  fee: number
  totalDebit: number
  currency: string
  balanceBefore: number
  balanceAfter: number
  isInternal: boolean
  isKnownRecipient: boolean
  isScheduled: boolean
  feeName: string | null
  feeDescription: string | null
  requiresStrongConfirmation: boolean
  remainingDailyLimit: number
  warnings: string[]
}

export interface PaymentOrder {
  id: number
  fromAccountId: number
  fromAccountNumber: string | null
  recipientName: string
  recipientAccountNumber: string
  recipientAddress: string | null
  isInternal: boolean
  amount: number
  feeAmount: number
  totalDebit: number
  currency: string
  paymentCode: string
  purpose: string
  referenceModel: string | null
  referenceNumber: string | null
  status: PaymentOrderStatus
  scheduledFor: string | null
  executedAt: string | null
  rejectionReason: string | null
  transactionReference: string | null
  balanceAfter: number | null
  createdAt: string
  isReplay: boolean
}

export interface Beneficiary {
  id: number
  nickname: string
  recipientName: string
  accountNumber: string
  recipientAddress: string | null
  paymentCode: string | null
  purpose: string | null
  referenceModel: string | null
  referenceNumber: string | null
  lastUsedAt: string | null
}

export type BeneficiaryRequest = Omit<Beneficiary, 'id' | 'lastUsedAt'>

export interface AppNotification {
  id: number
  type: NotificationType
  title: string
  message: string
  isRead: boolean
  transactionId: number | null
  createdAt: string
}

export interface FinancialOperationResponse {
  message: string
  transactionReference: string
  transactionId: number
  amount: number
  currency: string
  balanceAfter: number
  executedAt: string
  isReplay: boolean
}

export interface Session {
  id: string
  deviceLabel: string | null
  ipAddress: string | null
  createdAt: string
  lastSeenAt: string
  expiresAt: string
  isCurrent: boolean
}

export interface SecurityEvent {
  occurredAt: string
  action: string
  isSuccess: boolean
  ipAddress: string | null
}

export interface Preferences {
  notifyIncomingPayments: boolean
  notifyOutgoingPayments: boolean
  notifyAccountChanges: boolean
  notifyComplaintUpdates: boolean
  notifySecurityEvents: boolean
  marketingConsent: boolean
  hideBalancesByDefault: boolean
  extraConfirmationThreshold: number | null
}

export interface ConsentRecord {
  legalDocumentId: number
  documentType: LegalDocumentType
  version: string
  title: string
  acceptedAt: string
}

export interface ConsentEvent {
  consentType: string
  granted: boolean
  source: string
  createdAt: string
}

export interface DataAccessEntry {
  accessedAt: string
  viewerRole: string
  viewerLabel: string
  resource: string
  purpose: string
}

export interface PrivacyRequest {
  id: number
  referenceNumber: string
  requestType: PrivacyRequestType
  details: string
  status: PrivacyRequestStatus
  dueAt: string
  response: string | null
  respondedAt: string | null
  createdAt: string
  customerEmail: string | null
}

export interface ComplaintSummary {
  id: number
  referenceNumber: string
  category: ComplaintCategory
  subject: string
  status: ComplaintStatus
  priority: 'Normalan' | 'Visok'
  dueAt: string
  isOverdue: boolean
  deadlineExtended: boolean
  customerName: string | null
  assignedTo: string | null
  createdAt: string
  updatedAt: string
}

export interface ComplaintMessage {
  id: number
  isFromStaff: boolean
  isInternalNote: boolean
  authorName: string
  message: string
  createdAt: string
}

export interface ComplaintDetail {
  summary: ComplaintSummary
  description: string
  relatedTransactionId: number | null
  relatedAccountId: number | null
  resolution: string | null
  resolvedAt: string | null
  deadlineExtensionReason: string | null
  messages: ComplaintMessage[]
  history: { oldStatus: string | null; newStatus: string; note: string | null; changedAt: string }[]
}

export interface Fee {
  code: string
  name: string
  description: string
  appliesTo: string
  calculationType: 'Fiksna' | 'Procenat'
  amount: number
  minAmount: number | null
  maxAmount: number | null
  currency: string
  validFrom: string
  humanReadable: string
}

export interface FaqArticle {
  id: number
  category: string
  question: string
  answer: string
  sortOrder: number
  isPublished: boolean
}

export interface SupportContact {
  phone: string
  email: string
  workingHours: string
  regulatorInfo: string
}

export interface ReferenceData {
  paymentCodes: { code: string; description: string }[]
  referenceModels: string[]
  currencies: string[]
  bankCode: string
}

export interface SystemOverview {
  totalUsers: number
  customers: number
  bankers: number
  administrators: number
  activeUsers: number
  lockedUsers: number
  totalAccounts: number
  activeAccounts: number
  blockedAccounts: number
  depositsByCurrency: CurrencyAmount[]
  transactionsToday: number
  volumeTodayByCurrency: CurrencyAmount[]
  failedLoginsLast24Hours: number
  activityLast7Days: { day: string; deposits: number; withdrawals: number; transfers: number }[]
}

export interface AuditLogEntry {
  id: number
  userId: number | null
  userEmail: string | null
  action: string
  entityType: string | null
  entityId: number | null
  ipAddress: string | null
  oldValue: string | null
  newValue: string | null
  isSuccess: boolean
  errorMessage: string | null
  createdAt: string
}

export interface ComplianceAlert {
  id: number
  ruleCode: string
  severity: RiskLevel
  userId: number
  customerEmail: string | null
  accountId: number | null
  transactionId: number | null
  details: string
  status: ComplianceAlertStatus
  reviewNote: string | null
  createdAt: string
  reviewedAt: string | null
  isSimulation: boolean
}

export interface SurveyScore {
  surveyType: SurveyType
  responses: number
  score: number | null
  interpretation: string
}

export interface FeedbackSummary {
  scores: SurveyScore[]
  totalResponses: number
}

export interface FeedbackEntry {
  id: number
  surveyType: SurveyType
  score: number
  comment: string | null
  context: string
  createdAt: string
}

export interface TrialBalanceLine {
  currency: string
  total: number
  postingCount: number
}

export interface LedgerAccountBalance {
  ledgerAccountId: number
  code: string
  name: string
  type: string
  currency: string
  balance: number
}

export interface ReconciliationLine {
  accountId: number
  accountNumber: string
  currency: string
  storedBalance: number
  ledgerBalance: number
  difference: number
}
