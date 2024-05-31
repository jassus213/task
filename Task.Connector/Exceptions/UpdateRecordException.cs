namespace Task.Connector.Exceptions;

public class UpdateRecordException : ComponentException
{
    public UpdateRecordException(Exception inner) : base(inner)
    {
    }
}