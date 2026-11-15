CREATE TABLE notifications (
  Id int NOT NULL AUTO_INCREMENT,
  UserId int NOT NULL,
  Type varchar(40) NOT NULL,
  Title varchar(150) NOT NULL,
  Message varchar(500) NOT NULL,
  IsRead tinyint(1) NOT NULL DEFAULT 0,
  TransactionId int NULL,
  CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  INDEX idx_notifications_user_read_created (UserId, IsRead, CreatedAt),
  INDEX idx_notifications_transaction (TransactionId),
  CONSTRAINT fk_notifications_user FOREIGN KEY (UserId) REFERENCES users (Id) ON DELETE CASCADE,
  CONSTRAINT fk_notifications_transaction FOREIGN KEY (TransactionId) REFERENCES transactions (Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP TRIGGER IF EXISTS trg_transactions_prevent_delete;
DROP TRIGGER IF EXISTS trg_transactions_protect_ledger;

DELIMITER $$

CREATE TRIGGER trg_transactions_prevent_delete
BEFORE DELETE ON transactions
FOR EACH ROW
BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Transakcije su nepromenljive i ne mogu se brisati.';
END$$

CREATE TRIGGER trg_transactions_protect_ledger
BEFORE UPDATE ON transactions
FOR EACH ROW
BEGIN
  IF NOT (
    OLD.Amount <=> NEW.Amount
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

CREATE OR REPLACE VIEW v_account_reconciliation AS
SELECT
  a.Id AS AccountId,
  a.AccountNumber,
  a.Currency,
  a.Balance AS StoredBalance,
  COALESCE(incoming.Total, 0) - COALESCE(outgoing.Total, 0) AS LedgerBalance,
  a.Balance - (COALESCE(incoming.Total, 0) - COALESCE(outgoing.Total, 0)) AS Difference
FROM accounts a
LEFT JOIN (
  SELECT ToAccountId AS AccountId, SUM(Amount) AS Total
  FROM transactions
  WHERE Status = 'Uspesna' AND ToAccountId IS NOT NULL
  GROUP BY ToAccountId
) incoming ON incoming.AccountId = a.Id
LEFT JOIN (
  SELECT FromAccountId AS AccountId, SUM(Amount) AS Total
  FROM transactions
  WHERE Status = 'Uspesna' AND FromAccountId IS NOT NULL
  GROUP BY FromAccountId
) outgoing ON outgoing.AccountId = a.Id;

INSERT INTO schema_migrations (Version, Description) VALUES ('V003', 'notifications, ledger protection, reconciliation view');
