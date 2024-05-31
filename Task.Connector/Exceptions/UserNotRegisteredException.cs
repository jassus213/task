namespace Task.Connector.Exceptions;

public class UserNotRegisteredException : ComponentException
{
    public UserNotRegisteredException(string message) : base(message)
    {
        
    }
}