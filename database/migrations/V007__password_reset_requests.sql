CREATE TABLE password_reset_requests (
  Id int NOT NULL AUTO_INCREMENT,
  ReferenceNumber varchar(20) NOT NULL,
  UserId int NOT NULL,
  NewPasswordHash varchar(255) NULL,
  Status enum('Podnet','Odobren','Odbijen','Istekao') NOT NULL DEFAULT 'Podnet',
  IpAddress varchar(45) NULL,
  UserAgent varchar(256) NULL,
  ExpiresAt datetime NOT NULL,
  ReviewedByUserId int NULL,
  ReviewNote varchar(500) NULL,
  ReviewedAt datetime NULL,
  CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UpdatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  UNIQUE KEY ux_password_reset_requests_reference (ReferenceNumber),
  KEY idx_password_reset_requests_user (UserId, Status, CreatedAt),
  KEY idx_password_reset_requests_status (Status, CreatedAt),
  CONSTRAINT fk_password_reset_requests_user FOREIGN KEY (UserId) REFERENCES users (Id),
  CONSTRAINT fk_password_reset_requests_reviewer FOREIGN KEY (ReviewedByUserId) REFERENCES users (Id) ON DELETE SET NULL,
  CONSTRAINT chk_password_reset_requests_pending_hash CHECK (Status <> 'Podnet' OR NewPasswordHash IS NOT NULL),
  CONSTRAINT chk_password_reset_requests_decided_hash CHECK (Status = 'Podnet' OR NewPasswordHash IS NULL),
  CONSTRAINT chk_password_reset_requests_review CHECK ((Status IN ('Odobren','Odbijen')) = (ReviewedAt IS NOT NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO schema_migrations (Version, Description) VALUES ('V007', 'password reset requests approved by administrator');
