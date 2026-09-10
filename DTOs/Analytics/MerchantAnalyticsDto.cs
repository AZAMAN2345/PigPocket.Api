namespace PigPocket.Api.DTOs.Analytics;

public class MerchantAnalyticsItemDto
{
    public string Merchant { get; set; } = "";
    public decimal Amount { get; set; }
    public int TransactionCount { get; set; }
    public decimal PercentageOfSpending { get; set; }
}

public class MerchantAnalyticsDto
{
    public decimal TotalSpending { get; set; }
    public List<MerchantAnalyticsItemDto> Merchants { get; set; } = [];
}
