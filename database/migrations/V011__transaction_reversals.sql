ALTER TABLE transactions DROP CHECK chk_transactions_type_accounts;

ALTER TABLE transactions
  MODIFY Type enum('Uplata','Isplata','Transfer','EksterniTransfer','Konverzija','Storno') NOT NULL,
  ADD COLUMN ReversalOfTransactionId int NULL AFTER IdempotencyKey,
  ADD COLUMN ReversedAt datetime NULL AFTER ReversalOfTransactionId,
  ADD UNIQUE KEY ux_transactions_reversal_of (ReversalOfTransactionId),
  ADD CONSTRAINT fk_transactions_reversal_of FOREIGN KEY (ReversalOfTransactionId) REFERENCES transactions (Id),
  ADD CONSTRAINT chk_transactions_type_accounts CHECK (
    (Type = 'Uplata' AND FromAccountId IS NULL AND ToAccountId IS NOT NULL)
    OR (Type = 'Isplata' AND FromAccountId IS NOT NULL AND ToAccountId IS NULL)
    OR (Type = 'Transfer' AND FromAccountId IS NOT NULL AND ToAccountId IS NOT NULL AND FromAccountId <> ToAccountId)
    OR (Type = 'EksterniTransfer' AND FromAccountId IS NOT NULL AND ToAccountId IS NULL)
    OR (Type = 'Konverzija' AND ((FromAccountId IS NOT NULL AND ToAccountId IS NULL) OR (FromAccountId IS NULL AND ToAccountId IS NOT NULL)))
    OR (Type = 'Storno' AND (FromAccountId IS NOT NULL OR ToAccountId IS NOT NULL) AND (FromAccountId IS NULL OR ToAccountId IS NULL OR FromAccountId <> ToAccountId))
  ),
  ADD CONSTRAINT chk_transactions_storno_link CHECK ((Type = 'Storno') = (ReversalOfTransactionId IS NOT NULL));

CREATE TABLE transaction_reversals (
  Id int NOT NULL AUTO_INCREMENT,
  ReferenceNumber varchar(20) NOT NULL,
  OriginalTransactionId int NOT NULL,
  ReversalTransactionId int NULL,
  Reason varchar(500) NOT NULL,
  Status enum('Podnet','Izvrsen','Odbijen') NOT NULL DEFAULT 'Podnet',
  RequestedByUserId int NOT NULL,
  ReviewedByUserId int NULL,
  ReviewNote varchar(500) NULL,
  CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  ReviewedAt datetime NULL,
  PRIMARY KEY (Id),
  UNIQUE KEY ux_transaction_reversals_reference (ReferenceNumber),
  KEY idx_transaction_reversals_status (Status, CreatedAt),
  KEY idx_transaction_reversals_original (OriginalTransactionId, Status),
  CONSTRAINT fk_transaction_reversals_original FOREIGN KEY (OriginalTransactionId) REFERENCES transactions (Id),
  CONSTRAINT fk_transaction_reversals_reversal FOREIGN KEY (ReversalTransactionId) REFERENCES transactions (Id),
  CONSTRAINT fk_transaction_reversals_requester FOREIGN KEY (RequestedByUserId) REFERENCES users (Id),
  CONSTRAINT fk_transaction_reversals_reviewer FOREIGN KEY (ReviewedByUserId) REFERENCES users (Id),
  CONSTRAINT chk_transaction_reversals_four_eyes CHECK (ReviewedByUserId IS NULL OR ReviewedByUserId <> RequestedByUserId),
  CONSTRAINT chk_transaction_reversals_result CHECK (
    (Status = 'Podnet' AND ReviewedAt IS NULL AND ReversalTransactionId IS NULL)
    OR (Status = 'Izvrsen' AND ReviewedAt IS NOT NULL AND ReversalTransactionId IS NOT NULL)
    OR (Status = 'Odbijen' AND ReviewedAt IS NOT NULL AND ReversalTransactionId IS NULL AND ReviewNote IS NOT NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO schema_migrations (Version, Description) VALUES ('V011', 'transaction reversals with maker-checker approval');
