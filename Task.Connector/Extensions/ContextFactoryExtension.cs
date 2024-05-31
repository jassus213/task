using Task.Connector.Data;
using Task.Integration.Data.DbCommon;

namespace Task.Connector.Extensions;

public static class ContextFactoryExtension
{
    public static DataContext EnsureCreate(this ApplicationContextFactory? factory)
    {
        if (factory == null)
            throw new ArgumentException(nameof(factory));

        return factory.Create();
    }
}