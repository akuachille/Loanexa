using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace Infrastructure.Repositories
{
    public class RequirementVerificationService : IRequirementVerificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserContext _userContext;

        public RequirementVerificationService(ApplicationDbContext context, IUserContext userContext)
        {
            _context = context;
            _userContext = userContext;
        }

        public async Task<List<ApplicationRequirementVerification>> GetVerificationsForApplicationAsync(int loanApplicationId)
        {
            return await _context.ApplicationRequirementVerifications
                .Include(v => v.Requirement)
                    .ThenInclude(r => r.RequiredDocument)
                .Include(v => v.AdditionalRequirement)
                .Where(v => v.LoanApplicationId == loanApplicationId)
                .ToListAsync();
        }

        public async Task UpdateVerificationAsync(int verificationId, bool isVerified)
        {
            var verification = await _context.ApplicationRequirementVerifications.FindAsync(verificationId);
            if (verification != null)
            {
                verification.IsVerified = isVerified;
                await _context.SaveChangesAsync();
            }
        }

        public async Task InitializeVerificationsForApplicationAsync(int loanApplicationId, int loanProductId)
        {
            var existing = await _context.ApplicationRequirementVerifications
                .Where(v => v.LoanApplicationId == loanApplicationId)
                .ToListAsync();

            if (existing.Any()) return; // Already initialized

            var newVerifications = new List<ApplicationRequirementVerification>();

            // 1. Add Document Requirements
            var documentRequirements = await _context.Requirements
                .Where(r => r.LoanProductId == loanProductId)
                .ToListAsync();

            foreach (var req in documentRequirements)
            {
                newVerifications.Add(new ApplicationRequirementVerification
                {
                    LoanApplicationId = loanApplicationId,
                    RequirementId = req.Id,
                    IsVerified = false,
                    PersonId = _userContext.PersonId ?? 0
                });
            }

            // 2. Add Additional Requirements
            var additionalRequirements = await _context.LoanProductAdditionalRequirements
                .Where(r => r.LoanProductId == loanProductId)
                .ToListAsync();

            foreach (var req in additionalRequirements)
            {
                newVerifications.Add(new ApplicationRequirementVerification
                {
                    LoanApplicationId = loanApplicationId,
                    AdditionalRequirementId = req.Id,
                    IsVerified = false,
                    PersonId = _userContext.PersonId ?? 0
                });
            }

            if (newVerifications.Any())
            {
                _context.ApplicationRequirementVerifications.AddRange(newVerifications);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> AreAllRequirementsVerifiedAsync(int loanApplicationId)
        {
            var verifications = await _context.ApplicationRequirementVerifications
                .Where(v => v.LoanApplicationId == loanApplicationId)
                .ToListAsync();

            if (!verifications.Any()) return true; // Nothing to verify

            return verifications.All(v => v.IsVerified);
        }
    }
}
