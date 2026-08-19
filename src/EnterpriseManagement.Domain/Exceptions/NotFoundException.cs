namespace EnterpriseManagement.Domain.Exceptions;

/// <summary>Thrown when a requested entity does not exist. Maps to HTTP 404.</summary>
public class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message)
    {
    }

    /// <summary>
    /// Produces a message that names the type and key without echoing caller
    /// input back verbatim, e.g. <c>Employee with id 42 was not found.</c>
    /// </summary>
    public NotFoundException(string entityName, object key)
        : base($"{entityName} with id {key} was not found.")
    {
    }
}
