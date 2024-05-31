using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Task.Connector.Extensions;

public static class QueryableExecutePatchUpdateExtensions
{
    public static Task<int> ExecutePatchUpdateAsync<TSource>(
        this IQueryable<TSource> source,
        Action<SetPropertyBuilder<TSource>> setPropertyBuilder,
        CancellationToken ct = default
    )
    {
        var builder = new SetPropertyBuilder<TSource>();
        setPropertyBuilder.Invoke(builder);
        return source.ExecuteUpdateAsync(builder.SetPropertyCalls, ct);
    }

    public static int ExecutePatchUpdate<TSource>(
        this IQueryable<TSource> source,
        Action<SetPropertyBuilder<TSource>> setPropertyBuilder
    )
    {
        var builder = new SetPropertyBuilder<TSource>();
        setPropertyBuilder.Invoke(builder);
        return source.ExecuteUpdate(builder.SetPropertyCalls);
    }
}

public class SetPropertyBuilder<TSource>
{
    public Expression<Func<SetPropertyCalls<TSource>, SetPropertyCalls<TSource>>>
        SetPropertyCalls { get; private set; } = b => b;

    public SetPropertyBuilder<TSource> SetProperty<TProperty>(
        Expression<Func<TSource, TProperty>> propertyExpression,
        TProperty value
    ) => SetProperty(propertyExpression, _ => value);

    private SetPropertyBuilder<TSource> SetProperty<TProperty>(
        Expression<Func<TSource, TProperty>> propertyExpression,
        Expression<Func<TSource, TProperty>> valueExpression
    )
    {
        SetPropertyCalls = SetPropertyCalls.Update(
            body: Expression.Call(
                instance: SetPropertyCalls.Body,
                methodName: nameof(SetPropertyCalls<TSource>.SetProperty),
                typeArguments: new[] { typeof(TProperty) },
                arguments: new Expression[]
                {
                    propertyExpression,
                    valueExpression
                }
            ),
            parameters: SetPropertyCalls.Parameters
        );

        return this;
    }
}