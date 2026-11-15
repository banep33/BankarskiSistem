CREATE TABLE spending_categories (
  Id int NOT NULL AUTO_INCREMENT,
  Code varchar(30) NOT NULL,
  Name varchar(60) NOT NULL,
  Color char(7) NOT NULL,
  SortOrder int NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE KEY ux_spending_categories_code (Code),
  CONSTRAINT chk_spending_categories_color CHECK (Color REGEXP '^#[0-9A-Fa-f]{6}$')
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO spending_categories (Code, Name, Color, SortOrder) VALUES
  ('STANOVANJE', 'Stanovanje', '#0B2320', 10),
  ('REZIJE', 'Računi i režije', '#1F5F57', 20),
  ('HRANA', 'Hrana i namirnice', '#F05A3C', 30),
  ('PREVOZ', 'Prevoz', '#E8A33D', 40),
  ('KUPOVINA', 'Kupovina', '#C8F169', 50),
  ('ZDRAVLJE', 'Zdravlje', '#5B8DEF', 60),
  ('ZABAVA', 'Zabava i izlasci', '#B05CC9', 70),
  ('OBRAZOVANJE', 'Obrazovanje', '#2E9C6A', 80),
  ('PUTOVANJA', 'Putovanja', '#2BB3C0', 90),
  ('POREZI', 'Porezi i takse', '#7A6F66', 100),
  ('KREDITI', 'Krediti', '#A33B2C', 110),
  ('GOTOVINA', 'Gotovina', '#8C8C8C', 120),
  ('STEDNJA', 'Štednja i ulaganja', '#4A7C59', 130),
  ('OSTALO', 'Ostalo', '#B8B0A6', 140);

CREATE TABLE transaction_categories (
  Id int NOT NULL AUTO_INCREMENT,
  UserId int NOT NULL,
  TransactionId int NOT NULL,
  CategoryId int NOT NULL,
  AssignedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  UNIQUE KEY ux_transaction_categories_user_tx (UserId, TransactionId),
  CONSTRAINT fk_transaction_categories_user FOREIGN KEY (UserId) REFERENCES users (Id) ON DELETE CASCADE,
  CONSTRAINT fk_transaction_categories_tx FOREIGN KEY (TransactionId) REFERENCES transactions (Id),
  CONSTRAINT fk_transaction_categories_category FOREIGN KEY (CategoryId) REFERENCES spending_categories (Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE category_rules (
  Id int NOT NULL AUTO_INCREMENT,
  UserId int NOT NULL,
  CounterpartyAccountNumber varchar(20) NOT NULL,
  CategoryId int NOT NULL,
  CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  UNIQUE KEY ux_category_rules_user_account (UserId, CounterpartyAccountNumber),
  CONSTRAINT fk_category_rules_user FOREIGN KEY (UserId) REFERENCES users (Id) ON DELETE CASCADE,
  CONSTRAINT fk_category_rules_category FOREIGN KEY (CategoryId) REFERENCES spending_categories (Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE budgets (
  Id int NOT NULL AUTO_INCREMENT,
  UserId int NOT NULL,
  CategoryId int NOT NULL,
  MonthlyLimit decimal(18,2) NOT NULL,
  CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UpdatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  UNIQUE KEY ux_budgets_user_category (UserId, CategoryId),
  CONSTRAINT fk_budgets_user FOREIGN KEY (UserId) REFERENCES users (Id) ON DELETE CASCADE,
  CONSTRAINT fk_budgets_category FOREIGN KEY (CategoryId) REFERENCES spending_categories (Id),
  CONSTRAINT chk_budgets_limit CHECK (MonthlyLimit > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE budget_alerts (
  Id int NOT NULL AUTO_INCREMENT,
  BudgetId int NOT NULL,
  Month char(7) NOT NULL,
  Threshold int NOT NULL,
  CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  UNIQUE KEY ux_budget_alerts_once (BudgetId, Month, Threshold),
  CONSTRAINT fk_budget_alerts_budget FOREIGN KEY (BudgetId) REFERENCES budgets (Id) ON DELETE CASCADE,
  CONSTRAINT chk_budget_alerts_threshold CHECK (Threshold IN (80, 100))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO schema_migrations (Version, Description) VALUES ('V012', 'spending categories, user rules, monthly budgets and alerts');
