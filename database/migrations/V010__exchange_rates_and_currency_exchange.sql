CREATE TABLE exchange_rates (
  Id int NOT NULL AUTO_INCREMENT,
  Currency char(3) NOT NULL,
  BuyRate decimal(18,4) NOT NULL,
  MiddleRate decimal(18,4) NOT NULL,
  SellRate decimal(18,4) NOT NULL,
  ValidFrom datetime NOT NULL,
  CreatedByUserId int NULL,
  CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  KEY idx_exchange_rates_currency_valid (Currency, ValidFrom),
  CONSTRAINT fk_exchange_rates_user FOREIGN KEY (CreatedByUserId) REFERENCES users (Id) ON DELETE SET NULL,
  CONSTRAINT chk_exchange_rates_order CHECK (BuyRate > 0 AND BuyRate < MiddleRate AND MiddleRate < SellRate),
  CONSTRAINT chk_exchange_rates_currency CHECK (Currency REGEXP '^[A-Z]{3}$' AND Currency <> 'RSD')
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO exchange_rates (Currency, BuyRate, MiddleRate, SellRate, ValidFrom) VALUES
  ('EUR', 116.5000, 117.2000, 117.9000, UTC_TIMESTAMP()),
  ('USD', 107.6000, 108.4000, 109.2000, UTC_TIMESTAMP());

ALTER TABLE transactions DROP CHECK chk_transactions_type_accounts;

ALTER TABLE transactions
  MODIFY Type enum('Uplata','Isplata','Transfer','EksterniTransfer','Konverzija') NOT NULL,
  ADD CONSTRAINT chk_transactions_type_accounts CHECK (
    (Type = 'Uplata' AND FromAccountId IS NULL AND ToAccountId IS NOT NULL)
    OR (Type = 'Isplata' AND FromAccountId IS NOT NULL AND ToAccountId IS NULL)
    OR (Type = 'Transfer' AND FromAccountId IS NOT NULL AND ToAccountId IS NOT NULL AND FromAccountId <> ToAccountId)
    OR (Type = 'EksterniTransfer' AND FromAccountId IS NOT NULL AND ToAccountId IS NULL)
    OR (Type = 'Konverzija' AND ((FromAccountId IS NOT NULL AND ToAccountId IS NULL) OR (FromAccountId IS NULL AND ToAccountId IS NOT NULL)))
  );

CREATE TABLE currency_exchanges (
  Id int NOT NULL AUTO_INCREMENT,
  ReferenceNumber varchar(20) NOT NULL,
  UserId int NOT NULL,
  FromAccountId int NOT NULL,
  ToAccountId int NOT NULL,
  FromCurrency char(3) NOT NULL,
  ToCurrency char(3) NOT NULL,
  FromAmount decimal(18,2) NOT NULL,
  ToAmount decimal(18,2) NOT NULL,
  AppliedRate decimal(18,4) NOT NULL,
  RateType enum('Kupovni','Prodajni') NOT NULL,
  MiddleRate decimal(18,4) NOT NULL,
  SpreadRsd decimal(18,2) NOT NULL,
  ExchangeRateId int NOT NULL,
  SourceTransactionId int NOT NULL,
  TargetTransactionId int NOT NULL,
  IdempotencyKey varchar(64) NULL,
  CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  UNIQUE KEY ux_currency_exchanges_reference (ReferenceNumber),
  UNIQUE KEY ux_currency_exchanges_idempotency (UserId, IdempotencyKey),
  KEY idx_currency_exchanges_user (UserId, CreatedAt),
  CONSTRAINT fk_currency_exchanges_user FOREIGN KEY (UserId) REFERENCES users (Id),
  CONSTRAINT fk_currency_exchanges_from FOREIGN KEY (FromAccountId) REFERENCES accounts (Id),
  CONSTRAINT fk_currency_exchanges_to FOREIGN KEY (ToAccountId) REFERENCES accounts (Id),
  CONSTRAINT fk_currency_exchanges_rate FOREIGN KEY (ExchangeRateId) REFERENCES exchange_rates (Id),
  CONSTRAINT fk_currency_exchanges_source_tx FOREIGN KEY (SourceTransactionId) REFERENCES transactions (Id),
  CONSTRAINT fk_currency_exchanges_target_tx FOREIGN KEY (TargetTransactionId) REFERENCES transactions (Id),
  CONSTRAINT chk_currency_exchanges_amounts CHECK (FromAmount > 0 AND ToAmount > 0 AND SpreadRsd >= 0),
  CONSTRAINT chk_currency_exchanges_pair CHECK (FromCurrency <> ToCurrency AND (FromCurrency = 'RSD' OR ToCurrency = 'RSD')),
  CONSTRAINT chk_currency_exchanges_accounts CHECK (FromAccountId <> ToAccountId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO schema_migrations (Version, Description) VALUES ('V010', 'exchange rate list and currency exchange between own accounts');
