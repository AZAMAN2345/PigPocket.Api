namespace PigPocket.Api.Services.FinanceEngine;

public class SavingsCalculator
{
    private const decimal OnTargetTolerance = 0.01m;

    public decimal CalculateActualSavings(decimal income, decimal spending)
    {
        return income - spending;
    }

    public decimal CalculateExpectedSavings(decimal income, decimal targetRate)
    {
        return income * (targetRate / 100m);
    }

    public decimal CalculateSavingsRate(decimal income, decimal savings)
    {
        if (income <= 0)
        {
            return 0;
        }

        return savings / income * 100m;
    }

    public string GetSavingsStatus(decimal actualSavings, decimal expectedSavings)
    {
        var difference = actualSavings - expectedSavings;

        if (Math.Abs(difference) <= OnTargetTolerance)
        {
            return "OnTarget";
        }

        return difference > 0 ? "Ahead" : "Behind";
    }
}
