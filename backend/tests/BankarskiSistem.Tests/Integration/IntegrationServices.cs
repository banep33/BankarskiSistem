using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Settings;
using BankarskiSistem.Infrastructure.Data;
using BankarskiSistem.Infrastructure.Repositories;
using BankarskiSistem.Infrastructure.Security;
using BankarskiSistem.Infrastructure.Services;
using BankarskiSistem.Infrastructure.Services.Payments;
using BankarskiSistem.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BankarskiSistem.Tests.Integration
{
    public sealed class TestRequestContext : IRequestContext
    {
        public string? IpAddress { get; init; } = "127.0.0.1";
        public string? UserAgent { get; init; } = "Mozilla/5.0 (Windows NT 10.0) Chrome/130.0";
    }

    public sealed class IntegrationServices
    {
        private readonly ApplicationDbContext _context;
        private readonly IOptions<SecuritySettings> _security;
        private readonly IOptions<CustomerProtectionSettings> _protection;
        private readonly IOptions<ComplianceSettings> _compliance;
        private readonly IRequestContext _requestContext;

        public IntegrationServices(
            ApplicationDbContext context,
            SecuritySettings? security = null,
            CustomerProtectionSettings? protection = null,
            ComplianceSettings? compliance = null,
            IRequestContext? requestContext = null)
        {
            _context = context;
            _security = Options.Create(security ?? new SecuritySettings { MaxTransactionsPerDay = 1000, DefaultDailyLimit = 100_000_000 });
            _protection = Options.Create(protection ?? new CustomerProtectionSettings());
            _compliance = Options.Create(compliance ?? new ComplianceSettings());
            _requestContext = requestContext ?? new TestRequestContext();
        }

        private TimeProvider Time => TimeProvider.System;
        private BusinessCalendar Calendar => new(Time, _security);
        private UnitOfWork UnitOfWork => new(_context);
        private TransactionRepository Transactions => new(_context);
        private LedgerRepository Ledger => new(_context, Time);
        private DailyLimitPolicy Limits => new(Transactions, Calendar, _security);
        private LedgerRecorder Recorder => new(Transactions, Ledger, UnitOfWork, Time);

        public NotificationPublisher Publisher => new(_context);
        public ComplianceMonitor ComplianceMonitor => new(_context, _compliance, Time);
        public FeeService Fees => new(_context, Calendar);
        public PreferencesService Preferences => new(_context, _requestContext, Time);

        public PaymentService Payments => new(
            new AccountRepository(_context), Transactions, Publisher, ComplianceMonitor, Ledger, UnitOfWork, Limits, Recorder, Time);

        public PaymentOrderService PaymentOrders => new(
            new PaymentOrderRepository(_context),
            new AccountRepository(_context),
            new BeneficiaryRepository(_context),
            Publisher,
            Ledger,
            UnitOfWork,
            Limits,
            Recorder,
            Fees,
            ComplianceMonitor,
            Preferences,
            _context,
            Calendar,
            Options.Create(new BankingSettings()),
            _protection,
            Time,
            NullLogger<PaymentOrderService>.Instance);

        public ComplaintService Complaints => new(_context, Publisher, _protection, Time);
        public FeedbackService Feedback => new(_context, Time);
        public DataAccessLogger DataAccess => new(_context, _requestContext, Time);
        public SessionService Sessions => new(_context, _requestContext, new BCryptPasswordHasher(), Publisher, new SessionValidationCache(_protection, Time), _protection, Time);
        public ComplianceService Compliance => new(_context, Time);
        public ReversalService Reversals => new(_context, new AccountRepository(_context), Recorder, UnitOfWork, Publisher, Time);
        public CurrencyExchangeService CurrencyExchange => new(_context, new AccountRepository(_context), Ledger, Recorder, UnitOfWork, Publisher, Time);
        public StandingOrderService StandingOrders => new(_context, PaymentOrders, Publisher, UnitOfWork, Calendar, Time,
            NullLogger<StandingOrderService>.Instance);
        public TwoFactorService TwoFactor => new(_context, new PlainSecretProtector(), Sessions,
            new SessionValidationCache(_protection, Time), Publisher, Time);
        public PasswordResetService PasswordResets => new(_context, new BCryptPasswordHasher(), Sessions,
            new SessionValidationCache(_protection, Time), Publisher, _requestContext, Time);
    }
}
