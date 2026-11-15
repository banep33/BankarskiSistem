CREATE TABLE standing_orders (
  Id int NOT NULL AUTO_INCREMENT,
  ReferenceNumber varchar(20) NOT NULL,
  UserId int NOT NULL,
  FromAccountId int NOT NULL,
  RecipientName varchar(150) NOT NULL,
  RecipientAccountNumber varchar(20) NOT NULL,
  RecipientAddress varchar(200) NULL,
  Amount decimal(18,2) NOT NULL,
  Currency char(3) NOT NULL,
  PaymentCode char(3) NOT NULL,
  Purpose varchar(140) NOT NULL,
  ReferenceModel varchar(2) NULL,
  ReferenceNumberOnSlip varchar(23) NULL,
  Frequency enum('Nedeljno','Mesecno','Kvartalno','Godisnje') NOT NULL,
  StartDate date NOT NULL,
  EndDate date NULL,
  MaxExecutions int NULL,
  OccurrenceIndex int NOT NULL DEFAULT 0,
  NextExecutionDate date NULL,
  Status enum('Aktivan','Pauziran','Zavrsen','Otkazan') NOT NULL DEFAULT 'Aktivan',
  ExecutionsCount int NOT NULL DEFAULT 0,
  ConsecutiveFailures int NOT NULL DEFAULT 0,
  LastExecutedAt datetime NULL,
  LastFailureReason varchar(300) NULL,
  CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UpdatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  UNIQUE KEY ux_standing_orders_reference (ReferenceNumber),
  KEY idx_standing_orders_user (UserId, Status),
  KEY idx_standing_orders_due (Status, NextExecutionDate),
  CONSTRAINT fk_standing_orders_user FOREIGN KEY (UserId) REFERENCES users (Id),
  CONSTRAINT fk_standing_orders_account FOREIGN KEY (FromAccountId) REFERENCES accounts (Id),
  CONSTRAINT chk_standing_orders_amount CHECK (Amount > 0),
  CONSTRAINT chk_standing_orders_end CHECK (EndDate IS NULL OR EndDate >= StartDate),
  CONSTRAINT chk_standing_orders_max CHECK (MaxExecutions IS NULL OR MaxExecutions BETWEEN 1 AND 600),
  CONSTRAINT chk_standing_orders_next CHECK ((Status IN ('Aktivan','Pauziran')) = (NextExecutionDate IS NOT NULL)),
  CONSTRAINT chk_standing_orders_counts CHECK (ExecutionsCount >= 0 AND ConsecutiveFailures >= 0 AND OccurrenceIndex >= ExecutionsCount)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE standing_order_executions (
  Id int NOT NULL AUTO_INCREMENT,
  StandingOrderId int NOT NULL,
  ScheduledFor date NOT NULL,
  Status enum('Pokrenut','Izvrsen','Odbijen') NOT NULL DEFAULT 'Pokrenut',
  PaymentOrderId int NULL,
  FailureReason varchar(300) NULL,
  CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CompletedAt datetime NULL,
  PRIMARY KEY (Id),
  UNIQUE KEY ux_standing_order_executions_occurrence (StandingOrderId, ScheduledFor),
  KEY idx_standing_order_executions_payment (PaymentOrderId),
  CONSTRAINT fk_standing_order_executions_order FOREIGN KEY (StandingOrderId) REFERENCES standing_orders (Id),
  CONSTRAINT fk_standing_order_executions_payment FOREIGN KEY (PaymentOrderId) REFERENCES payment_orders (Id),
  CONSTRAINT chk_standing_order_executions_result CHECK (
    (Status = 'Pokrenut' AND CompletedAt IS NULL)
    OR (Status = 'Izvrsen' AND PaymentOrderId IS NOT NULL AND CompletedAt IS NOT NULL)
    OR (Status = 'Odbijen' AND FailureReason IS NOT NULL AND CompletedAt IS NOT NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO schema_migrations (Version, Description) VALUES ('V009', 'standing orders with per-occurrence execution log');
