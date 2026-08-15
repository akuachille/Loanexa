using Application.DTO;
using Application.Interfaces;
using Domain.Entities;


namespace Application.Interfaces
{
    public interface IAccount
    {
        Task<List<Account>> GetAllAccountsAsync();
        Task<Account?> GetAccountByIdAsync(int id);
        Task CreateAccountAsync(AccountCreateDTO accountCreateDTO);
        Task TransferFundsAsync(int fromAccountId, int toAccountId, decimal amount, string description);
        // Task UpdateAccountAsync(int id, AccountUpdateDTO accountUpdateDTO);
    }
}