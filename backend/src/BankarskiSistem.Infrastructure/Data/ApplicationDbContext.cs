using BankarskiSistem.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace BankarskiSistem.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Account> Accounts => Set<Account>();
        public DbSet<Transaction> Transactions => Set<Transaction>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<LedgerAccount> LedgerAccounts => Set<LedgerAccount>();
        public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
        public DbSet<Posting> Postings => Set<Posting>();
        public DbSet<PaymentOrder> PaymentOrders => Set<PaymentOrder>();
        public DbSet<Beneficiary> Beneficiaries => Set<Beneficiary>();
        public DbSet<LegalDocument> LegalDocuments => Set<LegalDocument>();
        public DbSet<UserConsent> UserConsents => Set<UserConsent>();
        public DbSet<UserSession> UserSessions => Set<UserSession>();
        public DbSet<RecoveryCode> RecoveryCodes => Set<RecoveryCode>();
        public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
        public DbSet<CurrencyExchange> CurrencyExchanges => Set<CurrencyExchange>();
        public DbSet<StandingOrder> StandingOrders => Set<StandingOrder>();
        public DbSet<StandingOrderExecution> StandingOrderExecutions => Set<StandingOrderExecution>();
        public DbSet<UserPreferences> UserPreferences => Set<UserPreferences>();
        public DbSet<ConsentEvent> ConsentEvents => Set<ConsentEvent>();
        public DbSet<FeeScheduleEntry> FeeSchedule => Set<FeeScheduleEntry>();
        public DbSet<Complaint> Complaints => Set<Complaint>();
        public DbSet<ComplaintMessage> ComplaintMessages => Set<ComplaintMessage>();
        public DbSet<ComplaintStatusChange> ComplaintStatusHistory => Set<ComplaintStatusChange>();
        public DbSet<UserFeedback> UserFeedback => Set<UserFeedback>();
        public DbSet<FaqArticle> FaqArticles => Set<FaqArticle>();
        public DbSet<DataAccessLog> DataAccessLogs => Set<DataAccessLog>();
        public DbSet<PrivacyRequest> PrivacyRequests => Set<PrivacyRequest>();
        public DbSet<PasswordResetRequest> PasswordResetRequests => Set<PasswordResetRequest>();
        public DbSet<ComplianceAlert> ComplianceAlerts => Set<ComplianceAlert>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
                entity.Property(u => u.LastName).HasMaxLength(100).IsRequired();
                entity.Property(u => u.Email).HasMaxLength(255).IsRequired();
                entity.Property(u => u.PasswordHash).HasMaxLength(255).IsRequired();
                entity.Property(u => u.Role).HasConversion<string>();
                entity.Property(u => u.KycStatus).HasConversion<string>();
                entity.Property(u => u.RiskRating).HasConversion<string>();
                entity.HasIndex(u => u.Email).IsUnique();
                entity.Property(u => u.TotpSecretProtected).HasMaxLength(1024);
                entity.Ignore(u => u.FullName);
                entity.Ignore(u => u.TwoFactorEnabled);
            });

            modelBuilder.Entity<Account>(entity =>
            {
                entity.ToTable("accounts");
                entity.Property(a => a.AccountNumber).HasMaxLength(20).IsRequired();
                entity.Property(a => a.Currency).HasMaxLength(3).IsFixedLength().IsRequired();
                entity.Property(a => a.Status).HasConversion<string>();
                entity.Property(a => a.Balance).HasColumnType("decimal(18,2)");
                entity.HasIndex(a => a.AccountNumber).IsUnique();
                entity.Ignore(a => a.IsActive);
                entity.HasOne(a => a.User)
                      .WithMany(u => u.Accounts)
                      .HasForeignKey(a => a.UserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.ToTable("transactions");
                entity.Property(t => t.Type).HasConversion<string>();
                entity.Property(t => t.Status).HasConversion<string>();
                entity.Property(t => t.Amount).HasColumnType("decimal(18,2)");
                entity.Property(t => t.FeeAmount).HasColumnType("decimal(18,2)");
                entity.Property(t => t.FromBalanceAfter).HasColumnType("decimal(18,2)");
                entity.Property(t => t.ToBalanceAfter).HasColumnType("decimal(18,2)");
                entity.Property(t => t.Currency).HasMaxLength(3).IsFixedLength().IsRequired();
                entity.Property(t => t.Description).HasMaxLength(500);
                entity.Property(t => t.ReferenceNumber).HasMaxLength(50).IsRequired();
                entity.Property(t => t.IdempotencyKey).HasMaxLength(64);
                entity.HasIndex(t => t.ReferenceNumber).IsUnique();
                entity.HasIndex(t => new { t.PerformedByUserId, t.IdempotencyKey }).IsUnique();
                entity.HasOne(t => t.FromAccount)
                      .WithMany()
                      .HasForeignKey(t => t.FromAccountId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(t => t.ToAccount)
                      .WithMany()
                      .HasForeignKey(t => t.ToAccountId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(t => t.PerformedBy)
                      .WithMany()
                      .HasForeignKey(t => t.PerformedByUserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.ToTable("auditlogs");
                entity.Property(a => a.Action).HasMaxLength(255).IsRequired();
                entity.Property(a => a.EntityType).HasMaxLength(100);
                entity.Property(a => a.IpAddress).HasMaxLength(45);
                entity.HasOne(a => a.User)
                      .WithMany()
                      .HasForeignKey(a => a.UserId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Notification>(entity =>
            {
                entity.ToTable("notifications");
                entity.Property(n => n.Type).HasConversion<string>().HasMaxLength(40);
                entity.Property(n => n.Title).HasMaxLength(150).IsRequired();
                entity.Property(n => n.Message).HasMaxLength(500).IsRequired();
                entity.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });
            });

            modelBuilder.Entity<LedgerAccount>(entity =>
            {
                entity.ToTable("ledger_accounts");
                entity.Property(l => l.Code).HasMaxLength(40).IsRequired();
                entity.Property(l => l.Name).HasMaxLength(150).IsRequired();
                entity.Property(l => l.Type).HasConversion<string>();
                entity.Property(l => l.Currency).HasMaxLength(3).IsFixedLength().IsRequired();
                entity.HasIndex(l => l.Code).IsUnique();
                entity.HasIndex(l => l.AccountId).IsUnique();
                entity.HasOne(l => l.Account)
                      .WithMany()
                      .HasForeignKey(l => l.AccountId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<JournalEntry>(entity =>
            {
                entity.ToTable("journal_entries");
                entity.Property(j => j.EntryType).HasMaxLength(40).IsRequired();
                entity.Property(j => j.Description).HasMaxLength(255).IsRequired();
                entity.HasIndex(j => j.TransactionId).IsUnique();
                entity.HasMany(j => j.Postings)
                      .WithOne()
                      .HasForeignKey(p => p.JournalEntryId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.Navigation(j => j.Postings).UsePropertyAccessMode(PropertyAccessMode.Field);
            });

            modelBuilder.Entity<PaymentOrder>(entity =>
            {
                entity.ToTable("payment_orders");
                entity.Property(p => p.RecipientName).HasMaxLength(100).IsRequired();
                entity.Property(p => p.RecipientAccountNumber).HasMaxLength(20).IsRequired();
                entity.Property(p => p.RecipientAddress).HasMaxLength(150);
                entity.Property(p => p.Amount).HasColumnType("decimal(18,2)");
                entity.Property(p => p.FeeAmount).HasColumnType("decimal(18,2)");
                entity.Property(p => p.Currency).HasMaxLength(3).IsFixedLength().IsRequired();
                entity.Property(p => p.PaymentCode).HasMaxLength(3).IsFixedLength().IsRequired();
                entity.Property(p => p.Purpose).HasMaxLength(105).IsRequired();
                entity.Property(p => p.ReferenceModel).HasMaxLength(2).IsFixedLength();
                entity.Property(p => p.ReferenceNumber).HasMaxLength(25);
                entity.Property(p => p.Status).HasConversion<string>();
                entity.Property(p => p.RejectionReason).HasMaxLength(255);
                entity.Property(p => p.IdempotencyKey).HasMaxLength(64);
                entity.HasIndex(p => new { p.UserId, p.IdempotencyKey }).IsUnique();
                entity.Ignore(p => p.IsInternal);
                entity.Ignore(p => p.CanBeCancelled);
                entity.HasOne(p => p.FromAccount).WithMany().HasForeignKey(p => p.FromAccountId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(p => p.ToAccount).WithMany().HasForeignKey(p => p.ToAccountId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(p => p.Transaction)
                      .WithOne(t => t.PaymentOrder)
                      .HasForeignKey<PaymentOrder>(p => p.TransactionId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Beneficiary>(entity =>
            {
                entity.ToTable("beneficiaries");
                entity.Property(b => b.Nickname).HasMaxLength(60).IsRequired();
                entity.Property(b => b.RecipientName).HasMaxLength(100).IsRequired();
                entity.Property(b => b.AccountNumber).HasMaxLength(20).IsRequired();
                entity.Property(b => b.RecipientAddress).HasMaxLength(150);
                entity.Property(b => b.PaymentCode).HasMaxLength(3).IsFixedLength();
                entity.Property(b => b.Purpose).HasMaxLength(105);
                entity.Property(b => b.ReferenceModel).HasMaxLength(2).IsFixedLength();
                entity.Property(b => b.ReferenceNumber).HasMaxLength(25);
                entity.HasIndex(b => new { b.UserId, b.Nickname }).IsUnique();
            });

            modelBuilder.Entity<LegalDocument>(entity =>
            {
                entity.ToTable("legal_documents");
                entity.Property(d => d.DocumentType).HasConversion<string>();
                entity.Property(d => d.Version).HasMaxLength(20).IsRequired();
                entity.Property(d => d.Title).HasMaxLength(150).IsRequired();
                entity.Property(d => d.Content).HasColumnType("mediumtext").IsRequired();
                entity.HasIndex(d => new { d.DocumentType, d.Version }).IsUnique();
            });

            modelBuilder.Entity<UserConsent>(entity =>
            {
                entity.ToTable("user_consents");
                entity.Property(c => c.IpAddress).HasMaxLength(45);
                entity.HasIndex(c => new { c.UserId, c.LegalDocumentId }).IsUnique();
                entity.HasOne(c => c.LegalDocument).WithMany().HasForeignKey(c => c.LegalDocumentId).OnDelete(DeleteBehavior.Restrict);
            });

            ConfigureCustomerProtection(modelBuilder);

            modelBuilder.Entity<Posting>(entity =>
            {
                entity.ToTable("postings");
                entity.Property(p => p.Amount).HasColumnType("decimal(18,2)");
                entity.Property(p => p.BalanceAfter).HasColumnType("decimal(18,2)");
                entity.Property(p => p.Currency).HasMaxLength(3).IsFixedLength().IsRequired();
                entity.HasOne(p => p.LedgerAccount)
                      .WithMany()
                      .HasForeignKey(p => p.LedgerAccountId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }

        private static void ConfigureCustomerProtection(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserSession>(entity =>
            {
                entity.ToTable("user_sessions");
                entity.Property(s => s.Id).HasColumnType("char(36)");
                entity.Property(s => s.RevokedReason).HasMaxLength(60);
                entity.Property(s => s.IpAddress).HasMaxLength(45);
                entity.Property(s => s.UserAgent).HasMaxLength(255);
                entity.Property(s => s.DeviceLabel).HasMaxLength(100);
                entity.Property(s => s.RefreshTokenHash).HasColumnType("char(64)");
                entity.Property(s => s.PreviousRefreshTokenHash).HasColumnType("char(64)");
                entity.HasIndex(s => new { s.UserId, s.RevokedAt, s.ExpiresAt });
                entity.HasIndex(s => s.RefreshTokenHash).IsUnique();
                entity.HasIndex(s => s.PreviousRefreshTokenHash);
            });

            modelBuilder.Entity<ExchangeRate>(entity =>
            {
                entity.ToTable("exchange_rates");
                entity.Property(r => r.Currency).HasColumnType("char(3)").IsRequired();
                entity.Property(r => r.BuyRate).HasPrecision(18, 4);
                entity.Property(r => r.MiddleRate).HasPrecision(18, 4);
                entity.Property(r => r.SellRate).HasPrecision(18, 4);
                entity.HasIndex(r => new { r.Currency, r.ValidFrom });
            });

            modelBuilder.Entity<CurrencyExchange>(entity =>
            {
                entity.ToTable("currency_exchanges");
                entity.Property(x => x.ReferenceNumber).HasMaxLength(20).IsRequired();
                entity.Property(x => x.FromCurrency).HasColumnType("char(3)").IsRequired();
                entity.Property(x => x.ToCurrency).HasColumnType("char(3)").IsRequired();
                entity.Property(x => x.FromAmount).HasPrecision(18, 2);
                entity.Property(x => x.ToAmount).HasPrecision(18, 2);
                entity.Property(x => x.AppliedRate).HasPrecision(18, 4);
                entity.Property(x => x.MiddleRate).HasPrecision(18, 4);
                entity.Property(x => x.SpreadRsd).HasPrecision(18, 2);
                entity.Property(x => x.RateType).HasConversion<string>();
                entity.Property(x => x.IdempotencyKey).HasMaxLength(64);
                entity.HasIndex(x => x.ReferenceNumber).IsUnique();
                entity.HasIndex(x => new { x.UserId, x.IdempotencyKey }).IsUnique();
                entity.HasOne(x => x.FromAccount).WithMany().HasForeignKey(x => x.FromAccountId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.ToAccount).WithMany().HasForeignKey(x => x.ToAccountId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<StandingOrder>(entity =>
            {
                entity.ToTable("standing_orders");
                entity.Property(o => o.ReferenceNumber).HasMaxLength(20).IsRequired();
                entity.Property(o => o.RecipientName).HasMaxLength(150).IsRequired();
                entity.Property(o => o.RecipientAccountNumber).HasMaxLength(20).IsRequired();
                entity.Property(o => o.RecipientAddress).HasMaxLength(200);
                entity.Property(o => o.Amount).HasPrecision(18, 2);
                entity.Property(o => o.Currency).HasColumnType("char(3)").IsRequired();
                entity.Property(o => o.PaymentCode).HasColumnType("char(3)").IsRequired();
                entity.Property(o => o.Purpose).HasMaxLength(140).IsRequired();
                entity.Property(o => o.ReferenceModel).HasMaxLength(2);
                entity.Property(o => o.ReferenceNumberOnSlip).HasMaxLength(23);
                entity.Property(o => o.Frequency).HasConversion<string>();
                entity.Property(o => o.Status).HasConversion<string>();
                entity.Property(o => o.LastFailureReason).HasMaxLength(300);
                entity.HasIndex(o => o.ReferenceNumber).IsUnique();
                entity.HasIndex(o => new { o.Status, o.NextExecutionDate });
                entity.HasOne(o => o.FromAccount).WithMany().HasForeignKey(o => o.FromAccountId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne<User>().WithMany().HasForeignKey(o => o.UserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasMany(o => o.Executions).WithOne().HasForeignKey(e => e.StandingOrderId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<StandingOrderExecution>(entity =>
            {
                entity.ToTable("standing_order_executions");
                entity.Property(e => e.Status).HasConversion<string>();
                entity.Property(e => e.FailureReason).HasMaxLength(300);
                entity.HasIndex(e => new { e.StandingOrderId, e.ScheduledFor }).IsUnique();
                entity.HasOne(e => e.PaymentOrder).WithMany().HasForeignKey(e => e.PaymentOrderId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<RecoveryCode>(entity =>
            {
                entity.ToTable("user_recovery_codes");
                entity.Property(c => c.CodeHash).HasColumnType("char(64)").IsRequired();
                entity.HasIndex(c => new { c.UserId, c.CodeHash }).IsUnique();
                entity.HasOne<User>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<UserPreferences>(entity =>
            {
                entity.ToTable("user_preferences");
                entity.HasKey(p => p.UserId);
                entity.Property(p => p.UserId).ValueGeneratedNever();
                entity.Property(p => p.ExtraConfirmationThreshold).HasColumnType("decimal(18,2)");
            });

            modelBuilder.Entity<ConsentEvent>(entity =>
            {
                entity.ToTable("consent_events");
                entity.Property(c => c.ConsentType).HasMaxLength(60).IsRequired();
                entity.Property(c => c.Source).HasMaxLength(40).IsRequired();
                entity.Property(c => c.IpAddress).HasMaxLength(45);
            });

            modelBuilder.Entity<FeeScheduleEntry>(entity =>
            {
                entity.ToTable("fee_schedule");
                entity.Property(f => f.Code).HasMaxLength(40).IsRequired();
                entity.Property(f => f.Name).HasMaxLength(120).IsRequired();
                entity.Property(f => f.Description).HasMaxLength(500).IsRequired();
                entity.Property(f => f.AppliesTo).HasConversion<string>();
                entity.Property(f => f.CalculationType).HasConversion<string>();
                entity.Property(f => f.Amount).HasColumnType("decimal(18,4)");
                entity.Property(f => f.MinAmount).HasColumnType("decimal(18,2)");
                entity.Property(f => f.MaxAmount).HasColumnType("decimal(18,2)");
                entity.Property(f => f.Currency).HasMaxLength(3).IsFixedLength();
            });

            modelBuilder.Entity<Complaint>(entity =>
            {
                entity.ToTable("complaints");
                entity.Property(c => c.ReferenceNumber).HasMaxLength(20).IsRequired();
                entity.Property(c => c.Category).HasConversion<string>();
                entity.Property(c => c.Status).HasConversion<string>();
                entity.Property(c => c.Priority).HasConversion<string>();
                entity.Property(c => c.Subject).HasMaxLength(150).IsRequired();
                entity.Property(c => c.Description).HasColumnType("text").IsRequired();
                entity.Property(c => c.Resolution).HasColumnType("text");
                entity.Property(c => c.DeadlineExtensionReason).HasMaxLength(500);
                entity.HasIndex(c => c.ReferenceNumber).IsUnique();
                entity.Ignore(c => c.IsOpen);
                entity.HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(c => c.AssignedTo).WithMany().HasForeignKey(c => c.AssignedToUserId).OnDelete(DeleteBehavior.SetNull);
                entity.HasMany(c => c.Messages).WithOne().HasForeignKey(m => m.ComplaintId).OnDelete(DeleteBehavior.Cascade);
                entity.HasMany(c => c.StatusHistory).WithOne().HasForeignKey(h => h.ComplaintId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ComplaintMessage>(entity =>
            {
                entity.ToTable("complaint_messages");
                entity.Property(m => m.Message).HasColumnType("text").IsRequired();
                entity.HasOne(m => m.Author).WithMany().HasForeignKey(m => m.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ComplaintStatusChange>(entity =>
            {
                entity.ToTable("complaint_status_history");
                entity.Property(h => h.OldStatus).HasMaxLength(20);
                entity.Property(h => h.NewStatus).HasMaxLength(20).IsRequired();
                entity.Property(h => h.Note).HasMaxLength(500);
            });

            modelBuilder.Entity<UserFeedback>(entity =>
            {
                entity.ToTable("user_feedback");
                entity.Property(f => f.SurveyType).HasConversion<string>();
                entity.Property(f => f.Score).HasColumnType("tinyint");
                entity.Property(f => f.Comment).HasMaxLength(1000);
                entity.Property(f => f.Context).HasMaxLength(60).IsRequired();
                entity.HasIndex(f => new { f.UserId, f.SurveyType, f.Context }).IsUnique();
            });

            modelBuilder.Entity<FaqArticle>(entity =>
            {
                entity.ToTable("faq_articles");
                entity.Property(f => f.Category).HasMaxLength(60).IsRequired();
                entity.Property(f => f.Question).HasMaxLength(255).IsRequired();
                entity.Property(f => f.Answer).HasColumnType("text").IsRequired();
            });

            modelBuilder.Entity<DataAccessLog>(entity =>
            {
                entity.ToTable("data_access_logs");
                entity.Property(d => d.Resource).HasMaxLength(40).IsRequired();
                entity.Property(d => d.Purpose).HasMaxLength(120).IsRequired();
                entity.Property(d => d.IpAddress).HasMaxLength(45);
                entity.HasOne(d => d.Viewer).WithMany().HasForeignKey(d => d.ViewerUserId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<PrivacyRequest>(entity =>
            {
                entity.ToTable("privacy_requests");
                entity.Property(p => p.ReferenceNumber).HasMaxLength(20).IsRequired();
                entity.Property(p => p.RequestType).HasConversion<string>();
                entity.Property(p => p.Status).HasConversion<string>();
                entity.Property(p => p.Details).HasColumnType("text").IsRequired();
                entity.Property(p => p.Response).HasColumnType("text");
                entity.HasIndex(p => p.ReferenceNumber).IsUnique();
                entity.HasOne(p => p.User).WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<PasswordResetRequest>(entity =>
            {
                entity.ToTable("password_reset_requests");
                entity.Property(p => p.ReferenceNumber).HasMaxLength(20).IsRequired();
                entity.Property(p => p.NewPasswordHash).HasMaxLength(255);
                entity.Property(p => p.Status).HasConversion<string>();
                entity.Property(p => p.IpAddress).HasMaxLength(45);
                entity.Property(p => p.UserAgent).HasMaxLength(256);
                entity.Property(p => p.ReviewNote).HasMaxLength(500);
                entity.HasIndex(p => p.ReferenceNumber).IsUnique();
                entity.HasOne(p => p.User).WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(p => p.ReviewedBy).WithMany().HasForeignKey(p => p.ReviewedByUserId).OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<ComplianceAlert>(entity =>
            {
                entity.ToTable("compliance_alerts");
                entity.Property(a => a.RuleCode).HasMaxLength(40).IsRequired();
                entity.Property(a => a.Severity).HasConversion<string>();
                entity.Property(a => a.Status).HasConversion<string>();
                entity.Property(a => a.Details).HasMaxLength(1000).IsRequired();
                entity.Property(a => a.ReviewNote).HasMaxLength(1000);
                entity.HasIndex(a => new { a.RuleCode, a.TransactionId }).IsUnique();
                entity.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
