using Domain.Entities;
using Application.DTO;
using Application.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Domain.ValueObjects;

namespace Infrastructure.Repositories
{
    public class PaymentRepository : IPayment
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly IUserContext _userContext;

        public PaymentRepository(IDbContextFactory<ApplicationDbContext> contextFactory, IUserContext userContext)
        {
            _contextFactory = contextFactory;
            _userContext = userContext;
        }

        public async Task CreatePaymentAsync(CreatePaymentDTO paymentDTO)
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
                // 1. Fetch Disbursement
                var allowedPersonIds = await _userContext.GetAllowedPersonIdsAsync();
                var disbursement = await context.Disbursements
                
                    .Include(d => d.LoanApplication)
                    .Include(d => d.Payments)
                     .Where(a => allowedPersonIds.Contains(a.PersonId))
                    .FirstOrDefaultAsync(d => d.Id == paymentDTO.DisbursementId);

                if (disbursement == null) throw new Exception("Disbursement not found.");

                // 4. Calculate Total Remaining Debt for the whole loan
                var totalPaidSoFar = disbursement.Payments
                    .Where(p => p.IsActive)
                    .Sum(p => p.Amount);

                decimal remainingTotalDebt = disbursement.Amount - totalPaidSoFar;

                if (paymentDTO.Amount > remainingTotalDebt)
                {
                    throw new InvalidOperationException($"Overpayment detected. The remaining loan balance is {remainingTotalDebt:N2}.");
                }

                // 6. Update Account Balance
                var account = await context.Accounts.FirstOrDefaultAsync(a => a.Id == paymentDTO.AccountId);
                if (account == null) throw new Exception("Target account not found.");
                
                account.Balance += paymentDTO.Amount;

                // 6. Calculate Penalty, Interest vs Principal
                decimal penaltyPart = 0;
                decimal interestPart = 0;
                decimal principalPart = 0;
                decimal remainingPayment = paymentDTO.Amount;

                // 1. Penalty
                if (remainingPayment > 0)
                {
                    decimal totalPenalties = await context.Penalties.Where(p => p.LoanApplicationId == disbursement.LoanApplicationId && p.IsActive).SumAsync(p => p.Amount);
                    decimal totalPenaltyPaid = disbursement.Payments.Where(p => p.IsActive).Sum(p => p.PenaltyPaid);
                    decimal unpaidPenalties = Math.Max(0, totalPenalties - totalPenaltyPaid);

                    penaltyPart = Math.Min(remainingPayment, unpaidPenalties);
                    remainingPayment -= penaltyPart;
                }

                // 2. Interest — capped to the interest due for ONE installment
                //    e.g. debt=1000, interest=30, 2 installments => each installment interest = 15
                if (disbursement.TotalInstallments > 0 && remainingPayment > 0)
                {
                    decimal totalInterest = disbursement.PrincipalOffered * (disbursement.InterestRate / 100);
                    decimal interestPerInstallment = totalInterest / disbursement.TotalInstallments;
                    decimal totalInterestPaid = disbursement.Payments.Where(p => p.IsActive).Sum(p => p.InterestPaid);
                    decimal unpaidInterest = Math.Max(0, totalInterest - totalInterestPaid);

                    // Cap to per-installment interest so one payment doesn't clear ALL interest at once
                    decimal interestCap = Math.Min(unpaidInterest, interestPerInstallment);
                    interestPart = Math.Min(remainingPayment, interestCap);
                    remainingPayment -= interestPart;
                }

                // 3. Principal
                principalPart = remainingPayment;

                var payment = new Payment
                {
                    DisbursementId = paymentDTO.DisbursementId,
                    AccountId = paymentDTO.AccountId,
                    PaymentTypeId = paymentDTO.PaymentTypeId,
                    Amount = paymentDTO.Amount,
                    PrincipalPaid = principalPart,
                    InterestPaid = interestPart,
                    PenaltyPaid = penaltyPart,
                    PaymentDate = DateTime.Now,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    PersonId = user.Person.Id,
                    ProofOfPayment = paymentDTO.ProofOfPayment,
                    ProofOfPaymentFileName = paymentDTO.ProofOfPaymentFileName,
                };

                context.Payments.Add(payment);
                var applicationCode = disbursement.LoanApplication?.ApplicationCode ?? disbursement.LoanApplicationId.ToString();
                context.ActivityLogs.Add(ActivityLogFactory.Create(
                    _userContext,
                    "Payment Created",
                    nameof(Payment),
                    applicationCode,
                    $"Recorded payment of {paymentDTO.Amount:N2} for loan application {applicationCode}."));

                // 8. Update Loan Status if fully paid
                if (totalPaidSoFar + paymentDTO.Amount >= disbursement.Amount)
                {
                    disbursement.LoanApplication.Status = LoanStatus.Paid;
                    context.ActivityLogs.Add(ActivityLogFactory.Create(
                        _userContext,
                        "Loan Paid",
                        nameof(LoanApplication),
                        applicationCode,
                        $"Loan application {applicationCode} was fully paid."));
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task CreatePaymentWithPenaltyAsync(CreatePaymentDTO paymentDTO, decimal shortfall, decimal penaltyAmount)
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
                // 1. Fetch Disbursement + Application + LoanProduct + Setting
                var disbursement = await context.Disbursements
                    .Include(d => d.LoanApplication)
                        .ThenInclude(la => la.LoanProductSetting) // Updated navigation property name
                    
                    .Include(d => d.Payments)
                    .FirstOrDefaultAsync(d => d.Id == paymentDTO.DisbursementId);

                if (disbursement == null) throw new Exception("Disbursement not found.");

                // 2. Record Payment Amount in the target Account
                var account = await context.Accounts.FirstOrDefaultAsync(a => a.Id == paymentDTO.AccountId);
                if (account == null) throw new Exception("Target account not found.");
                account.Balance += paymentDTO.Amount;

                // 3. Get actual penalty rate from Product Settings for the description and calculation
                decimal rate = disbursement.LoanApplication?.LoanProductSetting?.PenalityRate ?? 0;
                
                // Recalculate penalty amount if it was passed as 0 (using the dynamic rate on the remaining installment balance)
                if (penaltyAmount == 0) 
                {
                    penaltyAmount = shortfall * (rate / 100m);
                }

                // Guard: Get a valid ReasonId
                var lateReason = await context.Reasons.FirstOrDefaultAsync(r => r.Name.Contains("Late") || r.Name.Contains("Overdue"));
                if (lateReason == null) lateReason = await context.Reasons.FirstOrDefaultAsync();
                
                int validReasonId = lateReason?.Id ?? 0;
                if (validReasonId <= 0)
                    throw new Exception("Please create at least one Penalty Reason in the system before generating penalties.");

                var penalty = new Penality
                {
                    LoanApplicationId = disbursement.LoanApplicationId,
                    PersonId = user.Person.Id,
                    Amount = penaltyAmount,
                    Date = DateTime.Now,
                    ReasonId = validReasonId, 
                    Description = $"Penalty ({rate}%). Remaining Installment Balance: {shortfall:N2}.",
                    IsActive = true
                };
                context.Penalties.Add(penalty);

                // 5. Add Penalty to the Loan Balance (Reflected in Loan Details)
                disbursement.Amount += penaltyAmount;

                // 6. Calculate Penalty, Interest vs Principal
                decimal penaltyPart = 0;
                decimal interestPart = 0;
                decimal principalPart = 0;
                decimal remainingPayment = paymentDTO.Amount;

                // 1. Penalty
                if (remainingPayment > 0)
                {
                    decimal totalPenalties = await context.Penalties.Where(p => p.LoanApplicationId == disbursement.LoanApplicationId && p.IsActive).SumAsync(p => p.Amount);
                    totalPenalties += penaltyAmount; // Add the one we just created in memory
                    
                    decimal totalPenaltyPaid = disbursement.Payments.Where(p => p.IsActive).Sum(p => p.PenaltyPaid);
                    decimal unpaidPenalties = Math.Max(0, totalPenalties - totalPenaltyPaid);

                    penaltyPart = Math.Min(remainingPayment, unpaidPenalties);
                    remainingPayment -= penaltyPart;
                }

                // 2. Interest — capped to the interest due for ONE installment
                //    e.g. debt=1000, interest=30, 2 installments => each installment interest = 15
                if (disbursement.TotalInstallments > 0 && remainingPayment > 0)
                {
                    decimal totalInterest = disbursement.PrincipalOffered * (disbursement.InterestRate / 100);
                    decimal interestPerInstallment = totalInterest / disbursement.TotalInstallments;
                    decimal totalInterestPaid = disbursement.Payments.Where(p => p.IsActive).Sum(p => p.InterestPaid);
                    decimal unpaidInterest = Math.Max(0, totalInterest - totalInterestPaid);

                    // Cap to per-installment interest so one payment doesn't clear ALL interest at once
                    decimal interestCap = Math.Min(unpaidInterest, interestPerInstallment);
                    interestPart = Math.Min(remainingPayment, interestCap);
                    remainingPayment -= interestPart;
                }

                // 3. Principal
                principalPart = remainingPayment;

                // 7. Create the Payment Record
                var payment = new Payment
                {
                    DisbursementId = paymentDTO.DisbursementId,
                    AccountId = paymentDTO.AccountId,
                    PaymentTypeId = paymentDTO.PaymentTypeId,
                    Amount = paymentDTO.Amount,
                    PrincipalPaid = principalPart,
                    InterestPaid = interestPart,
                    PenaltyPaid = penaltyPart,
                    PaymentDate = DateTime.Now,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    PersonId = user.Person.Id,
                    ProofOfPayment = paymentDTO.ProofOfPayment,
                    ProofOfPaymentFileName = paymentDTO.ProofOfPaymentFileName
                };
                context.Payments.Add(payment);
                var applicationCode = disbursement.LoanApplication?.ApplicationCode ?? disbursement.LoanApplicationId.ToString();
                context.ActivityLogs.Add(ActivityLogFactory.Create(
                    _userContext,
                    "Payment With Penalty Created",
                    nameof(Payment),
                    applicationCode,
                    $"Recorded payment of {paymentDTO.Amount:N2} and penalty of {penaltyAmount:N2} for loan application {applicationCode}."));

                // 7. Check if loan is finished
                decimal totalPaidSoFar = disbursement.Payments
                    .Where(p => p.IsActive)
                    .Sum(p => (decimal?)p.Amount ?? 0) + paymentDTO.Amount;

                if (totalPaidSoFar >= disbursement.Amount)
                {
                    disbursement.LoanApplication.Status = LoanStatus.Paid;
                    context.ActivityLogs.Add(ActivityLogFactory.Create(
                        _userContext,
                        "Loan Paid",
                        nameof(LoanApplication),
                        applicationCode,
                        $"Loan application {applicationCode} was fully paid."));
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<(decimal Amount, DateTime Date)> GetNextScheduledPaymentAsync(int disbursementId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            var disbursement = await context.Disbursements
                .Include(d => d.PaymentModality)
                .FirstOrDefaultAsync(d => d.Id == disbursementId);

            if (disbursement == null) return (0, DateTime.Today);

            var totalPaid = await context.Payments
                .Where(p => p.DisbursementId == disbursementId && p.IsActive)
                .SumAsync(p => (decimal?)p.Amount ?? 0);

            decimal installmentAmount = disbursement.Amount / disbursement.TotalInstallments;
            string mode = disbursement.PaymentModality?.Mode?.ToLower() ?? "monthly";
            DateTime today = DateTime.Today;

            for (int i = 0; i < disbursement.TotalInstallments; i++)
            {
                DateTime dueDate = mode switch
                {
                    "daily" => disbursement.StartDate.AddDays(i + 1),
                    "weekly" => disbursement.StartDate.AddDays((i + 1) * 7),
                    "monthly" => disbursement.StartDate.AddMonths(i + 1),
                    "yearly" => disbursement.StartDate.AddYears(i + 1),
                    _ => disbursement.StartDate.AddMonths(i + 1)
                };

                decimal cumulativeDueSoFar = installmentAmount * (i + 1);

                if (today <= dueDate || totalPaid < cumulativeDueSoFar)
                {
                    decimal paidForPreviousPeriods = installmentAmount * i;
                    decimal paidTowardsCurrent = Math.Max(0, totalPaid - paidForPreviousPeriods);
                    decimal remaining = Math.Max(0, installmentAmount - paidTowardsCurrent);

                    return (remaining, dueDate);
                }
            }
            
            return (0, DateTime.Today);
        }

        public async Task<List<Payment>> GetAllPaymentsAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            if (_userContext.Id == null)
            {
                return new List<Payment>();
            }
            var allowedPersonIds = await _userContext.GetAllowedPersonIdsAsync();
            return await context.Payments
                .AsNoTracking()
                .Where(a => allowedPersonIds.Contains(a.PersonId))
                .Include(i => i.Disbursement).ThenInclude(d => d.LoanApplication).ThenInclude(l => l.Borrower)
                .Include(i => i.Account)
                .Include(i => i.PaymentType)
                .OrderByDescending(p => p.PaymentDate)
                .OrderByDescending(x => x.Id).ToListAsync();
        }

        public async Task<Payment?> GetPaymentByIdAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            if (_userContext.Id == null)
            {
                return null;
            }
            var allowedPersonIds = await _userContext.GetAllowedPersonIdsAsync();
            return await context.Payments
                .AsNoTracking()
                .Where(a => allowedPersonIds.Contains(a.PersonId))
                .Include(i => i.Account)
                .Include(i => i.PaymentType)
                .FirstOrDefaultAsync(i => i.Id == id);
        }
    }
}

