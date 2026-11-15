ALTER TABLE transactions DROP CHECK chk_transactions_type_accounts;

ALTER TABLE transactions
  MODIFY Type enum('Uplata','Isplata','Transfer','EksterniTransfer','Konverzija','Storno','Kartica') NOT NULL,
  ADD CONSTRAINT chk_transactions_type_accounts CHECK (
    (Type = 'Uplata' AND FromAccountId IS NULL AND ToAccountId IS NOT NULL)
    OR (Type = 'Isplata' AND FromAccountId IS NOT NULL AND ToAccountId IS NULL)
    OR (Type = 'Transfer' AND FromAccountId IS NOT NULL AND ToAccountId IS NOT NULL AND FromAccountId <> ToAccountId)
    OR (Type = 'EksterniTransfer' AND FromAccountId IS NOT NULL AND ToAccountId IS NULL)
    OR (Type = 'Konverzija' AND ((FromAccountId IS NOT NULL AND ToAccountId IS NULL) OR (FromAccountId IS NULL AND ToAccountId IS NOT NULL)))
    OR (Type = 'Storno' AND (FromAccountId IS NOT NULL OR ToAccountId IS NOT NULL) AND (FromAccountId IS NULL OR ToAccountId IS NULL OR FromAccountId <> ToAccountId))
    OR (Type = 'Kartica' AND FromAccountId IS NOT NULL AND ToAccountId IS NULL)
  );

CREATE TABLE cards (
  Id int NOT NULL AUTO_INCREMENT,
  UserId int NOT NULL,
  AccountId int NOT NULL,
  Brand enum('Dina','Visa','Mastercard') NOT NULL,
  PanHash char(64) NOT NULL,
  Last4 char(4) NOT NULL,
  CardholderName varchar(60) NOT NULL,
  ExpiryMonth tinyint NOT NULL,
  ExpiryYear smallint NOT NULL,
  Status enum('Aktivna','Blokirana','Ukinuta') NOT NULL DEFAULT 'Aktivna',
  OnlineEnabled tinyint(1) NOT NULL DEFAULT 1,
  ContactlessEnabled tinyint(1) NOT NULL DEFAULT 1,
  AbroadEnabled tinyint(1) NOT NULL DEFAULT 0,
  DailyPurchaseLimit decimal(18,2) NOT NULL,
  DailyAtmLimit decimal(18,2) NOT NULL,
  StatusReason varchar(200) NULL,
  BlockedByBank tinyint(1) NOT NULL DEFAULT 0,
  ReplacesCardId int NULL,
  IssuedByUserId int NULL,
  CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UpdatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CancelledAt datetime NULL,
  PRIMARY KEY (Id),
  UNIQUE KEY ux_cards_pan_hash (PanHash),
  KEY idx_cards_user (UserId, Status),
  KEY idx_cards_account (AccountId),
  CONSTRAINT fk_cards_user FOREIGN KEY (UserId) REFERENCES users (Id),
  CONSTRAINT fk_cards_account FOREIGN KEY (AccountId) REFERENCES accounts (Id),
  CONSTRAINT fk_cards_replaces FOREIGN KEY (ReplacesCardId) REFERENCES cards (Id),
  CONSTRAINT fk_cards_issued_by FOREIGN KEY (IssuedByUserId) REFERENCES users (Id) ON DELETE SET NULL,
  CONSTRAINT chk_cards_expiry CHECK (ExpiryMonth BETWEEN 1 AND 12),
  CONSTRAINT chk_cards_limits CHECK (DailyPurchaseLimit >= 0 AND DailyAtmLimit >= 0),
  CONSTRAINT chk_cards_last4 CHECK (Last4 REGEXP '^[0-9]{4}$'),
  CONSTRAINT chk_cards_cancelled CHECK ((Status = 'Ukinuta') = (CancelledAt IS NOT NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE card_authorizations (
  Id int NOT NULL AUTO_INCREMENT,
  CardId int NOT NULL,
  Channel enum('Prodavnica','Internet','Bankomat') NOT NULL,
  MerchantName varchar(80) NOT NULL,
  CategoryCode varchar(30) NULL,
  Amount decimal(18,2) NOT NULL,
  Currency char(3) NOT NULL,
  IsAbroad tinyint(1) NOT NULL DEFAULT 0,
  Status enum('Odobrena','Odbijena') NOT NULL,
  DeclineReason varchar(200) NULL,
  TransactionId int NULL,
  CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  UNIQUE KEY ux_card_authorizations_tx (TransactionId),
  KEY idx_card_authorizations_card (CardId, CreatedAt),
  CONSTRAINT fk_card_authorizations_card FOREIGN KEY (CardId) REFERENCES cards (Id),
  CONSTRAINT fk_card_authorizations_tx FOREIGN KEY (TransactionId) REFERENCES transactions (Id),
  CONSTRAINT chk_card_authorizations_amount CHECK (Amount > 0),
  CONSTRAINT chk_card_authorizations_result CHECK (
    (Status = 'Odobrena' AND TransactionId IS NOT NULL AND DeclineReason IS NULL)
    OR (Status = 'Odbijena' AND TransactionId IS NULL AND DeclineReason IS NOT NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO schema_migrations (Version, Description) VALUES ('V014', 'simulated debit cards with controls, limits and authorization log');
