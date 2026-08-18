using Domain.Entities;
using Application.DTO;
using Application.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class PenalityRepository : IPenality
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly IUserContext _userContext;

        public PenalityRepository(IDbContextFactory<ApplicationDbContext> contextFactory, IUserContext userContext)
        {
            _contextFactory = contextFactory;
            _userContext = userContext;
        }

        public async Task<List<Penality>> GetAllPenalitiesAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            if (_userContext.Id == null)
            {
                return new List<Penality>();
            }
            var penalties = await context.Penalties
                .AsNoTracking()
                .Where(a => a.PersonId == _userContext.PersonId && a.IsActive)
                .Include(i => i.LoanApplication)
                .ThenInclude(l => l.Borrower)
                .Include(i => i.Reason)
                .OrderBy(p => p.Date)
                .ToListAsync();

            var loanAppIds = penalties.Select(p => p.LoanApplicationId).Distinct().ToList();
            
            var disbursements = await context.Disbursements
                .AsNoTracking()
                .Where(d => loanAppIds.Contains(d.LoanApplicationId) && d.IsActive)
                .Include(d => d.Payments)
                .ToListAsync();

            var unpaidPenalties = new List<Penality>();

            foreach (var appId in loanAppIds)
            {
                var appPenalties = penalties.Where(p => p.LoanApplicationId == appId).OrderBy(p => p.Date).ToList();
                var appDisbursements = disbursements.Where(d => d.LoanApplicationId == appId).ToList();
                decimal totalPenaltyPaid = appDisbursements.SelectMany(d => d.Payments).Where(p => p.IsActive).Sum(p => p.PenaltyPaid);

                foreach (var penalty in appPenalties)
                {
                    if (totalPenaltyPaid >= penalty.Amount)
                    {
                        totalPenaltyPaid -= penalty.Amount;
                    }
                    else
                    {
                        if (totalPenaltyPaid > 0)
                        {
                            penalty.Amount -= totalPenaltyPaid;
                            totalPenaltyPaid = 0;
                        }
                        unpaidPenalties.Add(penalty);
                    }
                }
            }

            return unpaidPenalties.OrderByDescending(p => p.Date).OrderByDescending(x => x.Id).ToList();
        }

        public async Task<Penality?> GetPenalityByIdAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            if (_userContext.Id == null)
            {
                return null;
            }
            return await context.Penalties
                .AsNoTracking()
                .Where(a => a.PersonId == _userContext.PersonId) 
                .Include(i => i.LoanApplication)
                .Include(i => i.Reason)
                .FirstOrDefaultAsync(i => i.Id == id);
        }

        public async Task CreatePenalityAsync(CreatePenalityDTO penalityDTO)
        {

             if (_userContext.Id == null)
            {
                throw new Exception("User not authenticated");
            }

            using var dbContext = await _contextFactory.CreateDbContextAsync();

    // 2. Query 'Users' from the dbContext instance, NOT the factory
         var user = await dbContext.Users
        .Include(u => u.Person) // You'll likely need this to link the account
        .FirstOrDefaultAsync(u => u.Id == _userContext.Id);

            if (user == null)
            {
                throw new Exception("User record not found");
            }

            if (user.Person == null)
            {
                throw new Exception("Authenticated user does not have an associated Person record.");
            }
            using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                // 1. Identify active disbursement and include Settings for the dynamic rate
                var disbursement = await context.Disbursements
                    .Include(d => d.LoanApplication)
                        .ThenInclude(la => la.LoanProductSetting)
                    .FirstOrDefaultAsync(d => d.LoanApplicationId == penalityDTO.LoanApplicationId && d.IsActive);

                if (disbursement == null) return;

                // Guard: ensure ReasonId is valid (> 0) to prevent FK 547 error
                if (penalityDTO.ReasonId <= 0)
                    throw new Exception("Please select a valid Penalty Reason before applying the penalty.");

                // 2. Fetch dynamic rate from LoanProductSetting
                decimal rate = disbursement.LoanApplication?.LoanProductSetting?.PenalityRate ?? 0;
                
                // 3. APPLY DYNAMIC PENALTY ON THE SHORTFALL
                // penalityDTO.Amount contains the unpaid shortfall
                decimal calculatedPenaltyFee = penalityDTO.Amount * (rate / 100m);

                var penality = new Penality
                {
                    LoanApplicationId = penalityDTO.LoanApplicationId,
                    Amount = calculatedPenaltyFee,
                    Date = penalityDTO.Date ?? DateTime.Now,
                    ReasonId = penalityDTO.ReasonId,
                    Description = $"{penalityDTO.Description} (Shortfall: {penalityDTO.Amount:N2} | {rate}% Fee: {calculatedPenaltyFee:N2})",
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                      PersonId = user.Person.Id
                };

                context.Penalties.Add(penality);

                // 4. DEBT ADJUSTMENT
                // We only add the calculatedPenaltyFee. 
                // The penalityDTO.Amount (shortfall) is already included in the disbursement.Amount principal.
                disbursement.Amount += calculatedPenaltyFee;
                disbursement.UpdatedAt = DateTime.Now;

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw; 
            }
        }
    }
}
