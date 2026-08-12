using System;

namespace Domain.Entities
{
    public class ApplicationRequirementVerification
    {
        public int Id { get; set; }
        public int LoanApplicationId { get; set; }
        public LoanApplication LoanApplication { get; set; }

        public int? RequirementId { get; set; }
        public Requirement Requirement { get; set; }

        public int? AdditionalRequirementId { get; set; }
        public LoanProductAdditionalRequirement AdditionalRequirement { get; set; }

        public bool IsVerified { get; set; }
        
        public int PersonId { get; set; }
        public Person Person { get; set; }
    }
}
