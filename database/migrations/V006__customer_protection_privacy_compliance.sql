CREATE TABLE user_sessions (
  Id char(36) NOT NULL,
  UserId int NOT NULL,
  CreatedAt datetime NOT NULL,
  LastSeenAt datetime NOT NULL,
  ExpiresAt datetime NOT NULL,
  ReauthenticatedAt datetime NULL,
  RevokedAt datetime NULL,
  RevokedReason varchar(60) NULL,
  IpAddress varchar(45) NULL,
  UserAgent varchar(255) NULL,
  DeviceLabel varchar(100) NULL,
  PRIMARY KEY (Id),
  KEY idx_user_sessions_user_active (UserId, RevokedAt, ExpiresAt),
  KEY idx_user_sessions_expires (ExpiresAt),
  CONSTRAINT fk_user_sessions_user FOREIGN KEY (UserId) REFERENCES users (Id) ON DELETE CASCADE,
  CONSTRAINT chk_user_sessions_expiry CHECK (ExpiresAt > CreatedAt)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE user_preferences (
  UserId int NOT NULL,
  NotifyIncomingPayments tinyint(1) NOT NULL DEFAULT 1,
  NotifyOutgoingPayments tinyint(1) NOT NULL DEFAULT 1,
  NotifyAccountChanges tinyint(1) NOT NULL DEFAULT 1,
  NotifyComplaintUpdates tinyint(1) NOT NULL DEFAULT 1,
  MarketingConsent tinyint(1) NOT NULL DEFAULT 0,
  HideBalancesByDefault tinyint(1) NOT NULL DEFAULT 0,
  ExtraConfirmationThreshold decimal(18,2) NULL,
  UpdatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (UserId),
  CONSTRAINT fk_user_preferences_user FOREIGN KEY (UserId) REFERENCES users (Id) ON DELETE CASCADE,
  CONSTRAINT chk_user_preferences_threshold CHECK (ExtraConfirmationThreshold IS NULL OR ExtraConfirmationThreshold > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE consent_events (
  Id bigint NOT NULL AUTO_INCREMENT,
  UserId int NOT NULL,
  ConsentType varchar(60) NOT NULL,
  Granted tinyint(1) NOT NULL,
  Source varchar(40) NOT NULL,
  IpAddress varchar(45) NULL,
  CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  KEY idx_consent_events_user_created (UserId, CreatedAt),
  CONSTRAINT fk_consent_events_user FOREIGN KEY (UserId) REFERENCES users (Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO consent_events (UserId, ConsentType, Granted, Source, IpAddress, CreatedAt)
SELECT c.UserId, CONCAT(d.DocumentType, ':', d.Version), 1, 'Migracija', c.IpAddress, c.AcceptedAt
FROM user_consents c
JOIN legal_documents d ON d.Id = c.LegalDocumentId;

CREATE TABLE fee_schedule (
  Id int NOT NULL AUTO_INCREMENT,
  Code varchar(40) NOT NULL,
  Name varchar(120) NOT NULL,
  Description varchar(500) NOT NULL,
  AppliesTo enum('InterniNalog','EksterniNalog','Isplata','Uplata') NOT NULL,
  CalculationType enum('Fiksna','Procenat') NOT NULL,
  Amount decimal(18,4) NOT NULL,
  MinAmount decimal(18,2) NULL,
  MaxAmount decimal(18,2) NULL,
  Currency char(3) NOT NULL,
  ValidFrom date NOT NULL,
  ValidTo date NULL,
  CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  UNIQUE KEY ux_fee_schedule_code_valid_from (Code, ValidFrom),
  KEY idx_fee_schedule_applies (AppliesTo, Currency, ValidFrom),
  CONSTRAINT chk_fee_schedule_amount CHECK (Amount >= 0),
  CONSTRAINT chk_fee_schedule_bounds CHECK (MinAmount IS NULL OR MaxAmount IS NULL OR MinAmount <= MaxAmount),
  CONSTRAINT chk_fee_schedule_validity CHECK (ValidTo IS NULL OR ValidTo >= ValidFrom),
  CONSTRAINT chk_fee_schedule_currency CHECK (Currency REGEXP '^[A-Z]{3}$')
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO fee_schedule (Code, Name, Description, AppliesTo, CalculationType, Amount, MinAmount, MaxAmount, Currency, ValidFrom) VALUES
('NALOG-INTERNI', 'Plaćanje u okviru banke', 'Prenos na račun u našoj banci, uključujući prenos između sopstvenih računa.', 'InterniNalog', 'Fiksna', 0, NULL, NULL, 'RSD', '2026-01-01'),
('NALOG-EKSTERNI', 'Plaćanje ka drugoj banci', 'Dinarski nalog ka računu u drugoj banci u zemlji, putem elektronskog bankarstva.', 'EksterniNalog', 'Procenat', 0.1000, 40.00, 300.00, 'RSD', '2026-01-01'),
('ISPLATA', 'Isplata sa računa', 'Isplata sredstava sa sopstvenog računa.', 'Isplata', 'Fiksna', 0, NULL, NULL, 'RSD', '2026-01-01'),
('UPLATA', 'Uplata na račun', 'Uplata na račun na šalteru banke.', 'Uplata', 'Fiksna', 0, NULL, NULL, 'RSD', '2026-01-01');

ALTER TABLE payment_orders
  ADD COLUMN FeeAmount decimal(18,2) NOT NULL DEFAULT 0 AFTER Currency,
  ADD COLUMN FeeScheduleId int NULL AFTER FeeAmount,
  ADD CONSTRAINT fk_payment_orders_fee FOREIGN KEY (FeeScheduleId) REFERENCES fee_schedule (Id),
  ADD CONSTRAINT chk_payment_orders_fee CHECK (FeeAmount >= 0);

ALTER TABLE transactions
  ADD COLUMN FeeAmount decimal(18,2) NOT NULL DEFAULT 0 AFTER Amount,
  ADD CONSTRAINT chk_transactions_fee CHECK (FeeAmount >= 0);

DROP TRIGGER IF EXISTS trg_transactions_protect_ledger;

DELIMITER $$

CREATE TRIGGER trg_transactions_protect_ledger
BEFORE UPDATE ON transactions
FOR EACH ROW
BEGIN
  IF NOT (
    OLD.Amount <=> NEW.Amount
    AND OLD.FeeAmount <=> NEW.FeeAmount
    AND OLD.Currency <=> NEW.Currency
    AND OLD.FromAccountId <=> NEW.FromAccountId
    AND OLD.ToAccountId <=> NEW.ToAccountId
    AND OLD.Type <=> NEW.Type
    AND OLD.ReferenceNumber <=> NEW.ReferenceNumber
    AND OLD.FromBalanceAfter <=> NEW.FromBalanceAfter
    AND OLD.ToBalanceAfter <=> NEW.ToBalanceAfter
    AND OLD.CreatedAt <=> NEW.CreatedAt
  ) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Finansijski podaci transakcije se ne mogu menjati.';
  END IF;
END$$

DELIMITER ;

ALTER TABLE users
  ADD COLUMN KycStatus enum('NijePrimenljivo','NijeVerifikovan','Verifikovan','Odbijen') NOT NULL DEFAULT 'NijeVerifikovan' AFTER TokenVersion,
  ADD COLUMN KycVerifiedAt datetime NULL AFTER KycStatus,
  ADD COLUMN KycVerifiedByUserId int NULL AFTER KycVerifiedAt,
  ADD COLUMN RiskRating enum('Nizak','Srednji','Visok') NOT NULL DEFAULT 'Nizak' AFTER KycVerifiedByUserId,
  ADD CONSTRAINT fk_users_kyc_verified_by FOREIGN KEY (KycVerifiedByUserId) REFERENCES users (Id) ON DELETE SET NULL;

UPDATE users SET KycStatus = 'NijePrimenljivo' WHERE Role IN ('Administrator', 'Bankar');

CREATE TABLE complaints (
  Id int NOT NULL AUTO_INCREMENT,
  ReferenceNumber varchar(20) NOT NULL,
  UserId int NOT NULL,
  Category enum('PlatniPromet','Racun','Naknade','Kartice','ElektronskoBankarstvo','ZastitaPodataka','Ostalo') NOT NULL,
  Subject varchar(150) NOT NULL,
  Description text NOT NULL,
  RelatedTransactionId int NULL,
  RelatedAccountId int NULL,
  Status enum('Podnet','UObradi','CekaKorisnika','Resen','Odbijen','Zatvoren') NOT NULL DEFAULT 'Podnet',
  Priority enum('Normalan','Visok') NOT NULL DEFAULT 'Normalan',
  AssignedToUserId int NULL,
  DueAt datetime NOT NULL,
  DeadlineExtendedAt datetime NULL,
  DeadlineExtensionReason varchar(500) NULL,
  ResolvedAt datetime NULL,
  Resolution text NULL,
  CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UpdatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  UNIQUE KEY ux_complaints_reference (ReferenceNumber),
  KEY idx_complaints_user_created (UserId, CreatedAt),
  KEY idx_complaints_status_due (Status, DueAt),
  KEY idx_complaints_assigned (AssignedToUserId, Status),
  CONSTRAINT fk_complaints_user FOREIGN KEY (UserId) REFERENCES users (Id),
  CONSTRAINT fk_complaints_assigned FOREIGN KEY (AssignedToUserId) REFERENCES users (Id) ON DELETE SET NULL,
  CONSTRAINT fk_complaints_transaction FOREIGN KEY (RelatedTransactionId) REFERENCES transactions (Id),
  CONSTRAINT fk_complaints_account FOREIGN KEY (RelatedAccountId) REFERENCES accounts (Id),
  CONSTRAINT chk_complaints_due CHECK (DueAt > CreatedAt),
  CONSTRAINT chk_complaints_resolution CHECK ((Status IN ('Resen','Odbijen')) = (ResolvedAt IS NOT NULL) OR Status = 'Zatvoren')
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE complaint_messages (
  Id int NOT NULL AUTO_INCREMENT,
  ComplaintId int NOT NULL,
  AuthorUserId int NOT NULL,
  IsFromStaff tinyint(1) NOT NULL,
  IsInternalNote tinyint(1) NOT NULL DEFAULT 0,
  Message text NOT NULL,
  CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  KEY idx_complaint_messages_complaint (ComplaintId, CreatedAt),
  CONSTRAINT fk_complaint_messages_complaint FOREIGN KEY (ComplaintId) REFERENCES complaints (Id) ON DELETE CASCADE,
  CONSTRAINT fk_complaint_messages_author FOREIGN KEY (AuthorUserId) REFERENCES users (Id),
  CONSTRAINT chk_complaint_messages_internal CHECK (IsInternalNote = 0 OR IsFromStaff = 1)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE complaint_status_history (
  Id int NOT NULL AUTO_INCREMENT,
  ComplaintId int NOT NULL,
  OldStatus varchar(20) NULL,
  NewStatus varchar(20) NOT NULL,
  ChangedByUserId int NOT NULL,
  Note varchar(500) NULL,
  ChangedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  KEY idx_complaint_status_history_complaint (ComplaintId, ChangedAt),
  CONSTRAINT fk_complaint_status_history_complaint FOREIGN KEY (ComplaintId) REFERENCES complaints (Id) ON DELETE CASCADE,
  CONSTRAINT fk_complaint_status_history_user FOREIGN KEY (ChangedByUserId) REFERENCES users (Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE user_feedback (
  Id int NOT NULL AUTO_INCREMENT,
  UserId int NOT NULL,
  SurveyType enum('CSAT','NPS','CES') NOT NULL,
  Score tinyint NOT NULL,
  Comment varchar(1000) NULL,
  Context varchar(60) NOT NULL,
  CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  UNIQUE KEY ux_user_feedback_user_context (UserId, SurveyType, Context),
  KEY idx_user_feedback_type_created (SurveyType, CreatedAt),
  CONSTRAINT fk_user_feedback_user FOREIGN KEY (UserId) REFERENCES users (Id) ON DELETE CASCADE,
  CONSTRAINT chk_user_feedback_score CHECK (
    (SurveyType = 'CSAT' AND Score BETWEEN 1 AND 5)
    OR (SurveyType = 'NPS' AND Score BETWEEN 0 AND 10)
    OR (SurveyType = 'CES' AND Score BETWEEN 1 AND 7)
  )
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE faq_articles (
  Id int NOT NULL AUTO_INCREMENT,
  Category varchar(60) NOT NULL,
  Question varchar(255) NOT NULL,
  Answer text NOT NULL,
  SortOrder int NOT NULL DEFAULT 0,
  IsPublished tinyint(1) NOT NULL DEFAULT 1,
  UpdatedByUserId int NULL,
  UpdatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  KEY idx_faq_articles_category (IsPublished, Category, SortOrder),
  CONSTRAINT fk_faq_articles_user FOREIGN KEY (UpdatedByUserId) REFERENCES users (Id) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO faq_articles (Category, Question, Answer, SortOrder) VALUES
('Plaćanja', 'Koliko traje plaćanje drugom klijentu naše banke?', 'Plaćanje na račun u našoj banci izvršava se odmah i bez naknade. Primalac odmah vidi uplatu i dobija obaveštenje.', 10),
('Plaćanja', 'Koliko traje plaćanje ka drugoj banci i koliko košta?', 'Nalog ka drugoj banci odmah se skida sa vašeg računa i šalje u kliring. Naknada je navedena u cenovniku i prikazuje se pre potvrde naloga, zajedno sa ukupnim iznosom koji će biti skinut sa računa.', 20),
('Plaćanja', 'Šta je model i poziv na broj?', 'Poziv na broj služi primaocu da prepozna vašu uplatu (npr. broj računa za struju). Model 97 sadrži kontrolni broj, pa aplikacija odmah upozorava ako ste pogrešno prepisali poziv na broj.', 30),
('Plaćanja', 'Mogu li da otkažem nalog?', 'Zakazani nalog možete otkazati sve dok ne bude izvršen. Izvršeni nalog ne može se poništiti iz aplikacije; u tom slučaju podnesite prigovor.', 40),
('Bezbednost', 'Zašto je moj nalog zaključan?', 'Posle pet uzastopnih pogrešnih lozinki nalog se privremeno zaključava na 30 minuta radi zaštite. Ako to niste bili vi, odmah promenite lozinku.', 10),
('Bezbednost', 'Kako da odjavim druge uređaje?', 'U odeljku Bezbednost vidite sve aktivne sesije, uređaje i vreme poslednje aktivnosti. Svaku sesiju možete odjaviti pojedinačno ili sve ostale odjednom.', 20),
('Bezbednost', 'Da li će me banka ikada pitati za lozinku?', 'Ne. Banka nikada ne traži lozinku putem telefona, emaila ili poruke. Ako dobijete takav zahtev, ne odgovarajte i obavestite banku.', 30),
('Privatnost', 'Kako da preuzmem svoje podatke?', 'U odeljku Privatnost možete preuzeti kopiju svih svojih podataka. Iz bezbednosnih razloga pre preuzimanja potrebno je ponovo uneti lozinku.', 10),
('Privatnost', 'Ko je pristupao mojim podacima?', 'U odeljku Privatnost vidite evidenciju svakog pristupa vašim podacima od strane zaposlenih u banci, sa vremenom i razlogom pristupa.', 20),
('Privatnost', 'Mogu li da tražim brisanje podataka?', 'Možete podneti zahtev za brisanje. Podatke o transakcijama banka je zakonski obavezna da čuva propisani period, pa će vas odgovor obavestiti šta se može obrisati odmah, a šta po isteku roka.', 30),
('Prigovori', 'Kako da podnesem prigovor i u kom roku dobijam odgovor?', 'Prigovor podnosite u odeljku Prigovori. Dobijate broj predmeta i pratite status. Rok za odgovor prikazan je uz svaki predmet; ako je potrebno produženje, banka vas o tome obaveštava sa obrazloženjem.', 10),
('Prigovori', 'Šta ako nisam zadovoljan odgovorom?', 'Ako niste zadovoljni odgovorom ili ga niste dobili u roku, možete se obratiti Narodnoj banci Srbije, koja postupa po pritužbama korisnika finansijskih usluga.', 20);

CREATE TABLE data_access_logs (
  Id bigint NOT NULL AUTO_INCREMENT,
  ViewerUserId int NOT NULL,
  SubjectUserId int NOT NULL,
  Resource varchar(40) NOT NULL,
  ResourceId int NULL,
  Purpose varchar(120) NOT NULL,
  IpAddress varchar(45) NULL,
  AccessedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  KEY idx_data_access_logs_subject (SubjectUserId, AccessedAt),
  KEY idx_data_access_logs_viewer (ViewerUserId, AccessedAt),
  CONSTRAINT fk_data_access_logs_viewer FOREIGN KEY (ViewerUserId) REFERENCES users (Id),
  CONSTRAINT fk_data_access_logs_subject FOREIGN KEY (SubjectUserId) REFERENCES users (Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE privacy_requests (
  Id int NOT NULL AUTO_INCREMENT,
  ReferenceNumber varchar(20) NOT NULL,
  UserId int NOT NULL,
  RequestType enum('Ispravka','Brisanje','Ogranicenje','Prigovor','Pristup') NOT NULL,
  Details text NOT NULL,
  Status enum('Podnet','UObradi','Usvojen','DelimicnoUsvojen','Odbijen') NOT NULL DEFAULT 'Podnet',
  DueAt datetime NOT NULL,
  HandledByUserId int NULL,
  Response text NULL,
  RespondedAt datetime NULL,
  CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UpdatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  UNIQUE KEY ux_privacy_requests_reference (ReferenceNumber),
  KEY idx_privacy_requests_user (UserId, CreatedAt),
  KEY idx_privacy_requests_status_due (Status, DueAt),
  CONSTRAINT fk_privacy_requests_user FOREIGN KEY (UserId) REFERENCES users (Id),
  CONSTRAINT fk_privacy_requests_handler FOREIGN KEY (HandledByUserId) REFERENCES users (Id) ON DELETE SET NULL,
  CONSTRAINT chk_privacy_requests_response CHECK ((Status IN ('Usvojen','DelimicnoUsvojen','Odbijen')) = (RespondedAt IS NOT NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE compliance_alerts (
  Id int NOT NULL AUTO_INCREMENT,
  RuleCode varchar(40) NOT NULL,
  Severity enum('Nizak','Srednji','Visok') NOT NULL,
  UserId int NOT NULL,
  AccountId int NULL,
  TransactionId int NULL,
  Details varchar(1000) NOT NULL,
  Status enum('Otvoren','UProveri','ZatvorenBezNalaza','EskaliranSimulacija') NOT NULL DEFAULT 'Otvoren',
  ReviewedByUserId int NULL,
  ReviewNote varchar(1000) NULL,
  CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  ReviewedAt datetime NULL,
  PRIMARY KEY (Id),
  UNIQUE KEY ux_compliance_alerts_rule_transaction (RuleCode, TransactionId),
  KEY idx_compliance_alerts_status (Status, Severity, CreatedAt),
  KEY idx_compliance_alerts_user (UserId, CreatedAt),
  CONSTRAINT fk_compliance_alerts_user FOREIGN KEY (UserId) REFERENCES users (Id),
  CONSTRAINT fk_compliance_alerts_account FOREIGN KEY (AccountId) REFERENCES accounts (Id),
  CONSTRAINT fk_compliance_alerts_transaction FOREIGN KEY (TransactionId) REFERENCES transactions (Id),
  CONSTRAINT fk_compliance_alerts_reviewer FOREIGN KEY (ReviewedByUserId) REFERENCES users (Id) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO schema_migrations (Version, Description) VALUES ('V006', 'sessions, preferences, consents, fees, KYC, complaints, feedback, FAQ, data access log, privacy requests, compliance alerts');
