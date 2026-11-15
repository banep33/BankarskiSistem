using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Settings;
using BankarskiSistem.Infrastructure.Data;
using BankarskiSistem.Infrastructure.Repositories;
using BankarskiSistem.Infrastructure.Security;
using BankarskiSistem.Infrastructure.Services;
using BankarskiSistem.Infrastructure.Services.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BankarskiSistem.Infrastructure
{
    public static class DependencyInjection
    {
        public static readonly MySqlServerVersion DefaultServerVersion = new(new Version(8, 0, 36));

        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException(
                    "ConnectionStrings:DefaultConnection nije podesen. Koristite user-secrets ili promenljivu okruzenja.");

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseMySql(connectionString, DefaultServerVersion));

            services.AddOptions<JwtSettings>().Bind(configuration.GetSection(JwtSettings.SectionName));
            services.AddOptions<SecuritySettings>().Bind(configuration.GetSection(SecuritySettings.SectionName));
            services.AddOptions<BankingSettings>().Bind(configuration.GetSection(BankingSettings.SectionName));
            services.AddOptions<CustomerProtectionSettings>().Bind(configuration.GetSection(CustomerProtectionSettings.SectionName));
            services.AddOptions<ComplianceSettings>().Bind(configuration.GetSection(ComplianceSettings.SectionName));
            services.AddOptions<RetentionSettings>().Bind(configuration.GetSection(RetentionSettings.SectionName));

            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
            services.AddSingleton<ITokenService, JwtTokenService>();
            services.AddSingleton<IAccountNumberGenerator, AccountNumberGenerator>();
            services.AddSingleton<IBusinessCalendar, BusinessCalendar>();
            services.AddSingleton<ISessionValidationCache, SessionValidationCache>();
            services.AddMemoryCache();

            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IAccountRepository, AccountRepository>();
            services.AddScoped<ITransactionRepository, TransactionRepository>();
            services.AddScoped<INotificationRepository, NotificationRepository>();
            services.AddScoped<IAuditLogRepository, AuditLogRepository>();
            services.AddScoped<ILedgerRepository, LedgerRepository>();
            services.AddScoped<IPaymentOrderRepository, PaymentOrderRepository>();
            services.AddScoped<IBeneficiaryRepository, BeneficiaryRepository>();
            services.AddScoped<ILegalRepository, LegalRepository>();

            services.AddScoped<DailyLimitPolicy>();
            services.AddScoped<LedgerRecorder>();

            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IAccountService, AccountService>();
            services.AddScoped<IPaymentService, PaymentService>();
            services.AddScoped<ITransactionService, TransactionService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IStatisticsService, StatisticsService>();
            services.AddScoped<IAuditLogService, AuditLogService>();
            services.AddScoped<IPaymentOrderService, PaymentOrderService>();
            services.AddScoped<IBeneficiaryService, BeneficiaryService>();
            services.AddScoped<ILegalService, LegalService>();
            services.AddScoped<IPrivacyService, PrivacyService>();
            services.AddScoped<ILedgerQueryService, LedgerQueryService>();
            services.AddScoped<INotificationPublisher, NotificationPublisher>();
            services.AddScoped<ISessionService, SessionService>();
            services.AddScoped<IFeeService, FeeService>();
            services.AddScoped<IComplianceMonitor, ComplianceMonitor>();
            services.AddScoped<IComplianceService, ComplianceService>();
            services.AddScoped<IComplaintService, ComplaintService>();
            services.AddScoped<IFeedbackService, FeedbackService>();
            services.AddScoped<IFaqService, FaqService>();
            services.AddScoped<IDataAccessLogger, DataAccessLogger>();
            services.AddScoped<IPrivacyRequestService, PrivacyRequestService>();
            services.AddScoped<IPasswordResetService, PasswordResetService>();
            services.AddScoped<ITwoFactorService, TwoFactorService>();
            services.AddScoped<IStandingOrderService, StandingOrderService>();
            services.AddScoped<ICurrencyExchangeService, CurrencyExchangeService>();
            services.AddScoped<IReversalService, ReversalService>();
            services.AddScoped<IStatementService, StatementService>();
            services.AddScoped<IBudgetService, BudgetService>();
            services.AddScoped<IPreferencesService, PreferencesService>();
            services.AddScoped<IDataRetentionService, DataRetentionService>();

            return services;
        }
    }
}
