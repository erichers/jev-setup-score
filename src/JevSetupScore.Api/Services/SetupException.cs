namespace JevSetupScore.Api.Services;

public sealed class SetupException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}
