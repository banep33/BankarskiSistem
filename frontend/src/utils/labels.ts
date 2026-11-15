import type {
  AccountStatus,
  ComplaintCategory,
  ComplaintStatus,
  ComplianceAlertStatus,
  KycStatus,
  NotificationType,
  PasswordResetStatus,
  ReversalStatus,
  StandingOrderExecutionStatus,
  StandingOrderFrequency,
  StandingOrderStatus,
  PaymentOrderStatus,
  PrivacyRequestStatus,
  PrivacyRequestType,
  RiskLevel,
  Transaction,
  TransactionType,
  UserRole,
} from '@/types/api'

export type Tone = 'neutral' | 'success' | 'danger' | 'warning' | 'info' | 'brand'

export const roleLabels: Record<UserRole, string> = {
  Administrator: 'Administrator',
  Bankar: 'Bankar',
  Korisnik: 'Klijent',
}

export const accountStatusLabels: Record<AccountStatus, { label: string; tone: Tone }> = {
  Aktivan: { label: 'Aktivan', tone: 'success' },
  Neaktivan: { label: 'Neaktivan', tone: 'neutral' },
  Blokiran: { label: 'Blokiran', tone: 'danger' },
}

export const transactionTypeLabels: Record<TransactionType, string> = {
  Uplata: 'Uplata',
  Isplata: 'Isplata',
  Transfer: 'Plaćanje u banci',
  EksterniTransfer: 'Plaćanje drugoj banci',
  Konverzija: 'Menjačnica',
  Storno: 'Storno',
}

export const paymentStatusLabels: Record<PaymentOrderStatus, { label: string; tone: Tone }> = {
  Zakazan: { label: 'Zakazan', tone: 'info' },
  UObradi: { label: 'Poslat u kliring', tone: 'warning' },
  Izvrsen: { label: 'Izvršen', tone: 'success' },
  Odbijen: { label: 'Odbijen', tone: 'danger' },
  Otkazan: { label: 'Otkazan', tone: 'neutral' },
}

export const kycLabels: Record<KycStatus, { label: string; tone: Tone }> = {
  NijePrimenljivo: { label: 'Nije primenljivo', tone: 'neutral' },
  NijeVerifikovan: { label: 'Nije verifikovan', tone: 'warning' },
  Verifikovan: { label: 'Verifikovan', tone: 'success' },
  Odbijen: { label: 'Odbijen', tone: 'danger' },
}

export const riskLabels: Record<RiskLevel, { label: string; tone: Tone }> = {
  Nizak: { label: 'Nizak rizik', tone: 'success' },
  Srednji: { label: 'Srednji rizik', tone: 'warning' },
  Visok: { label: 'Visok rizik', tone: 'danger' },
}

export const complaintCategoryLabels: Record<ComplaintCategory, string> = {
  PlatniPromet: 'Platni promet',
  Racun: 'Račun',
  Naknade: 'Naknade i troškovi',
  Kartice: 'Kartice',
  ElektronskoBankarstvo: 'Elektronsko bankarstvo',
  ZastitaPodataka: 'Zaštita podataka',
  Ostalo: 'Ostalo',
}

export const complaintStatusLabels: Record<ComplaintStatus, { label: string; tone: Tone }> = {
  Podnet: { label: 'Podnet', tone: 'info' },
  UObradi: { label: 'U obradi', tone: 'warning' },
  CekaKorisnika: { label: 'Čeka vaš odgovor', tone: 'brand' },
  Resen: { label: 'Rešen', tone: 'success' },
  Odbijen: { label: 'Odbijen', tone: 'danger' },
  Zatvoren: { label: 'Zatvoren', tone: 'neutral' },
}

export const privacyRequestTypeLabels: Record<PrivacyRequestType, string> = {
  Pristup: 'Pristup podacima',
  Ispravka: 'Ispravka podataka',
  Brisanje: 'Brisanje podataka',
  Ogranicenje: 'Ograničenje obrade',
  Prigovor: 'Prigovor na obradu',
}

export const privacyRequestStatusLabels: Record<PrivacyRequestStatus, { label: string; tone: Tone }> = {
  Podnet: { label: 'Podnet', tone: 'info' },
  UObradi: { label: 'U obradi', tone: 'warning' },
  Usvojen: { label: 'Usvojen', tone: 'success' },
  DelimicnoUsvojen: { label: 'Delimično usvojen', tone: 'success' },
  Odbijen: { label: 'Odbijen', tone: 'danger' },
}

export const reversalStatusLabels: Record<ReversalStatus, { label: string; tone: Tone }> = {
  Podnet: { label: 'Čeka odobrenje', tone: 'warning' },
  Izvrsen: { label: 'Stornirano', tone: 'success' },
  Odbijen: { label: 'Odbijeno', tone: 'danger' },
}

export const frequencyLabels: Record<StandingOrderFrequency, { label: string; adverb: string }> = {
  Nedeljno: { label: 'Nedeljno', adverb: 'svake nedelje' },
  Mesecno: { label: 'Mesečno', adverb: 'svakog meseca' },
  Kvartalno: { label: 'Kvartalno', adverb: 'svaka tri meseca' },
  Godisnje: { label: 'Godišnje', adverb: 'svake godine' },
}

export const standingOrderStatusLabels: Record<StandingOrderStatus, { label: string; tone: Tone }> = {
  Aktivan: { label: 'Aktivan', tone: 'success' },
  Pauziran: { label: 'Pauziran', tone: 'warning' },
  Zavrsen: { label: 'Završen', tone: 'neutral' },
  Otkazan: { label: 'Otkazan', tone: 'neutral' },
}

export const executionStatusLabels: Record<StandingOrderExecutionStatus, { label: string; tone: Tone }> = {
  Pokrenut: { label: 'U obradi', tone: 'info' },
  Izvrsen: { label: 'Izvršen', tone: 'success' },
  Odbijen: { label: 'Nije izvršen', tone: 'danger' },
}

export const passwordResetStatusLabels: Record<PasswordResetStatus, { label: string; tone: Tone }> = {
  Podnet: { label: 'Čeka odobrenje', tone: 'warning' },
  Odobren: { label: 'Odobren', tone: 'success' },
  Odbijen: { label: 'Odbijen', tone: 'danger' },
  Istekao: { label: 'Istekao', tone: 'neutral' },
}

export const alertStatusLabels: Record<ComplianceAlertStatus, { label: string; tone: Tone }> = {
  Otvoren: { label: 'Otvoren', tone: 'danger' },
  UProveri: { label: 'U proveri', tone: 'warning' },
  ZatvorenBezNalaza: { label: 'Zatvoren bez nalaza', tone: 'success' },
  EskaliranSimulacija: { label: 'Eskaliran (simulacija)', tone: 'brand' },
}

export const alertRuleLabels: Record<string, string> = {
  GOTOVINA_PREKO_PRAGA: 'Gotovina preko praga za izveštavanje',
  STRUKTURIRANJE_GOTOVINE: 'Moguće strukturiranje gotovine',
  BRZE_UZASTOPNE_ISPLATE: 'Brze uzastopne isplate',
  NOVI_PRIMALAC_VELIKI_IZNOS: 'Veliki iznos novom primaocu',
  VISOKORIZICAN_KLIJENT: 'Visokorizičan klijent',
}

export const notificationIcons: Record<NotificationType, string> = {
  Uplata: 'arrow-down',
  Isplata: 'arrow-up',
  PrimljenTransfer: 'arrow-down',
  PoslatTransfer: 'send',
  StatusRacuna: 'wallet',
  NoviRacun: 'wallet',
  Bezbednost: 'shield',
  Prigovor: 'chat',
  Privatnost: 'lock',
  TrajniNalog: 'repeat',
  Konverzija: 'exchange',
}

export const securityActionLabels: Record<string, string> = {
  'Uspesna prijava': 'Uspešna prijava',
  'Neuspesna prijava': 'Neuspešna prijava',
  'Promena lozinke': 'Promena lozinke',
  'Izmenjen profil': 'Izmena profila',
  'Izvoz licnih podataka': 'Izvoz ličnih podataka',
  'Prihvaceni pravni dokumenti': 'Prihvatanje pravnih dokumenata',
  'Zahtev za novu lozinku': 'Zahtev za novu lozinku',
}

export function isIncoming(transaction: Transaction, ownAccountIds: Set<number>): boolean {
  if (transaction.type === 'Uplata') return true
  if (transaction.type === 'Isplata' || transaction.type === 'EksterniTransfer') return false
  const fromOwn = transaction.fromAccountId !== null && ownAccountIds.has(transaction.fromAccountId)
  const toOwn = transaction.toAccountId !== null && ownAccountIds.has(transaction.toAccountId)
  return toOwn && !fromOwn
}

export function counterpartyLabel(transaction: Transaction, incoming: boolean): string {
  if (transaction.type === 'Uplata') return 'Uplata na šalteru'
  if (transaction.type === 'Isplata') return 'Isplata sa računa'
  if (transaction.type === 'Storno') return incoming ? 'Storno, povraćaj na račun' : 'Storno, povlačenje sa računa'
  if (transaction.type === 'Konverzija') return incoming ? 'Menjačnica, priliv' : 'Menjačnica, odliv'
  if (!incoming && transaction.counterpartyName) return transaction.counterpartyName
  if (incoming) return transaction.fromAccountNumber ? `Sa računa ${transaction.fromAccountNumber}` : 'Primljena uplata'
  return transaction.counterpartyAccountNumber ?? 'Plaćanje'
}
