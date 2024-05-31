namespace Task.Connector.Exceptions;

public class UserAlreadyRegisteredException : ComponentException
{
    public UserAlreadyRegisteredException(string message)
    {
        ReasonPhrase = message;
    }
}