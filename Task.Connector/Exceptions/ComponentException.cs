namespace Task.Connector.Exceptions;

public class ComponentException : Exception
{
    public string ReasonPhrase;

    protected ComponentException(string reasonPhrase) : base(reasonPhrase)
    {
        ReasonPhrase = reasonPhrase;
    }

    protected ComponentException(Exception innerException,
        string reasonPhrase = "An unexpected error has occurred, please try again later") : base(reasonPhrase,
        innerException)
    {
    }

    protected ComponentException()
    {
    }
}