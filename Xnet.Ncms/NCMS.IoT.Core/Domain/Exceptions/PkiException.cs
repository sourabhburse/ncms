namespace NCMS.IoT.Core.Domain.Exceptions;

public sealed class PkiException : Exception
{
    public PkiException(string message) : base(message) { }
    public PkiException(string message, Exception inner) : base(message, inner) { }
}
