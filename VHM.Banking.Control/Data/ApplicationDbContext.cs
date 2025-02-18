using Microsoft.EntityFrameworkCore;
using VHM.Banking.Control.Entities;

namespace VHM.Banking.Control.Data
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<Expense> Expenses { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
    }
}
