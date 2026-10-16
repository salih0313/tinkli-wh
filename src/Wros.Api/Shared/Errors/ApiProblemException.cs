namespace Wros.Api.Shared.Errors;

public sealed class ApiProblemException(int status, string title) : Exception(title)
{
    public int Status { get; } = status;
}
