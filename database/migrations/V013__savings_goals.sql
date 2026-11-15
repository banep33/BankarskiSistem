ALTER TABLE accounts
  ADD COLUMN Kind enum('Tekuci','Stednja') NOT NULL DEFAULT 'Tekuci' AFTER Currency;

CREATE TABLE savings_goals (
  Id int NOT NULL AUTO_INCREMENT,
  UserId int NOT NULL,
  AccountId int NOT NULL,
  Name varchar(60) NOT NULL,
  TargetAmount decimal(18,2) NOT NULL,
  TargetDate date NULL,
  Status enum('Aktivan','Ostvaren','Zatvoren') NOT NULL DEFAULT 'Aktivan',
  CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UpdatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  AchievedAt datetime NULL,
  ClosedAt datetime NULL,
  PRIMARY KEY (Id),
  UNIQUE KEY ux_savings_goals_account (AccountId),
  KEY idx_savings_goals_user (UserId, Status),
  CONSTRAINT fk_savings_goals_user FOREIGN KEY (UserId) REFERENCES users (Id),
  CONSTRAINT fk_savings_goals_account FOREIGN KEY (AccountId) REFERENCES accounts (Id),
  CONSTRAINT chk_savings_goals_target CHECK (TargetAmount > 0),
  CONSTRAINT chk_savings_goals_closed CHECK ((Status = 'Zatvoren') = (ClosedAt IS NOT NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO schema_migrations (Version, Description) VALUES ('V013', 'savings goals backed by dedicated savings accounts');
