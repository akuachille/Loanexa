using Domain.Entities;
using Application.DTO;
using Application.Interfaces;

namespace Application.Services.Payments
{
    public class PaymentService : IPaymentService
    {
        private readonly IPayment _payment;

        public PaymentService(IPayment payment)
        {
            _payment = payment;
        }

        public async Task<Payment?> GetPaymentByIdAsync(int id) => await _payment.GetPaymentByIdAsync(id);

        public async Task<List<Payment>> GetAllPaymentsAsync() => await _payment.GetAllPaymentsAsync();

        public async Task CreatePaymentAsync(CreatePaymentDTO paymentDTO)
        {
            // 1. Fetch current installment status
            var (expectedAmount, dueDate) = await _payment.GetNextScheduledPaymentAsync(paymentDTO.DisbursementId);

            DateTime today = DateTime.Today; 
            bool isAfterDueDate = today > dueDate;
            
            // 2. We calculate penalty on the remaining balance of the installment (expectedAmount)
            if (isAfterDueDate && expectedAmount > 0 && !paymentDTO.IsPenaltyPayment)
            {
                // SCENARIO: Payment is late. Apply dynamic penalty rate on the installment's remaining balance.
                // We pass expectedAmount as the "shortfall/balance" and 0 for penaltyAmount so the repository computes it using the dynamic rate.
                await _payment.CreatePaymentWithPenaltyAsync(paymentDTO, expectedAmount, 0);
            }
            else
            {
                // SCENARIO: On-time payment, overpayment, or clearing the balance, or explicit penalty payment.
                await _payment.CreatePaymentAsync(paymentDTO);
            }
        }

        public async Task CreatePaymentWithPenaltyAsync(CreatePaymentDTO paymentDTO, decimal shortfall, decimal penaltyAmount)
        {
            await _payment.CreatePaymentWithPenaltyAsync(paymentDTO, shortfall, penaltyAmount);
        }

        public async Task<(decimal Amount, DateTime Date)> GetNextScheduledPaymentAsync(int disbursementId)
        {
            return await _payment.GetNextScheduledPaymentAsync(disbursementId);
        }
    }
}