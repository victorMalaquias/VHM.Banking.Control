using Microsoft.AspNetCore.Mvc;
using VHM.Banking.Control.Entities;
using VHM.Banking.Control.Services;

namespace VHM.Banking.Control.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExpenseController : ControllerBase
    {
        private readonly ExpenseService _expenseService;

        public ExpenseController(ExpenseService expenseService)
        {
            _expenseService = expenseService;
        }

        [HttpPost("generate-graph")]
        public async Task<IActionResult> GenerateGraph([FromBody] GraphRequest request)
        {
            try
            {
                await _expenseService.GenerateGraph(request);
                return Ok(new { Message = "Graph generated and saved successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddExpense([FromBody] Expense expense)
        {
            try
            {
                await _expenseService.AddExpenseAsync(expense);
                return Ok(expense);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAllExpenses()
        {
            var expenses = await _expenseService.GetAllExpensesAsync();
            return Ok(expenses);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateExpense(int id, [FromBody] Expense expense)
        {
            if (expense.Id != id)
            {
                return BadRequest("Expense ID does not match.");
            }

            try
            {
                await _expenseService.UpdateExpenseAsync(expense);
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteExpense(int id)
        {
            await _expenseService.DeleteExpenseAsync(id);
            return NoContent();
        }
    }
}
