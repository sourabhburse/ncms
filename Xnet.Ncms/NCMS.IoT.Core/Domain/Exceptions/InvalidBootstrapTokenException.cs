namespace NCMS.IoT.Core.Domain.Exceptions;

public sealed class InvalidBootstrapTokenException : Exception
{
    public InvalidBootstrapTokenException()
        : base("The provided factory bootstrap token is invalid or has already been consumed.") { }

    public InvalidBootstrapTokenException(string message) : base(message) { }
}
