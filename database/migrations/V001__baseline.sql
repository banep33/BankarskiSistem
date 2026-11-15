CREATE TABLE IF NOT EXISTS schema_migrations (
  Version VARCHAR(50) NOT NULL,
  Description VARCHAR(255) NOT NULL,
  AppliedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Version)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS users (
  Id int NOT NULL AUTO_INCREMENT,
  FirstName varchar(100) NOT NULL,
  LastName varchar(100) NOT NULL,
  Email varchar(255) NOT NULL,
  PasswordHash varchar(255) NOT NULL,
  Role enum('Administrator','Korisnik','Bankar') NOT NULL,
  IsActive tinyint(1) DEFAULT '1',
  IsLocked tinyint(1) DEFAULT '0',
  FailedLoginAttempts int DEFAULT '0',
  LockedUntil datetime DEFAULT NULL,
  CreatedAt datetime DEFAULT CURRENT_TIMESTAMP,
  UpdatedAt datetime DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  UNIQUE KEY Email (Email)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS accounts (
  Id int NOT NULL AUTO_INCREMENT,
  UserId int NOT NULL,
  AccountNumber varchar(20) NOT NULL,
  Balance decimal(18,2) NOT NULL DEFAULT '0.00',
  Currency varchar(3) DEFAULT 'RSD',
  Status enum('Aktivan','Neaktivan','Blokiran') DEFAULT 'Aktivan',
  CreatedAt datetime DEFAULT CURRENT_TIMESTAMP,
  UpdatedAt datetime DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  UNIQUE KEY AccountNumber (AccountNumber),
  KEY UserId (UserId),
  CONSTRAINT accounts_ibfk_1 FOREIGN KEY (UserId) REFERENCES users (Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS transactions (
  Id int NOT NULL AUTO_INCREMENT,
  FromAccountId int DEFAULT NULL,
  ToAccountId int DEFAULT NULL,
  Amount decimal(18,2) NOT NULL,
  Type enum('Uplata','Isplata','Transfer') NOT NULL,
  Status enum('Uspesna','Neuspesna','NaCekanju') DEFAULT 'NaCekanju',
  Description varchar(500) DEFAULT NULL,
  ReferenceNumber varchar(50) NOT NULL,
  PerformedByUserId int DEFAULT NULL,
  CreatedAt datetime DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  UNIQUE KEY ReferenceNumber (ReferenceNumber),
  KEY FromAccountId (FromAccountId),
  KEY ToAccountId (ToAccountId),
  KEY PerformedByUserId (PerformedByUserId),
  CONSTRAINT transactions_ibfk_1 FOREIGN KEY (FromAccountId) REFERENCES accounts (Id),
  CONSTRAINT transactions_ibfk_2 FOREIGN KEY (ToAccountId) REFERENCES accounts (Id),
  CONSTRAINT transactions_ibfk_3 FOREIGN KEY (PerformedByUserId) REFERENCES users (Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS auditlogs (
  Id int NOT NULL AUTO_INCREMENT,
  UserId int DEFAULT NULL,
  Action varchar(255) NOT NULL,
  EntityType varchar(100) DEFAULT NULL,
  EntityId int DEFAULT NULL,
  IpAddress varchar(45) DEFAULT NULL,
  OldValue text,
  NewValue text,
  IsSuccess tinyint(1) DEFAULT '1',
  ErrorMessage text,
  CreatedAt datetime DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  KEY UserId (UserId),
  CONSTRAINT auditlogs_ibfk_1 FOREIGN KEY (UserId) REFERENCES users (Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT IGNORE INTO schema_migrations (Version, Description) VALUES ('V001', 'baseline');
