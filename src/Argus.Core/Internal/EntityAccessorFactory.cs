using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Argus.Configuration;
using Argus.Contracts;

namespace Argus.Internal;

/// <summary>
/// Works out how to read identity and position out of an arbitrary application entity type.
/// </summary>
/// <remarks>
/// <para>
/// The compatibility facade takes an <c>IEnumerable&lt;TEntity&gt;</c> of an application type
/// that Argus cannot reference, and still has to produce a group centroid from it. Three
/// routes, tried in order:
/// </para>
/// <list type="number">
/// <item><description><c>TEntity : IArgusEntity</c> — a cast and four property reads.</description></item>
/// <item><description>A delegate the application registered on <c>MonitorOptions.Accessors</c>.</description></item>
/// <item><description>Convention — property names matched against the configured candidate
/// lists, compiled into an expression tree once per type and cached.</description></item>
/// </list>
/// <para>
/// If all three fail, this throws. It does not return zero. A position of <c>(0,0)</c>
/// invented for an entity whose position could not be read is the single most damaging thing
/// this library could do: it is a legal-looking position that will enter group centroids,
/// trip jump detection on the following tick, and generate confident findings about an
/// entity nobody ever measured. One loud exception at first use is cheaper by orders of
/// magnitude.
/// </para>
/// <para>
/// The convention route compiles expression trees, which needs a JIT. On a platform without
/// one, use route 1 or route 2 — the exception message says so.
/// </para>
/// </remarks>
internal static class EntityAccessorFactory
{
    /// <summary>
    /// Returns a cached accessor for <typeparamref name="TEntity"/>, building one if needed.
    /// </summary>
    /// <typeparam name="TEntity">The application's entity type.</typeparam>
    /// <param name="options">The options carrying the registry, the candidate names and the cache.</param>
    /// <returns>A delegate that reads a snapshot out of an entity.</returns>
    /// <exception cref="EntityAccessorException">No route resolved a position.</exception>
    internal static Func<TEntity, EntitySnapshot> Resolve<TEntity>(MonitorOptions options)
    {
        object cached = options.AccessorCache.GetOrAdd(typeof(TEntity), _ => Build<TEntity>(options));
        var accessor = cached as Func<TEntity, EntitySnapshot>;
        if (accessor == null)
        {
            throw new EntityAccessorException(
                "The cached accessor for " + typeof(TEntity).FullName + " was of an unexpected type. "
                + "This indicates two monitors sharing a MonitorOptions instance with different generic arguments for the same type.");
        }

        return accessor;
    }

    private static Func<TEntity, EntitySnapshot> Build<TEntity>(MonitorOptions options)
    {
        // Route 1: the interface.
        if (typeof(IArgusEntity).IsAssignableFrom(typeof(TEntity)))
        {
            return entity =>
            {
                var argusEntity = (IArgusEntity?)entity;
                if (argusEntity == null)
                {
                    return default(EntitySnapshot);
                }

                return new EntitySnapshot(
                    argusEntity.EntityId,
                    argusEntity.Latitude,
                    argusEntity.Longitude,
                    argusEntity.Altitude);
            };
        }

        // Route 2: a registered delegate.
        Func<TEntity, EntitySnapshot>? registered;
        if (options.Accessors.TryGet<TEntity>(out registered) && registered != null)
        {
            return registered;
        }

        // Route 3: convention.
        MemberInfo? latitude = FindMember(typeof(TEntity), options.LatitudeCandidates);
        MemberInfo? longitude = FindMember(typeof(TEntity), options.LongitudeCandidates);

        if (latitude == null || longitude == null)
        {
            throw new EntityAccessorException(BuildFailureMessage(typeof(TEntity), options, latitude, longitude));
        }

        MemberInfo? altitude = FindMember(typeof(TEntity), options.AltitudeCandidates);
        MemberInfo? identity = FindMember(typeof(TEntity), options.IdentityCandidates, requireNumeric: false);

        try
        {
            return CompileConventionAccessor<TEntity>(latitude, longitude, altitude, identity);
        }
        catch (Exception exception)
        {
            throw new EntityAccessorException(
                "Argus matched properties on " + typeof(TEntity).FullName
                + " by convention but could not compile an accessor for them. "
                + "This usually means the runtime does not support compiled expression trees, in which case "
                + "register a delegate on MonitorOptions.Accessors instead, or implement IArgusEntity.",
                exception);
        }
    }

    private static Func<TEntity, EntitySnapshot> CompileConventionAccessor<TEntity>(
        MemberInfo latitude,
        MemberInfo longitude,
        MemberInfo? altitude,
        MemberInfo? identity)
    {
        ParameterExpression parameter = Expression.Parameter(typeof(TEntity), "entity");

        Expression latitudeExpression = ToNullableDouble(Expression.MakeMemberAccess(parameter, latitude));
        Expression longitudeExpression = ToNullableDouble(Expression.MakeMemberAccess(parameter, longitude));

        Expression altitudeExpression = altitude == null
            ? (Expression)Expression.Constant(null, typeof(double?))
            : ToNullableDouble(Expression.MakeMemberAccess(parameter, altitude));

        Expression identityExpression = identity == null
            ? (Expression)Expression.Constant(null, typeof(string))
            : ToInvariantString(Expression.MakeMemberAccess(parameter, identity));

        ConstructorInfo constructor = typeof(EntitySnapshot).GetConstructor(
            new[] { typeof(string), typeof(double?), typeof(double?), typeof(double?) })!;

        NewExpression body = Expression.New(
            constructor,
            identityExpression,
            latitudeExpression,
            longitudeExpression,
            altitudeExpression);

        return Expression.Lambda<Func<TEntity, EntitySnapshot>>(body, parameter).Compile();
    }

    private static Expression ToNullableDouble(Expression source)
    {
        Type type = source.Type;
        Type? underlying = Nullable.GetUnderlyingType(type);

        if (type == typeof(double?))
        {
            return source;
        }

        if (type == typeof(double))
        {
            return Expression.Convert(source, typeof(double?));
        }

        if (underlying != null)
        {
            // A nullable of some other numeric type: convert through the nullable form so a
            // null stays null rather than becoming zero. Reading an absent value as zero is
            // the exact confusion architecture rule 6 exists to prevent.
            return Expression.Convert(source, typeof(double?));
        }

        return Expression.Convert(Expression.Convert(source, typeof(double)), typeof(double?));
    }

    private static Expression ToInvariantString(Expression source)
    {
        if (source.Type == typeof(string))
        {
            return source;
        }

        MethodInfo toString = typeof(Convert).GetMethod(
            nameof(Convert.ToString),
            new[] { typeof(object), typeof(IFormatProvider) })!;

        return Expression.Call(
            toString,
            Expression.Convert(source, typeof(object)),
            Expression.Constant(CultureInfo.InvariantCulture, typeof(IFormatProvider)));
    }

    private static MemberInfo? FindMember(Type type, IEnumerable<string> candidates, bool requireNumeric = true)
    {
        foreach (string candidate in candidates)
        {
            PropertyInfo? property = FindProperty(type, candidate);
            if (property != null && (!requireNumeric || IsNumeric(property.PropertyType)))
            {
                return property;
            }

            FieldInfo? field = FindField(type, candidate);
            if (field != null && (!requireNumeric || IsNumeric(field.FieldType)))
            {
                return field;
            }
        }

        return null;
    }

    private static PropertyInfo? FindProperty(Type type, string name)
    {
        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        for (int i = 0; i < properties.Length; i++)
        {
            PropertyInfo property = properties[i];
            if (property.CanRead
                && property.GetIndexParameters().Length == 0
                && string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property;
            }
        }

        return null;
    }

    private static FieldInfo? FindField(Type type, string name)
    {
        FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
        for (int i = 0; i < fields.Length; i++)
        {
            if (string.Equals(fields[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return fields[i];
            }
        }

        return null;
    }

    private static bool IsNumeric(Type type)
    {
        Type actual = Nullable.GetUnderlyingType(type) ?? type;

        return actual == typeof(double)
            || actual == typeof(float)
            || actual == typeof(decimal)
            || actual == typeof(int)
            || actual == typeof(long)
            || actual == typeof(short)
            || actual == typeof(byte)
            || actual == typeof(sbyte)
            || actual == typeof(uint)
            || actual == typeof(ulong)
            || actual == typeof(ushort);
    }

    private static string BuildFailureMessage(Type entityType, MonitorOptions options, MemberInfo? latitude, MemberInfo? longitude)
    {
        var message = new StringBuilder();
        message.Append("Argus cannot read a position from ").Append(entityType.FullName).Append('.');
        message.AppendLine();

        message.Append("  Latitude: ");
        message.AppendLine(latitude == null
            ? "not found. Tried " + Join(options.LatitudeCandidates) + "."
            : "found on " + latitude.Name + ".");

        message.Append("  Longitude: ");
        message.AppendLine(longitude == null
            ? "not found. Tried " + Join(options.LongitudeCandidates) + "."
            : "found on " + longitude.Name + ".");

        message.AppendLine();
        message.AppendLine("There are three ways to fix this, in order of preference:");
        message.AppendLine("  1. Implement Argus.Contracts.IArgusEntity on " + entityType.Name
            + " (EntityId, Latitude, Longitude, Altitude). Fastest, and checked by the compiler.");
        message.AppendLine("  2. Register an accessor: options.Accessors.Register<" + entityType.Name
            + ">(e => e.Id, e => e.Lat, e => e.Lon, e => e.Alt). Use this when the model types cannot"
            + " reference Argus, or when the runtime has no JIT.");
        message.AppendLine("  3. Add the property names to options.LatitudeCandidates and"
            + " options.LongitudeCandidates. Use this when the names are simply different.");
        message.AppendLine();
        message.Append("Argus will not guess a position, and will not substitute zero:"
            + " a fabricated position at the origin would enter group centroids and trip jump detection"
            + " on the following tick, producing confident findings about an entity that was never measured.");

        return message.ToString();
    }

    private static string Join(IEnumerable<string> values)
    {
        var builder = new StringBuilder();
        foreach (string value in values)
        {
            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            builder.Append(value);
        }

        return builder.Length == 0 ? "(no candidates configured)" : builder.ToString();
    }
}
