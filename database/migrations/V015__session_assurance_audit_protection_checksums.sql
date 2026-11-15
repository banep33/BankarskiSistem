ALTER TABLE user_sessions
  ADD COLUMN MfaVerified tinyint(1) NOT NULL DEFAULT 0 AFTER ReauthenticatedAt;

ALTER TABLE schema_migrations
  ADD COLUMN Checksum char(64) NULL;

DROP TRIGGER IF EXISTS trg_auditlogs_prevent_update;
DROP TRIGGER IF EXISTS trg_auditlogs_prevent_delete;

DELIMITER $$

CREATE TRIGGER trg_auditlogs_prevent_update
BEFORE UPDATE ON auditlogs
FOR EACH ROW
BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Audit zapisi su nepromenljivi.';
END$$

CREATE TRIGGER trg_auditlogs_prevent_delete
BEFORE DELETE ON auditlogs
FOR EACH ROW
BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Audit zapisi se ne mogu brisati.';
END$$

DELIMITER ;

INSERT INTO schema_migrations (Version, Description) VALUES ('V015', 'session assurance level (MFA), immutable audit log, migration checksums');
