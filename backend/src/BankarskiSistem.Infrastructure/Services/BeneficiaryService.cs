using BankarskiSistem.Core.Banking;
using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;

namespace BankarskiSistem.Infrastructure.Services
{
    public class BeneficiaryService : IBeneficiaryService
    {
        private const int MaxBeneficiariesPerUser = 100;

        private readonly IBeneficiaryRepository _beneficiaryRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;

        public BeneficiaryService(IBeneficiaryRepository beneficiaryRepository, IUnitOfWork unitOfWork, TimeProvider timeProvider)
        {
            _beneficiaryRepository = beneficiaryRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
        }

        private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

        public Task<IReadOnlyList<Beneficiary>> GetAllAsync(int userId) => _beneficiaryRepository.GetForUserAsync(userId);

        public async Task<Beneficiary> CreateAsync(int userId, BeneficiaryCommand command)
        {
            var existing = await _beneficiaryRepository.GetForUserAsync(userId);
            if (existing.Count >= MaxBeneficiariesPerUser)
                throw new BusinessRuleException($"Možete sačuvati najviše {MaxBeneficiariesPerUser} primalaca.");

            if (await _beneficiaryRepository.FindByNicknameAsync(userId, command.Nickname.Trim()) != null)
                throw new ConflictException("Primalac sa tim nazivom već postoji.");

            var beneficiary = new Beneficiary { UserId = userId, CreatedAt = UtcNow };
            Apply(beneficiary, command);
            await _beneficiaryRepository.AddAsync(beneficiary);
            await _unitOfWork.SaveChangesAsync();
            return beneficiary;
        }

        public async Task<Beneficiary> UpdateAsync(int userId, int beneficiaryId, BeneficiaryCommand command)
        {
            var beneficiary = await FindAsync(userId, beneficiaryId);
            var duplicate = await _beneficiaryRepository.FindByNicknameAsync(userId, command.Nickname.Trim());
            if (duplicate != null && duplicate.Id != beneficiary.Id)
                throw new ConflictException("Primalac sa tim nazivom već postoji.");

            Apply(beneficiary, command);
            await _unitOfWork.SaveChangesAsync();
            return beneficiary;
        }

        public async Task DeleteAsync(int userId, int beneficiaryId)
        {
            _beneficiaryRepository.Remove(await FindAsync(userId, beneficiaryId));
            await _unitOfWork.SaveChangesAsync();
        }

        private async Task<Beneficiary> FindAsync(int userId, int beneficiaryId) =>
            await _beneficiaryRepository.GetAsync(userId, beneficiaryId)
            ?? throw new NotFoundException("Primalac nije pronađen.");

        private void Apply(Beneficiary beneficiary, BeneficiaryCommand command)
        {
            if (!SerbianAccountNumber.TryParse(command.AccountNumber, out var accountNumber))
                throw new BusinessRuleException("Broj računa primaoca nije u ispravnom formatu.");

            if (!string.IsNullOrWhiteSpace(command.PaymentCode) && !PaymentCodes.IsValidFormat(command.PaymentCode))
                throw new BusinessRuleException("Šifra plaćanja mora biti trocifren broj koji počinje sa 1 ili 2.");

            var referenceError = PaymentReference.Validate(Blank(command.ReferenceModel), Blank(command.ReferenceNumber));
            if (referenceError != null)
                throw new BusinessRuleException(referenceError);

            beneficiary.Nickname = command.Nickname.Trim();
            beneficiary.RecipientName = command.RecipientName.Trim();
            beneficiary.AccountNumber = accountNumber.Formatted;
            beneficiary.RecipientAddress = Blank(command.RecipientAddress);
            beneficiary.PaymentCode = Blank(command.PaymentCode);
            beneficiary.Purpose = Blank(command.Purpose);
            beneficiary.ReferenceModel = Blank(command.ReferenceModel);
            beneficiary.ReferenceNumber = Blank(command.ReferenceNumber);
            beneficiary.UpdatedAt = UtcNow;
        }

        private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
