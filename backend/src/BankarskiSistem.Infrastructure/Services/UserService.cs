using BankarskiSistem.Core.Banking;
using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;

namespace BankarskiSistem.Infrastructure.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ISessionValidationCache _sessionCache;
        private readonly TimeProvider _timeProvider;

        public UserService(
            IUserRepository userRepository,
            IUnitOfWork unitOfWork,
            IPasswordHasher passwordHasher,
            ISessionValidationCache sessionCache,
            TimeProvider timeProvider)
        {
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
            _passwordHasher = passwordHasher;
            _sessionCache = sessionCache;
            _timeProvider = timeProvider;
        }

        private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

        public Task<PagedResult<User>> GetPagedAsync(UserFilter filter, PageRequest page, Actor actor)
        {
            if (!actor.IsStaff)
                throw new ForbiddenException();

            var scopedFilter = actor.IsBanker ? filter with { Role = UserRole.Korisnik } : filter;
            return _userRepository.GetPagedAsync(scopedFilter, page);
        }

        public async Task<User> GetByIdAsync(int id, Actor actor)
        {
            var user = await FindUserAsync(id);
            EnsureCanManage(user, actor);
            return user;
        }

        public async Task<User> CreateAsync(CreateUserCommand command, Actor actor)
        {
            if (!actor.IsStaff)
                throw new ForbiddenException();

            if (actor.IsBanker && command.Role != UserRole.Korisnik)
                throw new ForbiddenException("Bankar može da kreira samo klijente.");

            var email = EmailNormalizer.Normalize(command.Email);
            if (await _userRepository.EmailExistsAsync(email))
                throw new ConflictException("Korisnik sa tom email adresom već postoji.");

            var user = new User
            {
                FirstName = command.FirstName.Trim(),
                LastName = command.LastName.Trim(),
                Email = email,
                PasswordHash = _passwordHasher.Hash(command.Password),
                Role = command.Role,
                IsActive = true,
                KycStatus = command.Role == UserRole.Korisnik ? KycStatus.NijeVerifikovan : KycStatus.NijePrimenljivo,
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            };

            await _userRepository.AddAsync(user);
            await _unitOfWork.SaveChangesAsync();
            return user;
        }

        public async Task<User> UpdateAsync(int id, UpdateUserCommand command, Actor actor)
        {
            var user = await FindUserAsync(id);
            EnsureCanManage(user, actor);

            if (command.Role != user.Role)
            {
                if (!actor.IsAdministrator)
                    throw new ForbiddenException("Samo administrator može menjati ulogu korisnika.");
                if (user.Id == actor.UserId)
                    throw new BusinessRuleException("Ne možete promeniti sopstvenu ulogu.");
            }

            var email = EmailNormalizer.Normalize(command.Email);
            if (email != user.Email && await _userRepository.EmailExistsAsync(email, user.Id))
                throw new ConflictException("Korisnik sa tom email adresom već postoji.");

            var securityRelevantChange = command.Role != user.Role || email != user.Email;

            user.FirstName = command.FirstName.Trim();
            user.LastName = command.LastName.Trim();
            user.Email = email;
            user.Role = command.Role;
            user.UpdatedAt = UtcNow;

            if (securityRelevantChange)
                user.InvalidateTokens();

            await _unitOfWork.SaveChangesAsync();
            if (securityRelevantChange)
                _sessionCache.InvalidateUser(user.Id);
            return user;
        }

        public async Task<User> DeactivateAsync(int id, Actor actor)
        {
            var user = await FindUserAsync(id);
            EnsureCanManage(user, actor);
            EnsureNotSelf(user, actor, "Ne možete deaktivirati sopstveni nalog.");

            if (!user.IsActive)
                throw new BusinessRuleException("Korisnik je već deaktiviran.");

            user.IsActive = false;
            user.InvalidateTokens();
            user.UpdatedAt = UtcNow;
            await _unitOfWork.SaveChangesAsync();
            _sessionCache.InvalidateUser(user.Id);
            return user;
        }

        public async Task<User> ActivateAsync(int id, Actor actor)
        {
            var user = await FindUserAsync(id);
            EnsureCanManage(user, actor);

            if (user.IsActive)
                throw new BusinessRuleException("Korisnik je već aktivan.");

            user.IsActive = true;
            user.UpdatedAt = UtcNow;
            await _unitOfWork.SaveChangesAsync();
            return user;
        }

        public async Task<User> UnlockAsync(int id, Actor actor)
        {
            var user = await FindUserAsync(id);
            EnsureCanManage(user, actor);

            if (!user.IsLocked)
                throw new BusinessRuleException("Nalog nije zaključan.");

            user.IsLocked = false;
            user.LockedUntil = null;
            user.FailedLoginAttempts = 0;
            user.UpdatedAt = UtcNow;
            await _unitOfWork.SaveChangesAsync();
            return user;
        }

        public async Task<User> UpdateKycAsync(int id, KycStatus status, RiskLevel riskRating, Actor actor)
        {
            var user = await FindUserAsync(id);
            EnsureCanManage(user, actor);

            if (user.Role != UserRole.Korisnik)
                throw new BusinessRuleException("KYC status se vodi samo za klijente banke.");
            if (status == KycStatus.NijePrimenljivo)
                throw new BusinessRuleException("Neispravan KYC status za klijenta.");

            user.KycStatus = status;
            user.RiskRating = riskRating;
            user.KycVerifiedAt = status == KycStatus.Verifikovan ? UtcNow : null;
            user.KycVerifiedByUserId = status == KycStatus.Verifikovan ? actor.UserId : null;
            user.UpdatedAt = UtcNow;
            await _unitOfWork.SaveChangesAsync();
            return user;
        }

        public async Task<User> PermanentDeleteAsync(int id, Actor actor)
        {
            if (!actor.IsAdministrator)
                throw new ForbiddenException();

            var user = await FindUserAsync(id);
            EnsureNotSelf(user, actor, "Ne možete obrisati sopstveni nalog.");

            if (await _userRepository.HasFinancialHistoryAsync(user.Id))
                throw new ConflictException(
                    "Korisnik ima račune ili finansijsku istoriju i ne može se trajno obrisati. Deaktivirajte ga.");

            _userRepository.Remove(user);
            await _unitOfWork.SaveChangesAsync();
            _sessionCache.InvalidateUser(user.Id);
            return user;
        }

        public Task<User> GetProfileAsync(int userId) => FindUserAsync(userId);

        public async Task<User> UpdateProfileAsync(int userId, UpdateProfileCommand command)
        {
            var user = await FindUserAsync(userId);
            user.FirstName = command.FirstName.Trim();
            user.LastName = command.LastName.Trim();
            user.UpdatedAt = UtcNow;
            await _unitOfWork.SaveChangesAsync();
            return user;
        }

        public async Task EnsureAdministratorExistsAsync(string email, string password, string firstName, string lastName)
        {
            if (await _userRepository.AnyAdministratorAsync())
                return;

            await _userRepository.AddAsync(new User
            {
                FirstName = firstName,
                LastName = lastName,
                Email = EmailNormalizer.Normalize(email),
                PasswordHash = _passwordHasher.Hash(password),
                Role = UserRole.Administrator,
                IsActive = true,
                KycStatus = KycStatus.NijePrimenljivo,
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            });
            await _unitOfWork.SaveChangesAsync();
        }

        private async Task<User> FindUserAsync(int id) =>
            await _userRepository.GetByIdAsync(id)
            ?? throw new NotFoundException($"Korisnik sa ID {id} nije pronađen.");

        private static void EnsureCanManage(User target, Actor actor)
        {
            if (actor.IsAdministrator)
                return;

            if (actor.IsBanker && target.Role == UserRole.Korisnik)
                return;

            throw new ForbiddenException();
        }

        private static void EnsureNotSelf(User target, Actor actor, string message)
        {
            if (target.Id == actor.UserId)
                throw new BusinessRuleException(message);
        }
    }
}
