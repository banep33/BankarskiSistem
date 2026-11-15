UPDATE users SET IsActive = 1 WHERE IsActive IS NULL;
UPDATE users SET IsLocked = 0 WHERE IsLocked IS NULL;
UPDATE users SET FailedLoginAttempts = 0 WHERE FailedLoginAttempts IS NULL;
UPDATE users SET CreatedAt = UTC_TIMESTAMP() WHERE CreatedAt IS NULL;
UPDATE users SET UpdatedAt = CreatedAt WHERE UpdatedAt IS NULL;

ALTER TABLE users
  MODIFY IsActive tinyint(1) NOT NULL DEFAULT 1,
  MODIFY IsLocked tinyint(1) NOT NULL DEFAULT 0,
  MODIFY FailedLoginAttempts int NOT NULL DEFAULT 0,
  MODIFY CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  MODIFY UpdatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  ADD COLUMN TokenVersion int NOT NULL DEFAULT 0 AFTER LockedUntil,
  ADD CONSTRAINT chk_users_failed_attempts_non_negative CHECK (FailedLoginAttempts >= 0),
  ADD CONSTRAINT chk_users_token_version_non_negative CHECK (TokenVersion >= 0),
  ADD INDEX idx_users_role_last_name (Role, LastName);

UPDATE accounts SET Currency = 'RSD' WHERE Currency IS NULL OR Currency = '';
UPDATE accounts SET Status = 'Aktivan' WHERE Status IS NULL;
UPDATE accounts SET CreatedAt = UTC_TIMESTAMP() WHERE CreatedAt IS NULL;
UPDATE accounts SET UpdatedAt = CreatedAt WHERE UpdatedAt IS NULL;

ALTER TABLE accounts
  MODIFY Currency char(3) NOT NULL DEFAULT 'RSD',
  MODIFY Status enum('Aktivan','Neaktivan','Blokiran') NOT NULL DEFAULT 'Aktivan',
  MODIFY CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  MODIFY UpdatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  ADD CONSTRAINT chk_accounts_balance_non_negative CHECK (Balance >= 0),
  ADD CONSTRAINT chk_accounts_currency_iso CHECK (Currency REGEXP '^[A-Z]{3}$'),
  ADD CONSTRAINT chk_accounts_number_format CHECK (AccountNumber REGEXP '^[0-9]{3}-[0-9]{7}-[0-9]{2}$');

UPDATE transactions SET Status = 'Uspesna' WHERE Status IS NULL;
UPDATE transactions SET CreatedAt = UTC_TIMESTAMP() WHERE CreatedAt IS NULL;

ALTER TABLE transactions
  ADD COLUMN Currency char(3) NULL AFTER Amount,
  ADD COLUMN FromBalanceAfter decimal(18,2) NULL AFTER Currency,
  ADD COLUMN ToBalanceAfter decimal(18,2) NULL AFTER FromBalanceAfter,
  ADD COLUMN IdempotencyKey varchar(64) NULL AFTER PerformedByUserId;

UPDATE transactions t
JOIN accounts a ON a.Id = COALESCE(t.FromAccountId, t.ToAccountId)
SET t.Currency = a.Currency
WHERE t.Currency IS NULL;

ALTER TABLE transactions
  MODIFY Currency char(3) NOT NULL,
  MODIFY Status enum('Uspesna','Neuspesna','NaCekanju') NOT NULL DEFAULT 'NaCekanju',
  MODIFY CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  ADD CONSTRAINT chk_transactions_amount_positive CHECK (Amount > 0),
  ADD CONSTRAINT chk_transactions_currency_iso CHECK (Currency REGEXP '^[A-Z]{3}$'),
  ADD CONSTRAINT chk_transactions_type_accounts CHECK (
    (Type = 'Uplata' AND FromAccountId IS NULL AND ToAccountId IS NOT NULL)
    OR (Type = 'Isplata' AND FromAccountId IS NOT NULL AND ToAccountId IS NULL)
    OR (Type = 'Transfer' AND FromAccountId IS NOT NULL AND ToAccountId IS NOT NULL AND FromAccountId <> ToAccountId)
  ),
  ADD UNIQUE INDEX ux_transactions_idempotency (PerformedByUserId, IdempotencyKey),
  ADD INDEX idx_transactions_from_created (FromAccountId, CreatedAt),
  ADD INDEX idx_transactions_to_created (ToAccountId, CreatedAt),
  ADD INDEX idx_transactions_created (CreatedAt);

SET @drop_index_sql = NULL;
SELECT CONCAT('ALTER TABLE transactions ', GROUP_CONCAT(DISTINCT CONCAT('DROP INDEX `', INDEX_NAME, '`') SEPARATOR ', '))
INTO @drop_index_sql
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'transactions'
  AND INDEX_NAME IN ('FromAccountId', 'ToAccountId', 'PerformedByUserId');
SET @drop_index_sql = COALESCE(@drop_index_sql, 'DO 0');
PREPARE drop_index_statement FROM @drop_index_sql;
EXECUTE drop_index_statement;
DEALLOCATE PREPARE drop_index_statement;

ALTER TABLE auditlogs DROP FOREIGN KEY auditlogs_ibfk_1;

UPDATE auditlogs SET IsSuccess = 1 WHERE IsSuccess IS NULL;
UPDATE auditlogs SET CreatedAt = UTC_TIMESTAMP() WHERE CreatedAt IS NULL;

ALTER TABLE auditlogs
  ADD INDEX idx_auditlogs_user_created (UserId, CreatedAt);

SET @drop_index_sql = NULL;
SELECT 'ALTER TABLE auditlogs DROP INDEX `UserId`'
INTO @drop_index_sql
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'auditlogs' AND INDEX_NAME = 'UserId'
LIMIT 1;
SET @drop_index_sql = COALESCE(@drop_index_sql, 'DO 0');
PREPARE drop_index_statement FROM @drop_index_sql;
EXECUTE drop_index_statement;
DEALLOCATE PREPARE drop_index_statement;

ALTER TABLE auditlogs
  MODIFY IsSuccess tinyint(1) NOT NULL DEFAULT 1,
  MODIFY CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  ADD INDEX idx_auditlogs_created (CreatedAt),
  ADD INDEX idx_auditlogs_entity (EntityType, EntityId),
  ADD CONSTRAINT fk_auditlogs_user FOREIGN KEY (UserId) REFERENCES users (Id) ON DELETE SET NULL;

INSERT INTO schema_migrations (Version, Description) VALUES ('V002', 'integrity constraints and indexes');
