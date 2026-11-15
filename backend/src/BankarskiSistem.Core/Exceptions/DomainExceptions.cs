namespace BankarskiSistem.Core.Exceptions
{
    public abstract class DomainException : Exception
    {
        protected DomainException(string message) : base(message) { }
    }

    public class NotFoundException : DomainException
    {
        public NotFoundException(string message) : base(message) { }
    }

    public class BusinessRuleException : DomainException
    {
        public BusinessRuleException(string message) : base(message) { }
    }

    public class ForbiddenException : DomainException
    {
        public ForbiddenException(string message = "Nemate dozvolu za ovu akciju.") : base(message) { }
    }

    public class ReauthenticationRequiredException : DomainException
    {
        public ReauthenticationRequiredException(string message = "Za ovu radnju potrebno je da ponovo potvrdite lozinku.") : base(message) { }
    }

    public class ConflictException : DomainException
    {
        public ConflictException(string message) : base(message) { }
    }

    public class DuplicateEntryException : ConflictException
    {
        public DuplicateEntryException(string message) : base(message) { }
    }

    public class AuthenticationFailedException : DomainException
    {
        public int? UserId { get; init; }

        public AuthenticationFailedException(string message) : base(message) { }
    }

    public class InvalidCredentialsException : AuthenticationFailedException
    {
        public InvalidCredentialsException(string message = "Pogrešni podaci za prijavu.") : base(message) { }
    }

    public class AccountDeactivatedException : AuthenticationFailedException
    {
        public AccountDeactivatedException() : base("Vaš nalog je deaktiviran. Obratite se banci.") { }
    }

    public class AccountLockedException : AuthenticationFailedException
    {
        public int RemainingMinutes { get; }

        public AccountLockedException(int remainingMinutes)
            : base($"Nalog je privremeno zaključan. Pokušajte ponovo za {remainingMinutes} min.")
        {
            RemainingMinutes = remainingMinutes;
        }
    }
}
