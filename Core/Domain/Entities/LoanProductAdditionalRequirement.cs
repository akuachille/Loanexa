using System.ComponentModel.DataAnnotations;

namespace Domain.Entities
{
    public class LoanProductAdditionalRequirement
    {
        public int Id { get; set; }
        public int LoanProductId { get; set; }
        public LoanProduct LoanProduct { get; set; }
        
        [Required]
        public string RequirementText { get; set; }
        
        public int PersonId { get; set; }
        public Person Person { get; set; }
    }
}
