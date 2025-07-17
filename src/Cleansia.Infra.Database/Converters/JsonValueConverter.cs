using System.Collections;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Newtonsoft.Json;

namespace Cleansia.Infra.Database.Converters;

public class JsonValueConverter<T>() : ValueConverter<T, string>(v => JsonConvert.SerializeObject(v), v => JsonConvert.DeserializeObject<T>(v)!);

public class JsonValueComparer<T>() : ValueComparer<T>((l, r) => CompareValues(l, r),
    v => v == null ? 0 : JsonConvert.SerializeObject(v).GetHashCode(),
    v => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(v))!)
{
    private static bool CompareValues(T? left, T? right)
    {
        // If both are null, they are equal
        if (left == null && right == null)
        {
            return true;
        }

        // If only one is null, they are not equal
        if (left == null || right == null)
        {
            return false;
        }

        // Check if either left or right is a collection
        if (left is not IEnumerable || right is not IEnumerable)
        {
            return left.Equals(right);
        }

        var leftJson = JsonConvert.SerializeObject(left);
        var rightJson = JsonConvert.SerializeObject(right);
        return leftJson == rightJson;
    }
}