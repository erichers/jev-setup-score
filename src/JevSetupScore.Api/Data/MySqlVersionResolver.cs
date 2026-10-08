using Microsoft.EntityFrameworkCore;

namespace JevSetupScore.Api.Data;

public static class MySqlVersionResolver
{
    public static ServerVersion Resolve(string connectionString, string? configured)
    {
        try
        {
            return ServerVersion.AutoDetect(connectionString);
        }
        catch (Exception)
        {
            if (!string.IsNullOrWhiteSpace(configured))
                return ServerVersion.Parse(configured);

            throw new InvalidOperationException(
                "MySQL version was not detected. Set Database:MySqlVersion, for example 5.7.39-mysql.");
        }
    }
}
