using System.Security.Cryptography;
using BankarskiSistem.Core.Banking;
using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using BankarskiSistem.Infrastructure.Data;
using BankarskiSistem.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BankarskiSistem.Infrastructure.Services
{
    public class PasswordResetService : IPasswordResetService
    {
        public static readonly TimeSpan RequestLifetime = TimeSpan.FromHours(72);
        private const int MaxRequestsPerDay = 3;

        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ISessionService _sessionService;
        private readonly ISessionValidationCache _sessionCache;
        private readonly INotificationPublisher _notificationPublisher;
        private readonly IRequestContext _requestContext;
        private readonly TimeProvider _timeProvider;

        public PasswordResetService(
            ApplicationDbContext context,
            IPasswordHasher passwordHasher,
            ISessionService sessionService,
            ISessionValidationCache sessionCache,
            INotificationPublisher notificationPublisher,
            IRequestContext requestContext,
            TimeProvider timeProvider)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _sessionService = sessionService;
            _sessionCache = sessionCache;
            _notificationPublisher = notificationPublisher;
            _requestContext = requestContext;
            _timeProvider = timeProvider;
        }

        private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

        public async Task<PasswordResetRequest?> SubmitAsync(string email, string newPassword)
        {
            var newPasswordHash = _passwordHasher.Hash(newPassword);
            var normalized = EmailNormalizer.Normalize(email);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalized);
            if (user == null || !user.IsActive)
                return null;

            var since = UtcNow.AddDays(-1);
            var recent = await _context.PasswordResetRequests.CountAsync(r => r.UserId == user.Id && r.CreatedAt >= since);
            if (recent >= MaxRequestsPerDay)
                return null;

            var pending = await _context.PasswordResetRequests
                .Where(r => r.UserId == user.Id && r.Status == PasswordResetStatus.Podnet)
                .ToListAsync();
            foreach (var previous in pending)
                Close(previous, PasswordResetStatus.Istekao, "Zamenjen novim zahtevom.");

            var request = new PasswordResetRequest
            {
                ReferenceNumber = $"LOZ-{UtcNow:yyyyMMdd}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(3))}",
                UserId = user.Id,
                NewPasswordHash = newPasswordHash,
                Status = PasswordResetStatus.Podnet,
                IpAddress = _requestContext.IpAddress,
                UserAgent = Truncate(_requestContext.UserAgent, 256),
                ExpiresAt = UtcNow.Add(RequestLifetime),
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            };

            await _context.PasswordResetRequests.AddAsync(request);
            await _notificationPublisher.PublishAsync(new Notification
            {
                UserId = user.Id,
                Type = NotificationType.Bezbednost,
                Title = "Primljen je zahtev za novu lozinku",
                Message = $"Zahtev {request.ReferenceNumber} čeka odobrenje banke. Ako ga niste vi poslali, odmah se obratite banci.",
                CreatedAt = UtcNow
            });
            await _context.SaveChangesAsync();
            return request;
        }

        public async Task<PagedResult<PasswordResetRequest>> GetPagedAsync(PasswordResetStatus? status, PageRequest page, Actor actor)
        {
            EnsureAdministrator(actor);
            await ExpireStaleAsync();

            var query = _context.PasswordResetRequests.AsNoTracking()
                .Include(r => r.User)
                .Include(r => r.ReviewedBy)
                .AsQueryable();
            if (status.HasValue)
                query = query.Where(r => r.Status == status.Value);

            return await query.OrderByDescending(r => r.Status == PasswordResetStatus.Podnet)
                .ThenByDescending(r => r.CreatedAt)
                .ToPagedResultAsync(page);
        }

        public async Task<int> CountPendingAsync(Actor actor)
        {
            EnsureAdministrator(actor);
            await ExpireStaleAsync();
            return await _context.PasswordResetRequests.CountAsync(r => r.Status == PasswordResetStatus.Podnet);
        }

        public async Task<PasswordResetRequest> ApproveAsync(int requestId, string? note, Actor actor)
        {
            var request = await LoadPendingAsync(requestId, actor);
            var user = request.User!;

            user.PasswordHash = request.NewPasswordHash!;
            user.InvalidateTokens();
            user.FailedLoginAttempts = 0;
            user.IsLocked = false;
            user.LockedUntil = null;
            user.UpdatedAt = UtcNow;

            Close(request, PasswordResetStatus.Odobren, string.IsNullOrWhiteSpace(note) ? null : note.Trim(), actor.UserId);
            await _notificationPublisher.PublishAsync(new Notification
            {
                UserId = user.Id,
                Type = NotificationType.Bezbednost,
                Title = "Nova lozinka je aktivirana",
                Message = $"Banka je odobrila zahtev {request.ReferenceNumber}. Prijavite se novom lozinkom; sve ranije sesije su odjavljene.",
                CreatedAt = UtcNow
            });
            await _context.SaveChangesAsync();
            await _context.Entry(request).Reference(r => r.ReviewedBy).LoadAsync();

            _sessionCache.InvalidateUser(user.Id);
            await _sessionService.RevokeAllAsync(user.Id, "Reset lozinke");
            return request;
        }

        public async Task<PasswordResetRequest> RejectAsync(int requestId, string note, Actor actor)
        {
            if (string.IsNullOrWhiteSpace(note))
                throw new BusinessRuleException("Razlog odbijanja je obavezan.");

            var request = await LoadPendingAsync(requestId, actor);
            Close(request, PasswordResetStatus.Odbijen, note.Trim(), actor.UserId);
            await _notificationPublisher.PublishAsync(new Notification
            {
                UserId = request.UserId,
                Type = NotificationType.Bezbednost,
                Title = "Zahtev za novu lozinku je odbijen",
                Message = $"Zahtev {request.ReferenceNumber} nije odobren. Lozinka nije promenjena. Za pomoć se obratite banci.",
                CreatedAt = UtcNow
            });
            await _context.SaveChangesAsync();
            await _context.Entry(request).Reference(r => r.ReviewedBy).LoadAsync();
            return request;
        }

        private async Task<PasswordResetRequest> LoadPendingAsync(int requestId, Actor actor)
        {
            EnsureAdministrator(actor);

            var request = await _context.PasswordResetRequests
                .Include(r => r.User)
                .Include(r => r.ReviewedBy)
                .FirstOrDefaultAsync(r => r.Id == requestId)
                ?? throw new NotFoundException("Zahtev nije pronađen.");

            if (request.UserId == actor.UserId)
                throw new BusinessRuleException("Ne možete odlučivati o zahtevu za sopstvenu lozinku. Odobriti ga mora drugi administrator.");
            if (request.Status != PasswordResetStatus.Podnet)
                throw new BusinessRuleException("O ovom zahtevu je već odlučeno.");
            if (request.ExpiresAt <= UtcNow)
            {
                Close(request, PasswordResetStatus.Istekao, "Rok za odobrenje je istekao.");
                await _context.SaveChangesAsync();
                throw new BusinessRuleException("Zahtev je istekao. Korisnik treba da pošalje novi zahtev.");
            }
            if (request.User is not { IsActive: true })
                throw new BusinessRuleException("Nalog korisnika nije aktivan.");

            return request;
        }

        private async Task ExpireStaleAsync()
        {
            var now = UtcNow;
            var stale = await _context.PasswordResetRequests
                .Where(r => r.Status == PasswordResetStatus.Podnet && r.ExpiresAt <= now)
                .ToListAsync();
            if (stale.Count == 0)
                return;

            foreach (var request in stale)
                Close(request, PasswordResetStatus.Istekao, "Rok za odobrenje je istekao.");
            await _context.SaveChangesAsync();
        }

        private void Close(PasswordResetRequest request, PasswordResetStatus status, string? note, int? reviewerId = null)
        {
            request.Status = status;
            request.NewPasswordHash = null;
            request.ReviewNote = note;
            request.UpdatedAt = UtcNow;
            if (status is PasswordResetStatus.Odobren or PasswordResetStatus.Odbijen)
            {
                request.ReviewedByUserId = reviewerId;
                request.ReviewedAt = UtcNow;
            }
        }

        private static void EnsureAdministrator(Actor actor)
        {
            if (!actor.IsAdministrator)
                throw new ForbiddenException();
        }

        private static string? Truncate(string? value, int length) =>
            value is null || value.Length <= length ? value : value[..length];
    }
}
