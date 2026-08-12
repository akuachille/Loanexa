using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public class LoanProductAdditionalRequirementService : ILoanProductAdditionalRequirementService
    {
        private readonly ApplicationDbContext _context;

        public LoanProductAdditionalRequirementService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<LoanProductAdditionalRequirement>> GetAdditionalRequirementsForProductAsync(int loanProductId)
        {
            return await _context.LoanProductAdditionalRequirements
                .Where(r => r.LoanProductId == loanProductId)
                .ToListAsync();
        }

        public async Task AddAdditionalRequirementAsync(LoanProductAdditionalRequirement requirement)
        {
            _context.LoanProductAdditionalRequirements.Add(requirement);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAdditionalRequirementAsync(int id)
        {
            var req = await _context.LoanProductAdditionalRequirements.FindAsync(id);
            if (req != null)
            {
                _context.LoanProductAdditionalRequirements.Remove(req);
                await _context.SaveChangesAsync();
            }
        }
    }
}
