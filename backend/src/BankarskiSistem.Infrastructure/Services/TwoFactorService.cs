using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using BankarskiSistem.Infrastructure.Data;
using BankarskiSistem.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace BankarskiSistem.Infrastructure.Services
{
    public class TwoFactorService : ITwoFactorService
    {
        public const string Issuer = "Bankarski Sistem";
        public const int RecoveryCodeCount = 10;

        private readonly ApplicationDbContext _context;
        private readonly ISecretProtector _protector;
        private readonly ISessionService _sessionService;
        private readonly ISessionValidationCache _sessionCache;
        private readonly INotificationPublisher _notificationPublisher;
        private readonly TimeProvider _timeProvider;

        public TwoFactorService(
            ApplicationDbContext context,
            ISecretProtector protector,
            ISessionService sessionService,
            ISessionValidationCache sessionCache,
            INotificationPublisher notificationPublisher,
            TimeProvider timeProvider)
        {
            _context = context;
            _protector = protector;
            _sessionService = sessionService;
            _sessionCache = sessionCache;
            _notificationPublisher = notificationPublisher;
            _timeProvider = timeProvider;
        }

        private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

        public async Task<TwoFactorStatus> GetStatusAsync(int userId)
        {
            var user = await FindUserAsync(userId);
            var remaining = await _context.RecoveryCodes.CountAsync(c => c.UserId == userId && c.UsedAt == null);
            return new TwoFactorStatus(user.TwoFactorEnabled, user.TotpEnabledAt, user.TwoFactorEnabled ? remaining : 0);
        }

        public async Task<TwoFactorSetup> BeginSetupAsync(int userId)
        {
            var user = await FindUserAsync(userId);
            if (user.TwoFactorEnabled)
                throw new BusinessRuleException("Dvostepena prijava je već uključena. Isključite je pre ponovnog podešavanja.");

            var secret = Totp.GenerateSecret();
            user.TotpSecretProtected = _protector.Protect(secret);
            user.TotpLastUsedStep = null;
            user.UpdatedAt = UtcNow;
            await _context.SaveChangesAsync();

            return new TwoFactorSetup(secret, Totp.BuildOtpAuthUri(Issuer, user.Email, secret));
        }

        public async Task<IReadOnlyList<string>> EnableAsync(int userId, string code)
        {
            var user = await FindUserAsync(userId);
            if (user.TwoFactorEnabled)
                throw new BusinessRuleException("Dvostepena prijava je već uključena.");
            if (user.TotpSecretProtected == null)
                throw new BusinessRuleException("Prvo pokrenite podešavanje i skenirajte QR kod.");

            if (!ConsumeTotp(user, code))
                throw new BusinessRuleException("Kod nije ispravan. Proverite vreme na telefonu i unesite trenutni kod iz aplikacije.");

            user.TotpEnabledAt = UtcNow;
            user.UpdatedAt = UtcNow;
            var codes = await ReplaceRecoveryCodesAsync(userId);
            await _notificationPublisher.PublishAsync(Notify(userId, "Dvostepena prijava je uključena",
                "Za prijavu je od sada potreban i kod iz aplikacije za autentifikaciju. Rezervne kodove čuvajte na sigurnom mestu."));
            await _context.SaveChangesAsync();
            return codes;
        }

        public async Task DisableAsync(int userId, string code)
        {
            var user = await FindUserAsync(userId);
            if (!user.TwoFactorEnabled)
                throw new BusinessRuleException("Dvostepena prijava nije uključena.");
            if (!ConsumeTotp(user, code) && !await ConsumeRecoveryCodeAsync(user, code))
                throw new BusinessRuleException("Kod nije ispravan.");

            await ClearAsync(user);
            await _notificationPublisher.PublishAsync(Notify(userId, "Dvostepena prijava je isključena",
                "Za prijavu je ponovo dovoljna samo lozinka. Ako to niste bili vi, odmah promenite lozinku i obratite se banci."));
            await _context.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<string>> RegenerateRecoveryCodesAsync(int userId, string code)
        {
            var user = await FindUserAsync(userId);
            if (!user.TwoFactorEnabled)
                throw new BusinessRuleException("Dvostepena prijava nije uključena.");
            if (!ConsumeTotp(user, code))
                throw new BusinessRuleException("Kod nije ispravan.");

            var codes = await ReplaceRecoveryCodesAsync(userId);
            await _notificationPublisher.PublishAsync(Notify(userId, "Napravljeni su novi rezervni kodovi",
                "Prethodni rezervni kodovi više ne važe."));
            await _context.SaveChangesAsync();
            return codes;
        }

        public async Task<bool> VerifyLoginCodeAsync(User user, string code)
        {
            var tracked = await FindUserAsync(user.Id);
            if (!tracked.TwoFactorEnabled)
                return false;

            if (ConsumeTotp(tracked, code))
            {
                await _context.SaveChangesAsync();
                return true;
            }

            if (!await ConsumeRecoveryCodeAsync(tracked, code))
                return false;

            var remaining = await _context.RecoveryCodes.CountAsync(c => c.UserId == user.Id && c.UsedAt == null);
            await _notificationPublisher.PublishAsync(Notify(user.Id, "Iskorišćen je rezervni kod",
                $"Prijava je potvrđena rezervnim kodom. Preostalo kodova: {remaining}. Ako to niste bili vi, odmah se obratite banci."));
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task ResetByAdministratorAsync(int userId, Actor actor)
        {
            if (!actor.IsAdministrator)
                throw new ForbiddenException();
            if (actor.UserId == userId)
                throw new BusinessRuleException("Sopstvenu dvostepenu prijavu isključite u odeljku Bezbednost.");

            var user = await FindUserAsync(userId);
            if (!user.TwoFactorEnabled && user.TotpSecretProtected == null)
                throw new BusinessRuleException("Korisnik nema uključenu dvostepenu prijavu.");

            await ClearAsync(user);
            user.InvalidateTokens();
            await _notificationPublisher.PublishAsync(Notify(userId, "Banka je isključila dvostepenu prijavu",
                "Na vaš zahtev dvostepena prijava je isključena i sve sesije su odjavljene. Preporučujemo da je ponovo uključite."));
            await _context.SaveChangesAsync();

            _sessionCache.InvalidateUser(userId);
            await _sessionService.RevokeAllAsync(userId, "Reset dvostepene prijave");
        }

        private bool ConsumeTotp(User user, string code)
        {
            if (user.TotpSecretProtected == null || string.IsNullOrWhiteSpace(code))
                return false;

            var secret = _protector.Unprotect(user.TotpSecretProtected);
            var step = Totp.FindMatchingStep(secret, code, _timeProvider.GetUtcNow(), user.TotpLastUsedStep);
            if (step == null)
                return false;

            user.TotpLastUsedStep = step;
            user.UpdatedAt = UtcNow;
            return true;
        }

        private async Task<bool> ConsumeRecoveryCodeAsync(User user, string code)
        {
            var normalized = OpaqueTokens.NormalizeRecoveryCode(code ?? string.Empty);
            if (normalized.Length != 10)
                return false;

            var hash = OpaqueTokens.Hash(normalized);
            var match = await _context.RecoveryCodes.FirstOrDefaultAsync(c => c.UserId == user.Id && c.CodeHash == hash && c.UsedAt == null);
            if (match == null)
                return false;

            match.UsedAt = UtcNow;
            return true;
        }

        private async Task<IReadOnlyList<string>> ReplaceRecoveryCodesAsync(int userId)
        {
            await _context.RecoveryCodes.Where(c => c.UserId == userId).ExecuteDeleteAsync();

            var codes = new List<string>(RecoveryCodeCount);
            while (codes.Count < RecoveryCodeCount)
            {
                var code = OpaqueTokens.GenerateRecoveryCode();
                if (codes.Contains(code))
                    continue;
                codes.Add(code);
                await _context.RecoveryCodes.AddAsync(new RecoveryCode
                {
                    UserId = userId,
                    CodeHash = OpaqueTokens.Hash(OpaqueTokens.NormalizeRecoveryCode(code)),
                    CreatedAt = UtcNow
                });
            }
            return codes;
        }

        private async Task ClearAsync(User user)
        {
            user.TotpSecretProtected = null;
            user.TotpEnabledAt = null;
            user.TotpLastUsedStep = null;
            user.UpdatedAt = UtcNow;
            await _context.RecoveryCodes.Where(c => c.UserId == user.Id).ExecuteDeleteAsync();
        }

        private Notification Notify(int userId, string title, string message) => new()
        {
            UserId = userId,
            Type = NotificationType.Bezbednost,
            Title = title,
            Message = message,
            CreatedAt = UtcNow
        };

        private async Task<User> FindUserAsync(int userId) =>
            await _context.Users.FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new NotFoundException("Korisnik nije pronađen.");
    }
}
