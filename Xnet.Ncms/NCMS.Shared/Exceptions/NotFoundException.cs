namespace NCMS.Shared.Exceptions;

public class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message) { }

    public static NotFoundException For<T>(object id) =>
        new($"{typeof(T).Name} with id '{id}' was not found.");
}
