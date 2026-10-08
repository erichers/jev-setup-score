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

public sealed class ScoreQuery
{
    public long Id { get; set; }
    public string Symbol { get; set; } = "";
    public int Horizon { get; set; }
    public int Threshold { get; set; }
    public int Score { get; set; }
    public double Probability { get; set; }
    public DateOnly AsOf { get; set; }
    public string DataSource { get; set; } = "";
    public string Summary { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
}

public abstract class AppDbContext : DbContext
{
    protected AppDbContext(DbContextOptions options) : base(options)
    {
    }

    public DbSet<TickerRow> Tickers => Set<TickerRow>();
    public DbSet<BarRow> Bars => Set<BarRow>();
    public DbSet<ScoreQuery> ScoreQueries => Set<ScoreQuery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TickerRow>(entity =>
        {
            entity.HasKey(ticker => ticker.Symbol);
            entity.Property(ticker => ticker.Symbol).HasMaxLength(16);
            entity.Property(ticker => ticker.Name).HasMaxLength(80);
            entity.Property(ticker => ticker.Kind).HasMaxLength(16);
        });

        modelBuilder.Entity<BarRow>(entity =>
        {
            entity.HasIndex(bar => new { bar.Symbol, bar.Date }).IsUnique();
            entity.Property(bar => bar.Symbol).HasMaxLength(16);
        });

        modelBuilder.Entity<ScoreQuery>(entity =>
        {
            entity.Property(query => query.Symbol).HasMaxLength(16);
            entity.Property(query => query.DataSource).HasMaxLength(16);
            entity.Property(query => query.Summary).HasMaxLength(500);
            entity.HasIndex(query => query.CreatedUtc);
        });
    }
}

public sealed class SqliteAppDbContext : AppDbContext
{
    public SqliteAppDbContext(DbContextOptions<SqliteAppDbContext> options) : base(options)
    {
    }
}

public sealed class MysqlAppDbContext : AppDbContext
{
    public MysqlAppDbContext(DbContextOptions<MysqlAppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasCharSet("utf8mb4");
        modelBuilder.UseCollation("utf8mb4_unicode_ci");
    }
}
