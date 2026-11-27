using System.Reflection;

namespace CryptoTrader.Host.Persistence;

internal static class EfContextLocator
{
    public static object? Resolve(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var dbContextType = LoadType("Microsoft.EntityFrameworkCore", "Microsoft.EntityFrameworkCore.DbContext");
        if (dbContextType is null)
        {
            return null;
        }

        var resolved = services.GetService(dbContextType);
        if (resolved is not null)
        {
            return resolved;
        }

        var infrastructure = LoadAssembly("CryptoTrader.Infrastructure");
        if (infrastructure is null)
        {
            return null;
        }

        foreach (var candidate in ConcreteTypes(infrastructure, dbContextType))
        {
            var context = services.GetService(candidate);
            if (context is not null)
            {
                return context;
            }
        }

        return null;
    }

    public static async Task<bool> CanConnectAsync(object context, CancellationToken cancellationToken)
    {
        var database = context.GetType().GetProperty("Database")?.GetValue(context);
        var method = database?.GetType().GetMethod("CanConnectAsync", [typeof(CancellationToken)]);
        if (database is null || method is null)
        {
            return false;
        }

        var task = method.Invoke(database, [cancellationToken]) as Task<bool>
            ?? throw new InvalidOperationException("Database connectivity check did not return a result.");
        return await task;
    }

    public static async Task MigrateAsync(object context, CancellationToken cancellationToken)
    {
        var database = context.GetType().GetProperty("Database")?.GetValue(context)
            ?? throw new InvalidOperationException("The DbContext does not expose a database facade.");
        var extensions = LoadType(
                "Microsoft.EntityFrameworkCore.Relational",
                "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions")
            ?? throw new InvalidOperationException("EF Core relational migrations are unavailable.");
        var method = extensions
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(candidate =>
            {
                if (!string.Equals(candidate.Name, "MigrateAsync", StringComparison.Ordinal))
                {
                    return false;
                }

                var parameters = candidate.GetParameters();
                return parameters.Length == 2
                    && parameters[0].ParameterType.IsInstanceOfType(database)
                    && parameters[1].ParameterType == typeof(CancellationToken);
            })
            ?? throw new InvalidOperationException("EF Core MigrateAsync was not found.");

        var task = method.Invoke(null, [database, cancellationToken]) as Task
            ?? throw new InvalidOperationException("EF Core MigrateAsync did not return a task.");
        await task;
    }

    private static IEnumerable<Type> ConcreteTypes(Assembly assembly, Type dbContextType)
    {
        IEnumerable<Type> types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            types = exception.Types.OfType<Type>();
        }

        return types
            .Where(type => type is { IsAbstract: false } && !type.IsGenericTypeDefinition && dbContextType.IsAssignableFrom(type))
            .OrderByDescending(type => type.Name.Contains("Trading", StringComparison.Ordinal) || type.Name.Contains("Crypto", StringComparison.Ordinal));
    }

    private static Type? LoadType(string assemblyName, string typeName) =>
        LoadAssembly(assemblyName)?.GetType(typeName, throwOnError: false);

    private static Assembly? LoadAssembly(string assemblyName)
    {
        try
        {
            return Assembly.Load(assemblyName);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (FileLoadException)
        {
            return null;
        }
    }
}
