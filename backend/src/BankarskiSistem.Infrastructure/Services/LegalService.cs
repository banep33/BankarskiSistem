using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;

namespace BankarskiSistem.Infrastructure.Services
{
    public class LegalService : ILegalService
    {
        private readonly ILegalRepository _legalRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IRequestContext _requestContext;
        private readonly TimeProvider _timeProvider;

        public LegalService(
            ILegalRepository legalRepository,
            IUnitOfWork unitOfWork,
            IRequestContext requestContext,
            TimeProvider timeProvider)
        {
            _legalRepository = legalRepository;
            _unitOfWork = unitOfWork;
            _requestContext = requestContext;
            _timeProvider = timeProvider;
        }

        public Task<IReadOnlyList<LegalDocument>> GetCurrentDocumentsAsync() => _legalRepository.GetCurrentDocumentsAsync();

        public Task<IReadOnlyList<LegalDocument>> GetPendingDocumentsAsync(int userId) => _legalRepository.GetPendingForUserAsync(userId);

        public Task<IReadOnlyList<UserConsent>> GetConsentsAsync(int userId) => _legalRepository.GetConsentsForUserAsync(userId);

        public async Task AcceptAsync(int userId, IReadOnlyCollection<int> documentIds)
        {
            var pending = await _legalRepository.GetPendingForUserAsync(userId);
            var unknown = documentIds.Except(pending.Select(d => d.Id)).ToList();
            if (unknown.Count > 0)
                throw new BusinessRuleException("Možete prihvatiti samo trenutno važeće dokumente koje još niste prihvatili.");

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            foreach (var documentId in documentIds.Distinct())
            {
                await _legalRepository.AddConsentAsync(new UserConsent
                {
                    UserId = userId,
                    LegalDocumentId = documentId,
                    AcceptedAt = now,
                    IpAddress = _requestContext.IpAddress
                });
            }

            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<LegalDocument> PublishAsync(PublishLegalDocumentCommand command, Actor actor)
        {
            if (!actor.IsAdministrator)
                throw new ForbiddenException();

            var version = command.Version.Trim();
            if (await _legalRepository.VersionExistsAsync(command.DocumentType, version))
                throw new ConflictException($"Verzija {version} ovog dokumenta već postoji.");

            var document = new LegalDocument
            {
                DocumentType = command.DocumentType,
                Version = version,
                Title = command.Title.Trim(),
                Content = command.Content.Trim(),
                PublishedAt = _timeProvider.GetUtcNow().UtcDateTime,
                PublishedByUserId = actor.UserId
            };

            await _legalRepository.AddDocumentAsync(document);
            await _unitOfWork.SaveChangesAsync();
            return document;
        }
    }
}
