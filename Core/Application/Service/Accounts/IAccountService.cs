using Application.DTO;
using Domain.Entities;

namespace Application.Interfaces
{
    public interface IAccountService
    {
        Task<List<Account>> GetAllAccountsAsync();
        Task<Account?> GetAccountByIdAsync(int id);
        Task CreateAccountAsync(AccountCreateDTO accountCreateDTO);
        Task TransferFundsAsync(int fromAccountId, int toAccountId, decimal amount, string description);
    }
}
