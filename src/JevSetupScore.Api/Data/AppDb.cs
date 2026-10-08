using Microsoft.EntityFrameworkCore;

namespace JevSetupScore.Api.Data;

public sealed class TickerRow
{
    public string Symbol { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
}

public sealed class BarRow
{
    public long Id { get; set; }
    public string Symbol { get; set; } = "";
    public DateOnly Date { get; set; }
    public double Open { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public double Close { get; set; }
    public long Volume { get; set; }
}

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TickerRow> Tickers => Set<TickerRow>();
    public DbSet<BarRow> Bars => Set<BarRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TickerRow>().HasKey(ticker => ticker.Symbol);
        modelBuilder.Entity<TickerRow>().Property(ticker => ticker.Symbol).HasMaxLength(16);
        modelBuilder.Entity<BarRow>().HasIndex(bar => new { bar.Symbol, bar.Date }).IsUnique();
        modelBuilder.Entity<BarRow>().Property(bar => bar.Symbol).HasMaxLength(16);
    }
}
