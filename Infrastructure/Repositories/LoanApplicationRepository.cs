using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Application.DTO;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
namespace Infrastructure.Repositories
{
    public class LoanApplicationRepository : ILoanApplication
    {
        // private readonly ApplicationDbContext dbContext;
        // public LoanApplicationRepository(ApplicationDbContext context)
     private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
     private readonly IUserContext _userContext;
     private readonly IEmailService _emailService;

    public LoanApplicationRepository(IDbContextFactory<ApplicationDbContext> contextFactory, IUserContext userContext, IEmailService emailService)
        {
        //    dbContext=context; 
         _contextFactory = contextFactory;
         _userContext = userContext;
         _emailService = emailService;
        }
        public  async Task<List<LoanApplication>> GetAllLoanApplicationsAsync()
        {
            using var dbContext = await _contextFactory.CreateDbContextAsync();
            if (_userContext.Id == null)
            {
                return new List<LoanApplication>();
            }
            var allowedPersonIds = await _userContext.GetAllowedPersonIdsAsync();
            return await dbContext.LoanApplications
                .Include(a => a.LoanProductSetting)
                    .ThenInclude(s => s.LoanProduct)
                .Include(a => a.Borrower)
                .Include(a => a.PaymentModality)
                .Where(a => allowedPersonIds.Contains(a.PersonId))
                .ToListAsync();
        }
        public async Task <LoanApplication> GetLoanApplicationById(int Id)
        {
            if (_userContext.Id == null)
            {
                return null;
            }
            using var dbContext = await _contextFactory.CreateDbContextAsync();
            var allowedPersonIds = await _userContext.GetAllowedPersonIdsAsync();
            return await dbContext.LoanApplications
                .Include(a => a.LoanProductSetting)
                    .ThenInclude(s => s.LoanProduct)
                .Include(a => a.Borrower)
                .Include(a => a.PaymentModality)
                .Where(a => allowedPersonIds.Contains(a.PersonId))
                .FirstOrDefaultAsync(a => a.Id == Id);
        }
         public async Task<LoanApplication> CreateLoanApplication(CreateApplicationDTO loanApplicationDTO)
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

        var borrower = await dbContext.Borrowers.FindAsync(loanApplicationDTO.BorrowerId);
        var loanProductSetting = await dbContext.LoanProductSettings.FindAsync(loanApplicationDTO.LoanProductSettingId);
        var paymentModality = await dbContext.PaymentModalities.FindAsync(loanApplicationDTO.PaymentModalityId);

        if (borrower == null || loanProductSetting == null || paymentModality == null)
        {
            throw new Exception("One or more related entities required for loan application creation were not found.");
        }

        var randomSuffix = new Random().Next(1000, 9999);
        var generatedCode = !string.IsNullOrWhiteSpace(borrower.CompanyName)
            ? $"LN-{DateTime.Now.Year}-{borrower.CompanyName}-{randomSuffix}"
            : $"LN-{DateTime.Now.Year}-{borrower.FirstName}-{borrower.LastName}-{randomSuffix}";
        var currentUserName = string.IsNullOrWhiteSpace(_userContext.FullName) ? _userContext.Email : _userContext.FullName;
        
            var _loanApplication = new LoanApplication
            {
                ApplicationCode = generatedCode,
                BorrowerId = borrower.Id,
                PersonId = user.Person.Id,
                LoanProductSettingId = loanProductSetting.Id,
                PaymentModalityId = paymentModality.Id,
                AmountRequested = loanApplicationDTO.AmountRequested,
                DateofApplication = DateTime.Now,
                Status = LoanStatus.Applied,
                PreferredDate = DateTime.Now,
                ApprovedBy = currentUserName,
                CreatedBy = currentUserName
            };

            dbContext.LoanApplications.Add(_loanApplication);
            dbContext.ActivityLogs.Add(ActivityLogFactory.Create(
                _userContext,
                "Loan Application Created",
                nameof(LoanApplication),
                generatedCode,
                $"Created loan application {generatedCode} for {borrower.FirstName} {borrower.LastName}."));
            


            await dbContext.SaveChangesAsync();
            return _loanApplication;
        }
        public async Task UpdateLoanApplication(int Id, UpdateApplicationDTO loanApplicationDTO)
            {
                using var dbContext = await _contextFactory.CreateDbContextAsync();
                
                var _loanApplication = await dbContext.LoanApplications
                    .Include(a => a.LoanProductSetting)
                    .FirstOrDefaultAsync(t => t.Id == Id);

                if (_loanApplication != null)
                {
                    var oldStatus = _loanApplication.Status;
                    var oldAmount = _loanApplication.AmountRequested;

                    if (loanApplicationDTO.Status == LoanStatus.Approved && oldStatus != LoanStatus.Approved)
                    {
                        await ValidateApprovalRequirementsAsync(dbContext, Id);
                    }

                    // Update fields
                    _loanApplication.PaymentModalityId = loanApplicationDTO.PaymentModalityId;
                    _loanApplication.AmountRequested = loanApplicationDTO.AmountRequested;
                    _loanApplication.ApprovedBy = loanApplicationDTO.ApprovedBy;
                    _loanApplication.Status = loanApplicationDTO.Status; // Assuming DTO uses LoanStatus enum
                    _loanApplication.PreferredDate = loanApplicationDTO.PreferredDate;
                    _loanApplication.DateofApplication = loanApplicationDTO.DateofApplication;
                    if (loanApplicationDTO.Status == LoanStatus.Rejected && !string.IsNullOrEmpty(loanApplicationDTO.RejectionReason))
                    {
                        _loanApplication.RejectionReason = loanApplicationDTO.RejectionReason;
                    }

                    dbContext.LoanApplications.Update(_loanApplication);
                    var logMessage = $"Edited loan application {_loanApplication.ApplicationCode}. Amount {oldAmount:N2} to {_loanApplication.AmountRequested:N2}; status {oldStatus} to {_loanApplication.Status}.";
                    if (_loanApplication.Status == LoanStatus.Rejected && !string.IsNullOrEmpty(_loanApplication.RejectionReason))
                    {
                        logMessage += $" Reason: {_loanApplication.RejectionReason}";
                    }

                    dbContext.ActivityLogs.Add(ActivityLogFactory.Create(
                        _userContext,
                        "Loan Application Edited",
                        nameof(LoanApplication),
                        _loanApplication.ApplicationCode,
                        logMessage));

                    if (_loanApplication.Status == LoanStatus.Rejected && oldStatus != LoanStatus.Rejected && !string.IsNullOrEmpty(_loanApplication.Borrower?.Email))
                    {
                        var subject = $"Update on your Loan Application: {_loanApplication.ApplicationCode}";
                        var body = $@"<p>Dear {(_loanApplication.Borrower.CompanyName ?? _loanApplication.Borrower.FirstName)},</p>
                                      <p>We regret to inform you that your loan application (<strong>{_loanApplication.ApplicationCode}</strong>) has been rejected.</p>
                                      <p><strong>Reason for rejection:</strong> {_loanApplication.RejectionReason}</p>
                                      <p>If you have any questions or require further clarification, please contact our support team.</p>
                                      <br/>
                                      <p>Sincerely,<br/>The Loan Operations Team</p>";
                        await _emailService.SendEmailAsync(_loanApplication.Borrower.Email, subject, body);
                    }

                    await dbContext.SaveChangesAsync();
               }
           }

public async Task<List<LoanApplication>> GetFilteredLoansAsync(string role, int? currentUserId = null)
{
    using var dbContext = await _contextFactory.CreateDbContextAsync();

    var query = dbContext.LoanApplications
        .Include(a => a.LoanProductSetting)
            .ThenInclude(s => s.LoanProduct)
        .Include(a => a.Borrower)
        .AsQueryable();

    if (role == "LoanManager")
    {
        // Use the Enum directly here
        query = query.Where(l => l.Status == LoanStatus.Applied);
    }
    else if (role != "Admin" && currentUserId.HasValue)
    {
        query = query.Where(l => l.BorrowerId == currentUserId.Value);
    }

    return await query.ToListAsync();
}

public async Task UpdateStatusAsync(int id, LoanStatus newStatus)
{
    using var dbContext = await _contextFactory.CreateDbContextAsync();

    var loan = await dbContext.LoanApplications.FindAsync(id);
    if (loan != null)
    {
        var oldStatus = loan.Status;

        if (newStatus == LoanStatus.Approved && oldStatus != LoanStatus.Approved)
        {
            await ValidateApprovalRequirementsAsync(dbContext, id);
        }

        loan.Status = newStatus;

        dbContext.ActivityLogs.Add(ActivityLogFactory.Create(
            _userContext,
            GetLoanStatusAction(newStatus),
            nameof(LoanApplication),
            loan.ApplicationCode,
            $"Changed loan application {loan.ApplicationCode} status from {oldStatus} to {newStatus}."));

        await dbContext.SaveChangesAsync();
    }
}

private async Task ValidateApprovalRequirementsAsync(ApplicationDbContext dbContext, int loanApplicationId)
{
    var loanApp = await dbContext.LoanApplications
        .Include(a => a.LoanProductSetting)
        .FirstOrDefaultAsync(a => a.Id == loanApplicationId);

    if (loanApp?.LoanProductSetting == null) return;

    var requiredDocuments = await dbContext.Requirements
        .Include(r => r.RequiredDocument)
        .Where(r => r.LoanProductId == loanApp.LoanProductSetting.LoanProductId && r.RequiredDocument != null)
        .Select(r => r.RequiredDocument.DocumentName)
        .ToListAsync();

    var providedDocuments = await dbContext.ProvidedDocuments
        .Where(pd => pd.LoanApplicationId == loanApplicationId)
        .Select(pd => pd.DocumentName)
        .ToListAsync();

    var missingDocuments = requiredDocuments
        .Where(rd => rd != null && !providedDocuments.Contains(rd))
        .ToList();

    if (missingDocuments.Any())
    {
        throw new InvalidOperationException($"Cannot approve application. The borrower has not provided all required documents. Missing: {string.Join(", ", missingDocuments)}");
    }
}

private static string GetLoanStatusAction(LoanStatus status)
{
    return status switch
    {
        LoanStatus.Approved => "Loan Approval",
        LoanStatus.Rejected => "Loan Rejection",
        LoanStatus.Disbursed => "Loan Disbursed",
        LoanStatus.Paid => "Loan Paid",
        _ => "Loan Status Updated"
    };
}

public async Task<List<TransactionHistoryDTO>> GetTransactionHistoryAsync(int loanApplicationId)
{
    using var dbContext = await _contextFactory.CreateDbContextAsync();
    var history = new List<TransactionHistoryDTO>();

    // 1. Process Fees
    var processFees = await dbContext.ProcessFeeDeposits
        .Where(p => p.LoanApplicationId == loanApplicationId)
        .ToListAsync();

    history.AddRange(processFees.Select(p => new TransactionHistoryDTO
    {
        TransactionDate = p.DepositDate,
        TransactionType = "Process Fee",
        Amount = p.Amount,
        Description = $"Processing Fee Deposit (Status: {p.Status})"
    }));

    // 2. Disbursements
    var disbursements = await dbContext.Disbursements
        .Where(d => d.LoanApplicationId == loanApplicationId && d.IsActive)
        .ToListAsync();

    history.AddRange(disbursements.Select(d => new TransactionHistoryDTO
    {
        TransactionDate = d.StartDate,
        TransactionType = "Disbursement",
        Amount = d.PrincipalOffered,
        Description = $"Loan Disbursement (Principal: {d.PrincipalOffered:N2})"
    }));

    // 3. Payments
    var disbursementIds = disbursements.Select(d => d.Id).ToList();
    if (disbursementIds.Any())
    {
        var payments = await dbContext.Payments
            .Include(p => p.PaymentType)
            .Where(p => disbursementIds.Contains(p.DisbursementId) && p.IsActive)
            .ToListAsync();

        history.AddRange(payments.Select(p => new TransactionHistoryDTO
        {
            TransactionDate = p.PaymentDate,
            TransactionType = "Payment",
            Amount = p.Amount,
            Description = $"Loan Payment ({p.PaymentType?.PaymentTypeName ?? "Standard"})"
        }));
    }

    return history.OrderByDescending(t => t.TransactionDate).ToList();
}

        public async Task DeleteLoanApplicationAsync(int id)
        {
            using var dbContext = await _contextFactory.CreateDbContextAsync();
            var loanApplication = await dbContext.LoanApplications.FindAsync(id);
            if (loanApplication == null)
            {
                throw new KeyNotFoundException("Loan application not found.");
            }

            // Validate status: only Applied or Rejected applications can be deleted
            if (loanApplication.Status == LoanStatus.Disbursed ||
                loanApplication.Status == LoanStatus.Approved ||
                loanApplication.Status == LoanStatus.Paid ||
                loanApplication.Status == LoanStatus.Rescheduled)
            {
                throw new InvalidOperationException($"Cannot delete loan application {loanApplication.ApplicationCode} because its status is {loanApplication.Status}. Only Applied or Rejected applications can be deleted.");
            }

            dbContext.LoanApplications.Remove(loanApplication);
            dbContext.ActivityLogs.Add(ActivityLogFactory.Create(
                _userContext,
                "Loan Application Deleted",
                nameof(LoanApplication),
                loanApplication.ApplicationCode,
                $"Deleted loan application {loanApplication.ApplicationCode} for amount {loanApplication.AmountRequested:N2}."
            ));

            await dbContext.SaveChangesAsync();
        }
    }
}
