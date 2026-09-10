using PigPocket.Api.Models;
using PigPocket.Api.Models.Enums;

namespace PigPocket.Api.Services.FinanceEngine;

public class SpendingCalculator
{
    public decimal CalculateEligibleIncome(IEnumerable<Transaction> transactions)
    {
        return transactions
            .Where(transaction =>
                transaction.Type == TransactionType.Credit
                && !transaction.IsInternalTransfer)
            .Sum(transaction => transaction.Amount);
    }

    public decimal CalculateEligibleSpending(IEnumerable<Transaction> transactions)
    {
        return transactions
            .Where(transaction => transaction.CountsAsSpending)
            .Sum(transaction => transaction.Amount);
    }
}
