using VHM.Banking.Control.Enum;

namespace VHM.Banking.Control.Entities
{
    public class Expense
    {
        public int Id { get; set; }
        public string Description { get; set; }
        public Category Category { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
    }
}
