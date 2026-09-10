using PigPocket.Api.DTOs.Savings;

namespace PigPocket.Api.Services.FinanceEngine;

public class ProjectionCalculator
{
    public List<ProjectionPointDto> ProjectSavings(
        decimal currentSavings,
        decimal monthlySavingsAverage,
        int months)
    {
        var points = new List<ProjectionPointDto>();

        for (var month = 1; month <= months; month++)
        {
            points.Add(new ProjectionPointDto
            {
                Months = month,
                ProjectedSavings = currentSavings + (monthlySavingsAverage * month)
            });
        }

        return points;
    }
}
