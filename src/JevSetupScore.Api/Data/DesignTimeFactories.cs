using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JevSetupScore.Api.Data;

public sealed class SqliteDesignTimeFactory : IDesignTimeDbContextFactory<SqliteAppDbContext>
{
    public SqliteAppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SqliteAppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        return new SqliteAppDbContext(options);
    }
}

public sealed class MysqlDesignTimeFactory : IDesignTimeDbContextFactory<MysqlAppDbContext>
{
    public MysqlAppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("JEV_MYSQL_DESIGN");
        if (string.IsNullOrWhiteSpace(connection))
            connection = "Server=127.0.0.1;Port=3306;Database=jev_setup_score;User=jev";

        var options = new DbContextOptionsBuilder<MysqlAppDbContext>()
            .UseMySql(connection, ServerVersion.Parse("8.4.0-mysql"))
            .Options;
        return new MysqlAppDbContext(options);
    }
}
