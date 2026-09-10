 using Microsoft.EntityFrameworkCore;
using PigPocket.Api.Models;

namespace PigPocket.Api.Data;

public class FinanceDbContext : DbContext
{
    public FinanceDbContext(
        DbContextOptions<FinanceDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }

    public DbSet<Transaction> Transactions { get; set; }

    public DbSet<BankAccount> BankAccounts { get; set; }

    public DbSet<Category> Categories { get; set; }

    public DbSet<SavingsGoal> SavingsGoals { get; set; }

    public DbSet<Budget> Budgets { get; set; }

    public DbSet<SavingsAccount> SavingsAccounts { get; set; }

    public DbSet<InterestRate> InterestRates { get; set; }

    public DbSet<KycProfile> KycProfiles { get; set; }
}