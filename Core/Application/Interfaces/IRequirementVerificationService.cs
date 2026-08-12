using Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Application.Interfaces
{
    public interface IRequirementVerificationService
    {
        Task<List<ApplicationRequirementVerification>> GetVerificationsForApplicationAsync(int loanApplicationId);
        Task UpdateVerificationAsync(int verificationId, bool isVerified);
        Task InitializeVerificationsForApplicationAsync(int loanApplicationId, int loanProductId);
        Task<bool> AreAllRequirementsVerifiedAsync(int loanApplicationId);
    }
}
