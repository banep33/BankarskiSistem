namespace BankarskiSistem.API.Contracts
{
    public record AssignCategoryRequest(int CategoryId, bool RememberForCounterparty);

    public record SetBudgetRequest(int CategoryId, decimal MonthlyLimit);

    public record BudgetResponse(int Id, int CategoryId, string CategoryName, string Color, decimal MonthlyLimit);
}
