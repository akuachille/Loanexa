using Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Application.Interfaces
{
    public interface ILoanProductAdditionalRequirementService
    {
        Task<List<LoanProductAdditionalRequirement>> GetAdditionalRequirementsForProductAsync(int loanProductId);
        Task AddAdditionalRequirementAsync(LoanProductAdditionalRequirement requirement);
        Task DeleteAdditionalRequirementAsync(int id);
    }
}
