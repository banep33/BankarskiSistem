using BankarskiSistem.API.Contracts;
using BankarskiSistem.API.Security;
using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankarskiSistem.API.Controllers
{
    [Route(RoutePrefix)]
    public class BudgetsController(IBudgetService budgetService) : ApiControllerBase
    {
        [HttpGet("categories")]
        public async Task<ActionResult<IEnumerable<SpendingCategory>>> GetCategories() =>
            Ok(await budgetService.GetCategoriesAsync());

        [HttpGet("me/spending")]
        [Authorize(Policy = Policies.Customer)]
        public async Task<ActionResult<SpendingSummary>> GetSpending([FromQuery] string? month)
        {
            var actor = CurrentActor;
            var summary = await budgetService.GetSpendingAsync(actor, month);
            await budgetService.TryEvaluateAlertsAsync(actor.UserId);
            return Ok(summary);
        }

        [HttpPut("me/transactions/{transactionId:int}/category")]
        [Authorize(Policy = Policies.Customer)]
        public async Task<IActionResult> AssignCategory(int transactionId, AssignCategoryRequest request)
        {
            await budgetService.AssignCategoryAsync(CurrentActor, transactionId, request.CategoryId, request.RememberForCounterparty);
            return NoContent();
        }

        [HttpGet("me/budgets")]
        [Authorize(Policy = Policies.Customer)]
        public async Task<ActionResult<IEnumerable<BudgetResponse>>> GetBudgets() =>
            Ok((await budgetService.GetBudgetsAsync(CurrentActor)).Select(ToResponse));

        [HttpPut("me/budgets")]
        [Authorize(Policy = Policies.Customer)]
        public async Task<ActionResult<BudgetResponse>> SetBudget(SetBudgetRequest request) =>
            Ok(ToResponse(await budgetService.SetBudgetAsync(CurrentActor, request.CategoryId, request.MonthlyLimit)));

        [HttpDelete("me/budgets/{id:int}")]
        [Authorize(Policy = Policies.Customer)]
        public async Task<IActionResult> DeleteBudget(int id)
        {
            await budgetService.DeleteBudgetAsync(CurrentActor, id);
            return NoContent();
        }

        private static BudgetResponse ToResponse(Budget budget) =>
            new(budget.Id, budget.CategoryId, budget.Category?.Name ?? string.Empty, budget.Category?.Color ?? "#B8B0A6", budget.MonthlyLimit);
    }
}
