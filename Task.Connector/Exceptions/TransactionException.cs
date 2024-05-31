namespace Task.Connector.Exceptions;

public class TransactionException : ComponentException
{
    public TransactionException(Exception innerException) : base(innerException)
    {
    }
}