using VHM.Banking.Control.Entities;

namespace VHM.Banking.Control.Repositories.Interfaces
{
    public interface IExpenseRepository
    {
        Task AddExpenseAsync(Expense expense);
        Task<IEnumerable<Expense>> GetAllExpensesAsync();
        Task<Expense> GetExpenseByIdAsync(int id);
        Task UpdateExpenseAsync(Expense expense);
        Task DeleteExpenseAsync(int id);
    }
}
