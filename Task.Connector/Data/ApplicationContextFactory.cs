using Microsoft.EntityFrameworkCore;
using Task.Integration.Data.DbCommon;

namespace Task.Connector.Data;

public class ApplicationContextFactory
{
    private readonly string _connectionString;

    public ApplicationContextFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public DataContext Create()
    {
        var optionsBuilder = new DbContextOptionsBuilder<DataContext>();
        var dataBaseType = GetDataBaseType(_connectionString);

        switch (dataBaseType)
        {
            case DataBaseType.Postgres:
                optionsBuilder.UseNpgsql(
                    ExtractConnectionString(_connectionString));
                break;

            case DataBaseType.MsSql:
                optionsBuilder.UseSqlServer(ExtractConnectionString(_connectionString));
                break;

            case DataBaseType.Unknown:
                throw new ArgumentException("Unknown DataBase Provider In Connection String",
                    nameof(_connectionString));
        }

        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

        return new DataContext(optionsBuilder.Options);
    }


    private static DataBaseType GetDataBaseType(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return DataBaseType.Unknown;

        if (connectionString.Contains("SqlServer", StringComparison.InvariantCultureIgnoreCase))
            return DataBaseType.MsSql;

        return connectionString.Contains("PostgreSql", StringComparison.InvariantCultureIgnoreCase)
            ? DataBaseType.Postgres
            : DataBaseType.Unknown;
    }

    private static string ExtractConnectionString(string input)
    {
        const string startDelimiter = "ConnectionString='";
        const string endDelimiter = "';";

        var startIndex = input.IndexOf(startDelimiter, StringComparison.Ordinal) + startDelimiter.Length;
        var endIndex = input.IndexOf(endDelimiter, startIndex, StringComparison.Ordinal);

        if (startIndex < startDelimiter.Length || endIndex < 0)
            throw new ApplicationException("The input string does not contain a valid ConnectionString.");

        return input.Substring(startIndex, endIndex - startIndex);
    }
}