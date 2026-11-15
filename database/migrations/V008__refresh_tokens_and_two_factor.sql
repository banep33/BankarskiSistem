ALTER TABLE user_sessions
  ADD COLUMN RefreshTokenHash char(64) NULL AFTER ReauthenticatedAt,
  ADD COLUMN PreviousRefreshTokenHash char(64) NULL AFTER RefreshTokenHash,
  ADD COLUMN RefreshRotatedAt datetime NULL AFTER PreviousRefreshTokenHash,
  ADD UNIQUE KEY ux_user_sessions_refresh (RefreshTokenHash),
  ADD KEY idx_user_sessions_previous_refresh (PreviousRefreshTokenHash);

ALTER TABLE users
  ADD COLUMN TotpSecretProtected varchar(1024) NULL AFTER TokenVersion,
  ADD COLUMN TotpEnabledAt datetime NULL AFTER TotpSecretProtected,
  ADD COLUMN TotpLastUsedStep bigint NULL AFTER TotpEnabledAt,
  ADD CONSTRAINT chk_users_totp_secret CHECK (TotpEnabledAt IS NULL OR TotpSecretProtected IS NOT NULL);

CREATE TABLE user_recovery_codes (
  Id int NOT NULL AUTO_INCREMENT,
  UserId int NOT NULL,
  CodeHash char(64) NOT NULL,
  UsedAt datetime NULL,
  CreatedAt datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  UNIQUE KEY ux_user_recovery_codes_hash (UserId, CodeHash),
  KEY idx_user_recovery_codes_user (UserId, UsedAt),
  CONSTRAINT fk_user_recovery_codes_user FOREIGN KEY (UserId) REFERENCES users (Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO schema_migrations (Version, Description) VALUES ('V008', 'rotating refresh tokens and TOTP two-factor authentication');
