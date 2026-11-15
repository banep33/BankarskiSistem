using BankarskiSistem.API.Contracts;
using BankarskiSistem.API.Security;
using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankarskiSistem.API.Controllers
{
    [Authorize(Policy = Policies.Staff)]
    public class UsersController(
        IUserService userService,
        IAccountService accountService,
        IDataAccessLogger dataAccessLogger,
        ITwoFactorService twoFactorService,
        IStandingOrderService standingOrderService,
        IAuditLogService auditLog) : ApiControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<PagedResponse<UserResponse>>> GetAll([FromQuery] UserQuery query)
        {
            var filter = new UserFilter { Role = query.Role, IsActive = query.IsActive, Search = query.Search };
            var result = await userService.GetPagedAsync(filter, new PageRequest(query.Page, query.PageSize), CurrentActor);
            return Ok(result.ToResponse(u => u.ToResponse()));
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<UserResponse>> GetById(int id)
        {
            var actor = CurrentActor;
            var user = await userService.GetByIdAsync(id, actor);
            await dataAccessLogger.LogAsync(actor, id, "Profil", id, "Pregled profila klijenta");
            return Ok(user.ToResponse());
        }

        [HttpPost("{id:int}/kyc")]
        public async Task<ActionResult<UserResponse>> UpdateKyc(int id, UpdateKycRequest request)
        {
            var actor = CurrentActor;
            var user = await userService.UpdateKycAsync(id, request.Status, request.RiskRating, actor);
            await auditLog.LogAsync(new AuditEntry(actor.UserId, "Promenjen KYC status", "User", id,
                $"KYC: {request.Status}, rizik: {request.RiskRating} (simulirana provera identiteta)"));
            return Ok(user.ToResponse());
        }

        [HttpGet("{id:int}/accounts")]
        public async Task<ActionResult<IEnumerable<AccountResponse>>> GetAccounts(int id)
        {
            var actor = CurrentActor;
            await userService.GetByIdAsync(id, actor);
            var accounts = await accountService.GetByUserIdAsync(id, actor);
            await dataAccessLogger.LogAsync(actor, id, "Racuni", null, "Pregled računa klijenta");
            return Ok(accounts.Select(a => a.ToResponse()));
        }

        [HttpPost]
        public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request)
        {
            var actor = CurrentActor;
            var user = await userService.CreateAsync(
                new CreateUserCommand(request.FirstName, request.LastName, request.Email, request.Password, request.Role),
                actor);

            await auditLog.LogAsync(new AuditEntry(actor.UserId, "Kreiran korisnik", "User", user.Id, $"{user.Email}, uloga {user.Role}"));
            return CreatedAtAction(nameof(GetById), new { id = user.Id }, user.ToResponse());
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<UserResponse>> Update(int id, UpdateUserRequest request)
        {
            var actor = CurrentActor;
            var before = Describe(await userService.GetByIdAsync(id, actor));
            var user = await userService.UpdateAsync(
                id, new UpdateUserCommand(request.FirstName, request.LastName, request.Email, request.Role), actor);

            await auditLog.LogAsync(new AuditEntry(actor.UserId, "Izmenjen korisnik", "User", id,
                NewValue: Describe(user), OldValue: before));
            return Ok(user.ToResponse());
        }

        [HttpPost("{id:int}/deactivate")]
        public Task<ActionResult<UserResponse>> Deactivate(int id) =>
            ExecuteLifecycleAsync(id, "Deaktiviran korisnik", userService.DeactivateAsync);

        [HttpPost("{id:int}/activate")]
        public Task<ActionResult<UserResponse>> Activate(int id) =>
            ExecuteLifecycleAsync(id, "Aktiviran korisnik", userService.ActivateAsync);

        [HttpPost("{id:int}/unlock")]
        public Task<ActionResult<UserResponse>> Unlock(int id) =>
            ExecuteLifecycleAsync(id, "Otkljucan nalog", userService.UnlockAsync);

        [HttpGet("{id:int}/standing-orders")]
        public async Task<ActionResult<IEnumerable<StandingOrderResponse>>> GetStandingOrders(int id)
        {
            var actor = CurrentActor;
            var orders = await standingOrderService.GetForUserAsync(id, actor);
            await dataAccessLogger.LogAsync(actor, id, "TrajniNalozi", null, "Pregled trajnih naloga klijenta");
            return Ok(orders.Select(o => o.ToResponse()));
        }

        [HttpPost("{id:int}/two-factor/reset")]
        [Authorize(Policy = Policies.Administrator)]
        public async Task<IActionResult> ResetTwoFactor(int id)
        {
            var actor = CurrentActor;
            await twoFactorService.ResetByAdministratorAsync(id, actor);
            await auditLog.LogAsync(new AuditEntry(actor.UserId, "Resetovana dvostepena prijava", "User", id));
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        [Authorize(Policy = Policies.Administrator)]
        public async Task<IActionResult> PermanentDelete(int id)
        {
            var actor = CurrentActor;
            var user = await userService.PermanentDeleteAsync(id, actor);
            await auditLog.LogAsync(new AuditEntry(actor.UserId, "Trajno obrisan korisnik", "User", id, user.Email));
            return NoContent();
        }

        private async Task<ActionResult<UserResponse>> ExecuteLifecycleAsync(
            int id, string auditAction, Func<int, Actor, Task<User>> operation)
        {
            var actor = CurrentActor;
            var user = await operation(id, actor);
            await auditLog.LogAsync(new AuditEntry(actor.UserId, auditAction, "User", id, user.Email));
            return Ok(user.ToResponse());
        }

        private static string Describe(User user) => $"Ime: {user.FullName}, Email: {user.Email}, Uloga: {user.Role}";
    }
}
