using System.Reflection;

namespace Cleansia.TestUtilities;

public static class TestHelper
{
    public static TEntity Merge<TEntity, TPartial>(this TEntity entity, TPartial? partial)
        where TEntity : class
        where TPartial : class
    {
        if (partial is null)
        {
            return entity;
        }

        var partialProperties = typeof(TPartial).GetProperties();
        foreach (var prop in partialProperties)
        {
            var entityProp = typeof(TEntity).GetProperty(prop.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (entityProp == null || !entityProp.CanWrite)
            {
                continue;
            }

            var propertyType = entityProp.PropertyType;
            var defaultValue = propertyType.IsValueType ? Activator.CreateInstance(propertyType) : null;
            var value = prop.GetValue(partial);
            if (Equals(value, defaultValue))
            {
                continue;
            }

            if (value != null)
            {
                entityProp.SetValue(entity, value);
            }
        }
        return entity;
    }
}