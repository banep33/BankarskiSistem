CREATE TABLE ledger_accounts (
  Id int NOT NULL AUTO_INCREMENT,
  Code varchar(40) NOT NULL,
  Name varchar(150) NOT NULL,
  Type enum('Klijent','Blagajna','Uskladjivanje','Menjacnica','Provizije','Kliring') NOT NULL,
  Currency char(3) NOT NULL,
  AccountId int NULL,
  CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  UNIQUE KEY ux_ledger_accounts_code (Code),
  UNIQUE KEY ux_ledger_accounts_account (AccountId),
  KEY idx_ledger_accounts_type_currency (Type, Currency),
  CONSTRAINT fk_ledger_accounts_account FOREIGN KEY (AccountId) REFERENCES accounts (Id),
  CONSTRAINT chk_ledger_accounts_currency CHECK (Currency REGEXP '^[A-Z]{3}$'),
  CONSTRAINT chk_ledger_accounts_customer_link CHECK ((Type = 'Klijent') = (AccountId IS NOT NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE journal_entries (
  Id int NOT NULL AUTO_INCREMENT,
  TransactionId int NULL,
  EntryType varchar(40) NOT NULL,
  Description varchar(255) NOT NULL,
  IsPosted tinyint(1) NOT NULL DEFAULT 0,
  CreatedByUserId int NULL,
  CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PostedAt datetime NULL,
  MigrationKey varchar(60) NULL,
  PRIMARY KEY (Id),
  UNIQUE KEY ux_journal_entries_transaction (TransactionId),
  KEY idx_journal_entries_created (CreatedAt),
  KEY idx_journal_entries_created_by (CreatedByUserId),
  CONSTRAINT fk_journal_entries_transaction FOREIGN KEY (TransactionId) REFERENCES transactions (Id),
  CONSTRAINT fk_journal_entries_user FOREIGN KEY (CreatedByUserId) REFERENCES users (Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE postings (
  Id bigint NOT NULL AUTO_INCREMENT,
  JournalEntryId int NOT NULL,
  LedgerAccountId int NOT NULL,
  Amount decimal(18,2) NOT NULL,
  Currency char(3) NOT NULL,
  BalanceAfter decimal(18,2) NULL,
  CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  KEY idx_postings_entry (JournalEntryId),
  KEY idx_postings_ledger_account (LedgerAccountId, JournalEntryId),
  CONSTRAINT fk_postings_entry FOREIGN KEY (JournalEntryId) REFERENCES journal_entries (Id),
  CONSTRAINT fk_postings_ledger_account FOREIGN KEY (LedgerAccountId) REFERENCES ledger_accounts (Id),
  CONSTRAINT chk_postings_amount_non_zero CHECK (Amount <> 0),
  CONSTRAINT chk_postings_currency CHECK (Currency REGEXP '^[A-Z]{3}$')
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DELIMITER $$

CREATE TRIGGER trg_postings_validate_insert
BEFORE INSERT ON postings
FOR EACH ROW
BEGIN
  IF (SELECT IsPosted FROM journal_entries WHERE Id = NEW.JournalEntryId) = 1 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Proknjizenom nalogu se ne mogu dodavati stavke.';
  END IF;
  IF (SELECT Currency FROM ledger_accounts WHERE Id = NEW.LedgerAccountId) <> NEW.Currency THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Valuta stavke mora odgovarati valuti konta.';
  END IF;
END$$

CREATE TRIGGER trg_postings_prevent_update
BEFORE UPDATE ON postings
FOR EACH ROW
BEGIN
  IF NOT (OLD.JournalEntryId <=> NEW.JournalEntryId
          AND OLD.LedgerAccountId <=> NEW.LedgerAccountId
          AND OLD.Amount <=> NEW.Amount
          AND OLD.Currency <=> NEW.Currency
          AND OLD.CreatedAt <=> NEW.CreatedAt)
     OR (SELECT IsPosted FROM journal_entries WHERE Id = OLD.JournalEntryId) = 1 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Stavke knjizenja su nepromenljive.';
  END IF;
END$$

CREATE TRIGGER trg_postings_prevent_delete
BEFORE DELETE ON postings
FOR EACH ROW
BEGIN
  IF (SELECT IsPosted FROM journal_entries WHERE Id = OLD.JournalEntryId) = 1 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Stavke proknjizenog naloga se ne mogu brisati.';
  END IF;
END$$

CREATE TRIGGER trg_journal_entries_validate_posting
BEFORE UPDATE ON journal_entries
FOR EACH ROW
BEGIN
  IF OLD.IsPosted = 1 AND NOT (
       OLD.TransactionId <=> NEW.TransactionId
       AND OLD.EntryType <=> NEW.EntryType
       AND OLD.Description <=> NEW.Description
       AND NEW.IsPosted = 1
       AND OLD.CreatedAt <=> NEW.CreatedAt
       AND OLD.PostedAt <=> NEW.PostedAt) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Proknjizen nalog se ne moze menjati.';
  END IF;

  IF OLD.IsPosted = 0 AND NEW.IsPosted = 1 THEN
    IF (SELECT COUNT(*) FROM postings WHERE JournalEntryId = NEW.Id) < 2 THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Nalog za knjizenje mora imati najmanje dve stavke.';
    END IF;
    IF EXISTS (
      SELECT 1 FROM postings
      WHERE JournalEntryId = NEW.Id
      GROUP BY Currency
      HAVING SUM(Amount) <> 0
    ) THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Nalog za knjizenje nije uravnotezen (zbir stavki po valuti mora biti nula).';
    END IF;
  END IF;
END$$

CREATE TRIGGER trg_journal_entries_prevent_delete
BEFORE DELETE ON journal_entries
FOR EACH ROW
BEGIN
  IF OLD.IsPosted = 1 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Proknjizen nalog se ne moze brisati.';
  END IF;
END$$

DELIMITER ;

INSERT INTO ledger_accounts (Code, Name, Type, Currency)
SELECT CONCAT(system_types.Prefix, '-', currencies.Currency), CONCAT(system_types.Label, ' ', currencies.Currency), system_types.Type, currencies.Currency
FROM (
  SELECT 'RSD' AS Currency UNION SELECT 'EUR' UNION SELECT 'USD'
  UNION SELECT DISTINCT Currency FROM accounts
) currencies
CROSS JOIN (
  SELECT 'SYS-BLAGAJNA' AS Prefix, 'Blagajna banke' AS Label, 'Blagajna' AS Type
  UNION SELECT 'SYS-USKLADJIVANJE', 'Neuskladjena pocetna stanja', 'Uskladjivanje'
  UNION SELECT 'SYS-MENJACNICA', 'Devizna pozicija banke', 'Menjacnica'
  UNION SELECT 'SYS-PROVIZIJE', 'Prihodi od provizija', 'Provizije'
  UNION SELECT 'SYS-KLIRING', 'Kliring prema drugim bankama', 'Kliring'
) system_types;

INSERT INTO ledger_accounts (Code, Name, Type, Currency, AccountId, CreatedAt)
SELECT CONCAT('KLI-', a.AccountNumber), CONCAT('Racun klijenta ', a.AccountNumber), 'Klijent', a.Currency, a.Id, a.CreatedAt
FROM accounts a;

CREATE TEMPORARY TABLE migration_opening_differences AS
SELECT
  a.Id AS AccountId,
  a.AccountNumber,
  a.Currency,
  a.CreatedAt,
  a.Balance - (
    COALESCE((SELECT SUM(t.Amount) FROM transactions t WHERE t.ToAccountId = a.Id AND t.Status = 'Uspesna'), 0)
    - COALESCE((SELECT SUM(t.Amount) FROM transactions t WHERE t.FromAccountId = a.Id AND t.Status = 'Uspesna'), 0)
  ) AS Difference
FROM accounts a;

INSERT INTO journal_entries (EntryType, Description, CreatedAt, MigrationKey)
SELECT 'PocetnoStanje', CONCAT('Neuskladjeno pocetno stanje racuna ', AccountNumber), CreatedAt, CONCAT('OPEN-', AccountId)
FROM migration_opening_differences
WHERE Difference <> 0
ORDER BY CreatedAt, AccountId;

INSERT INTO postings (JournalEntryId, LedgerAccountId, Amount, Currency, CreatedAt)
SELECT je.Id, la.Id, d.Difference, d.Currency, d.CreatedAt
FROM migration_opening_differences d
JOIN journal_entries je ON je.MigrationKey = CONCAT('OPEN-', d.AccountId)
JOIN ledger_accounts la ON la.AccountId = d.AccountId;

INSERT INTO postings (JournalEntryId, LedgerAccountId, Amount, Currency, CreatedAt)
SELECT je.Id, la.Id, -d.Difference, d.Currency, d.CreatedAt
FROM migration_opening_differences d
JOIN journal_entries je ON je.MigrationKey = CONCAT('OPEN-', d.AccountId)
JOIN ledger_accounts la ON la.Type = 'Uskladjivanje' AND la.Currency = d.Currency;

INSERT INTO journal_entries (TransactionId, EntryType, Description, CreatedByUserId, CreatedAt)
SELECT t.Id, t.Type, COALESCE(NULLIF(t.Description, ''), CONCAT(t.Type, ' ', t.ReferenceNumber)), t.PerformedByUserId, t.CreatedAt
FROM transactions t
WHERE t.Status = 'Uspesna'
ORDER BY t.CreatedAt, t.Id;

INSERT INTO postings (JournalEntryId, LedgerAccountId, Amount, Currency, CreatedAt)
SELECT je.Id, la.Id, t.Amount, t.Currency, t.CreatedAt
FROM transactions t
JOIN journal_entries je ON je.TransactionId = t.Id
JOIN ledger_accounts la ON la.AccountId = t.ToAccountId
WHERE t.ToAccountId IS NOT NULL;

INSERT INTO postings (JournalEntryId, LedgerAccountId, Amount, Currency, CreatedAt)
SELECT je.Id, la.Id, -t.Amount, t.Currency, t.CreatedAt
FROM transactions t
JOIN journal_entries je ON je.TransactionId = t.Id
JOIN ledger_accounts la ON la.AccountId = t.FromAccountId
WHERE t.FromAccountId IS NOT NULL;

INSERT INTO postings (JournalEntryId, LedgerAccountId, Amount, Currency, CreatedAt)
SELECT je.Id, la.Id, CASE t.Type WHEN 'Uplata' THEN -t.Amount ELSE t.Amount END, t.Currency, t.CreatedAt
FROM transactions t
JOIN journal_entries je ON je.TransactionId = t.Id
JOIN ledger_accounts la ON la.Type = 'Blagajna' AND la.Currency = t.Currency
WHERE t.Type IN ('Uplata', 'Isplata');

CREATE TEMPORARY TABLE migration_running_balances AS
SELECT
  p.Id AS PostingId,
  SUM(p.Amount) OVER (PARTITION BY p.LedgerAccountId ORDER BY je.CreatedAt, je.Id, p.Id) AS RunningBalance
FROM postings p
JOIN journal_entries je ON je.Id = p.JournalEntryId
JOIN ledger_accounts la ON la.Id = p.LedgerAccountId
WHERE la.Type = 'Klijent';

UPDATE postings p
JOIN migration_running_balances r ON r.PostingId = p.Id
SET p.BalanceAfter = r.RunningBalance;

UPDATE journal_entries SET IsPosted = 1, PostedAt = CreatedAt WHERE IsPosted = 0;

DROP TEMPORARY TABLE migration_running_balances;
DROP TEMPORARY TABLE migration_opening_differences;

ALTER TABLE journal_entries DROP COLUMN MigrationKey;

CREATE OR REPLACE VIEW v_account_reconciliation AS
SELECT
  a.Id AS AccountId,
  a.AccountNumber,
  a.Currency,
  a.Balance AS StoredBalance,
  COALESCE(SUM(CASE WHEN je.IsPosted = 1 THEN p.Amount END), 0) AS LedgerBalance,
  a.Balance - COALESCE(SUM(CASE WHEN je.IsPosted = 1 THEN p.Amount END), 0) AS Difference
FROM accounts a
LEFT JOIN ledger_accounts la ON la.AccountId = a.Id
LEFT JOIN postings p ON p.LedgerAccountId = la.Id
LEFT JOIN journal_entries je ON je.Id = p.JournalEntryId
GROUP BY a.Id, a.AccountNumber, a.Currency, a.Balance;

CREATE OR REPLACE VIEW v_ledger_trial_balance AS
SELECT p.Currency, SUM(p.Amount) AS Total, COUNT(*) AS PostingCount
FROM postings p
JOIN journal_entries je ON je.Id = p.JournalEntryId AND je.IsPosted = 1
GROUP BY p.Currency;

CREATE OR REPLACE VIEW v_ledger_account_balances AS
SELECT la.Id AS LedgerAccountId, la.Code, la.Name, la.Type, la.Currency,
       COALESCE(SUM(CASE WHEN je.IsPosted = 1 THEN p.Amount END), 0) AS Balance
FROM ledger_accounts la
LEFT JOIN postings p ON p.LedgerAccountId = la.Id
LEFT JOIN journal_entries je ON je.Id = p.JournalEntryId
GROUP BY la.Id, la.Code, la.Name, la.Type, la.Currency;

INSERT INTO schema_migrations (Version, Description) VALUES ('V004', 'double-entry ledger with migrated history');
