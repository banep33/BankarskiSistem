using System.Text.RegularExpressions;
using BankarskiSistem.API.Security;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace BankarskiSistem.API.Controllers
{
    [ApiController]
    [Route(RoutePrefix + "/[controller]")]
    public abstract partial class ApiControllerBase : ControllerBase
    {
        public const string RoutePrefix = "api/v1";
        public const string IdempotencyHeader = "Idempotency-Key";

        protected Actor CurrentActor => User.ToActor();

        protected static string? NormalizeIdempotencyKey(string? idempotencyKey)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                return null;

            var trimmed = idempotencyKey.Trim();
            if (!IdempotencyKeyPattern().IsMatch(trimmed))
                throw new BusinessRuleException("Idempotency-Key mora imati 8 do 64 znaka (slova, brojevi, crtica ili donja crta).");

            return trimmed;
        }

        protected static (DateTime? FromUtc, DateTime? ToUtc) ToUtcRange(
            IBusinessCalendar calendar, DateOnly? from, DateOnly? to)
        {
            if (from.HasValue && to.HasValue && from > to)
                throw new BusinessRuleException("Datum od ne može biti posle datuma do.");

            DateTime? fromUtc = from.HasValue ? calendar.ToUtc(from.Value.ToDateTime(TimeOnly.MinValue)) : null;
            DateTime? toUtc = to.HasValue ? calendar.ToUtc(to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue)) : null;
            return (fromUtc, toUtc);
        }

        [GeneratedRegex("^[A-Za-z0-9_-]{8,64}$")]
        private static partial Regex IdempotencyKeyPattern();
    }
}
