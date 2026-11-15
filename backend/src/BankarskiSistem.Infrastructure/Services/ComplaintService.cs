using System.Security.Cryptography;
using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using BankarskiSistem.Core.Settings;
using BankarskiSistem.Infrastructure.Data;
using BankarskiSistem.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BankarskiSistem.Infrastructure.Services
{
    public class ComplaintService : IComplaintService
    {
        private const int MaxOpenComplaintsPerUser = 10;

        private static readonly Dictionary<ComplaintStatus, ComplaintStatus[]> AllowedTransitions = new()
        {
            [ComplaintStatus.Podnet] = [ComplaintStatus.UObradi, ComplaintStatus.Resen, ComplaintStatus.Odbijen],
            [ComplaintStatus.UObradi] = [ComplaintStatus.CekaKorisnika, ComplaintStatus.Resen, ComplaintStatus.Odbijen],
            [ComplaintStatus.CekaKorisnika] = [ComplaintStatus.UObradi, ComplaintStatus.Resen, ComplaintStatus.Odbijen],
            [ComplaintStatus.Resen] = [ComplaintStatus.Zatvoren],
            [ComplaintStatus.Odbijen] = [ComplaintStatus.Zatvoren],
            [ComplaintStatus.Zatvoren] = []
        };

        private readonly ApplicationDbContext _context;
        private readonly INotificationPublisher _notificationPublisher;
        private readonly CustomerProtectionSettings _settings;
        private readonly TimeProvider _timeProvider;

        public ComplaintService(
            ApplicationDbContext context,
            INotificationPublisher notificationPublisher,
            IOptions<CustomerProtectionSettings> settings,
            TimeProvider timeProvider)
        {
            _context = context;
            _notificationPublisher = notificationPublisher;
            _settings = settings.Value;
            _timeProvider = timeProvider;
        }

        private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

        public async Task<Complaint> SubmitAsync(int userId, SubmitComplaintCommand command)
        {
            var openCount = await _context.Complaints.CountAsync(c =>
                c.UserId == userId && (c.Status == ComplaintStatus.Podnet || c.Status == ComplaintStatus.UObradi || c.Status == ComplaintStatus.CekaKorisnika));
            if (openCount >= MaxOpenComplaintsPerUser)
                throw new BusinessRuleException($"Imate {openCount} otvorenih prigovora. Sačekajte rešavanje postojećih pre podnošenja novog.");

            if (command.RelatedAccountId.HasValue
                && !await _context.Accounts.AnyAsync(a => a.Id == command.RelatedAccountId && a.UserId == userId))
                throw new BusinessRuleException("Navedeni račun ne pripada vama.");

            if (command.RelatedTransactionId.HasValue
                && !await _context.Transactions.AnyAsync(t => t.Id == command.RelatedTransactionId
                    && ((t.FromAccount != null && t.FromAccount.UserId == userId) || (t.ToAccount != null && t.ToAccount.UserId == userId))))
                throw new BusinessRuleException("Navedena transakcija nije vaša.");

            var complaint = new Complaint
            {
                ReferenceNumber = $"PRG-{UtcNow:yyyyMMdd}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(3))}",
                UserId = userId,
                Category = command.Category,
                Subject = command.Subject.Trim(),
                Description = command.Description.Trim(),
                RelatedTransactionId = command.RelatedTransactionId,
                RelatedAccountId = command.RelatedAccountId,
                Status = ComplaintStatus.Podnet,
                DueAt = UtcNow.AddDays(_settings.ComplaintResponseDays),
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            };
            complaint.StatusHistory.Add(new ComplaintStatusChange
            {
                NewStatus = ComplaintStatus.Podnet.ToString(),
                ChangedByUserId = userId,
                Note = "Prigovor podnet putem aplikacije",
                ChangedAt = UtcNow
            });

            await _context.Complaints.AddAsync(complaint);
            await _notificationPublisher.PublishAsync(new Notification
            {
                UserId = userId,
                Type = NotificationType.Prigovor,
                Title = "Prigovor je primljen",
                Message = $"Vaš prigovor {complaint.ReferenceNumber} je zaveden. Rok za odgovor je {complaint.DueAt:dd.MM.yyyy}.",
                CreatedAt = UtcNow
            });
            await _context.SaveChangesAsync();
            return complaint;
        }

        public Task<PagedResult<Complaint>> GetMineAsync(int userId, PageRequest page) =>
            _context.Complaints.AsNoTracking()
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.CreatedAt)
                .ToPagedResultAsync(page);

        public async Task<Complaint> GetMineAsync(int userId, int complaintId)
        {
            var complaint = await LoadAsync(complaintId);
            if (complaint == null || complaint.UserId != userId)
                throw new NotFoundException("Prigovor nije pronađen.");

            complaint.Messages = complaint.Messages.Where(m => !m.IsInternalNote).ToList();
            return complaint;
        }

        public async Task<Complaint> AddCustomerMessageAsync(int userId, int complaintId, string message)
        {
            var complaint = await LoadAsync(complaintId);
            if (complaint == null || complaint.UserId != userId)
                throw new NotFoundException("Prigovor nije pronađen.");
            if (!complaint.IsOpen)
                throw new BusinessRuleException("Na zatvoren prigovor ne možete dodati poruku. Podnesite novi prigovor.");

            complaint.Messages.Add(new ComplaintMessage
            {
                AuthorUserId = userId,
                IsFromStaff = false,
                Message = message.Trim(),
                CreatedAt = UtcNow
            });

            if (complaint.Status == ComplaintStatus.CekaKorisnika)
                Transition(complaint, ComplaintStatus.UObradi, userId, "Korisnik je dostavio odgovor");

            complaint.UpdatedAt = UtcNow;
            await _context.SaveChangesAsync();
            return await GetMineAsync(userId, complaintId);
        }

        public Task<PagedResult<Complaint>> GetPagedAsync(ComplaintFilter filter, PageRequest page, Actor actor)
        {
            EnsureStaff(actor);
            var now = UtcNow;
            var query = _context.Complaints.AsNoTracking().Include(c => c.User).Include(c => c.AssignedTo).AsQueryable();

            if (filter.Status.HasValue)
                query = query.Where(c => c.Status == filter.Status.Value);
            if (filter.AssignedToUserId.HasValue)
                query = query.Where(c => c.AssignedToUserId == filter.AssignedToUserId.Value);
            if (filter.OnlyUnassigned)
                query = query.Where(c => c.AssignedToUserId == null);
            if (filter.OnlyOverdue)
                query = query.Where(c => c.DueAt < now
                    && (c.Status == ComplaintStatus.Podnet || c.Status == ComplaintStatus.UObradi || c.Status == ComplaintStatus.CekaKorisnika));

            return query.OrderBy(c => c.DueAt).ThenBy(c => c.Id).ToPagedResultAsync(page);
        }

        public async Task<Complaint> GetForStaffAsync(int complaintId, Actor actor)
        {
            EnsureStaff(actor);
            return await LoadAsync(complaintId) ?? throw new NotFoundException("Prigovor nije pronađen.");
        }

        public async Task<Complaint> AssignAsync(int complaintId, int? assigneeUserId, Actor actor)
        {
            var complaint = await GetForStaffAsync(complaintId, actor);

            if (assigneeUserId.HasValue && !await _context.Users.AnyAsync(u =>
                    u.Id == assigneeUserId && u.IsActive && (u.Role == UserRole.Bankar || u.Role == UserRole.Administrator)))
                throw new BusinessRuleException("Prigovor se može dodeliti samo aktivnom zaposlenom.");

            complaint.AssignedToUserId = assigneeUserId;
            if (complaint.Status == ComplaintStatus.Podnet && assigneeUserId.HasValue)
                Transition(complaint, ComplaintStatus.UObradi, actor.UserId, "Prigovor dodeljen u rad");

            complaint.UpdatedAt = UtcNow;
            await _context.SaveChangesAsync();
            return await GetForStaffAsync(complaintId, actor);
        }

        public async Task<Complaint> ChangeStatusAsync(int complaintId, ComplaintStatus status, string? note, Actor actor)
        {
            var complaint = await GetForStaffAsync(complaintId, actor);

            if (!AllowedTransitions[complaint.Status].Contains(status))
                throw new BusinessRuleException($"Prelaz iz statusa {complaint.Status} u {status} nije dozvoljen.");

            if (status is ComplaintStatus.Resen or ComplaintStatus.Odbijen)
            {
                if (string.IsNullOrWhiteSpace(note))
                    throw new BusinessRuleException("Za rešavanje ili odbijanje prigovora obavezno je obrazloženje korisniku.");
                complaint.Resolution = note.Trim();
                complaint.ResolvedAt = UtcNow;
                complaint.Messages.Add(new ComplaintMessage
                {
                    AuthorUserId = actor.UserId,
                    IsFromStaff = true,
                    Message = note.Trim() + Environment.NewLine + Environment.NewLine +
                              "Ako niste zadovoljni odgovorom, možete se obratiti Narodnoj banci Srbije.",
                    CreatedAt = UtcNow
                });
            }

            Transition(complaint, status, actor.UserId, note);
            complaint.UpdatedAt = UtcNow;

            await _notificationPublisher.PublishAsync(new Notification
            {
                UserId = complaint.UserId,
                Type = NotificationType.Prigovor,
                Title = "Promenjen status prigovora",
                Message = $"Prigovor {complaint.ReferenceNumber} je sada u statusu: {StatusLabel(status)}.",
                CreatedAt = UtcNow
            });

            await _context.SaveChangesAsync();
            return await GetForStaffAsync(complaintId, actor);
        }

        public async Task<Complaint> ExtendDeadlineAsync(int complaintId, string reason, Actor actor)
        {
            var complaint = await GetForStaffAsync(complaintId, actor);

            if (!complaint.IsOpen)
                throw new BusinessRuleException("Rok se može produžiti samo za otvoren prigovor.");
            if (complaint.DeadlineExtendedAt != null)
                throw new BusinessRuleException("Rok za odgovor može se produžiti samo jednom.");
            if (string.IsNullOrWhiteSpace(reason))
                throw new BusinessRuleException("Razlog produženja roka je obavezan i biće prikazan korisniku.");

            complaint.DueAt = complaint.DueAt.AddDays(_settings.ComplaintExtensionDays);
            complaint.DeadlineExtendedAt = UtcNow;
            complaint.DeadlineExtensionReason = reason.Trim();
            complaint.UpdatedAt = UtcNow;
            complaint.StatusHistory.Add(new ComplaintStatusChange
            {
                OldStatus = complaint.Status.ToString(),
                NewStatus = complaint.Status.ToString(),
                ChangedByUserId = actor.UserId,
                Note = $"Rok produžen do {complaint.DueAt:dd.MM.yyyy}: {reason.Trim()}",
                ChangedAt = UtcNow
            });

            await _notificationPublisher.PublishAsync(new Notification
            {
                UserId = complaint.UserId,
                Type = NotificationType.Prigovor,
                Title = "Produžen rok za odgovor na prigovor",
                Message = $"Rok za odgovor na prigovor {complaint.ReferenceNumber} produžen je do {complaint.DueAt:dd.MM.yyyy}. Razlog: {reason.Trim()}",
                CreatedAt = UtcNow
            });

            await _context.SaveChangesAsync();
            return await GetForStaffAsync(complaintId, actor);
        }

        public async Task<Complaint> AddStaffMessageAsync(int complaintId, string message, bool isInternalNote, Actor actor)
        {
            var complaint = await GetForStaffAsync(complaintId, actor);
            if (!complaint.IsOpen && !isInternalNote)
                throw new BusinessRuleException("Zatvorenom prigovoru se ne mogu slati poruke korisniku.");

            complaint.Messages.Add(new ComplaintMessage
            {
                AuthorUserId = actor.UserId,
                IsFromStaff = true,
                IsInternalNote = isInternalNote,
                Message = message.Trim(),
                CreatedAt = UtcNow
            });
            complaint.UpdatedAt = UtcNow;

            if (!isInternalNote)
            {
                await _notificationPublisher.PublishAsync(new Notification
                {
                    UserId = complaint.UserId,
                    Type = NotificationType.Prigovor,
                    Title = "Nova poruka u vezi sa prigovorom",
                    Message = $"Banka je odgovorila na prigovor {complaint.ReferenceNumber}.",
                    CreatedAt = UtcNow
                });
            }

            await _context.SaveChangesAsync();
            return await GetForStaffAsync(complaintId, actor);
        }

        private Task<Complaint?> LoadAsync(int complaintId) =>
            _context.Complaints
                .Include(c => c.User)
                .Include(c => c.AssignedTo)
                .Include(c => c.Messages.OrderBy(m => m.CreatedAt)).ThenInclude(m => m.Author)
                .Include(c => c.StatusHistory.OrderBy(h => h.ChangedAt))
                .AsSplitQuery()
                .FirstOrDefaultAsync(c => c.Id == complaintId);

        private void Transition(Complaint complaint, ComplaintStatus status, int changedBy, string? note)
        {
            complaint.StatusHistory.Add(new ComplaintStatusChange
            {
                OldStatus = complaint.Status.ToString(),
                NewStatus = status.ToString(),
                ChangedByUserId = changedBy,
                Note = string.IsNullOrWhiteSpace(note) ? null : (note.Length > 500 ? note[..500] : note.Trim()),
                ChangedAt = UtcNow
            });
            complaint.Status = status;
        }

        private static void EnsureStaff(Actor actor)
        {
            if (!actor.IsStaff)
                throw new ForbiddenException();
        }

        public static string StatusLabel(ComplaintStatus status) => status switch
        {
            ComplaintStatus.Podnet => "Podnet",
            ComplaintStatus.UObradi => "U obradi",
            ComplaintStatus.CekaKorisnika => "Čeka vaš odgovor",
            ComplaintStatus.Resen => "Rešen",
            ComplaintStatus.Odbijen => "Odbijen",
            ComplaintStatus.Zatvoren => "Zatvoren",
            _ => status.ToString()
        };
    }
}
