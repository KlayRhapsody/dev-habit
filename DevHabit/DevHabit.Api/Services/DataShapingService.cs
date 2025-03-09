using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace DevHabit.Api.Services;

public sealed class DataShapingService
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertiesCache = new(); 

    public ExpandoObject ShapeData<T>(T entity, string? fields)
    {
        HashSet<string> fieldSet = fields?
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(f => f.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        PropertyInfo[] propertyInfos = PropertiesCache.GetOrAdd(
            typeof(T), 
            t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));

        if (fieldSet.Any())
        {
            propertyInfos = propertyInfos
                .Where(p => fieldSet.Contains(p.Name))
                .ToArray();
        }

        IDictionary<string, object?> shapedObject = new ExpandoObject();

        foreach (PropertyInfo propertyInfo in propertyInfos)
        {
            shapedObject[propertyInfo.Name] = propertyInfo.GetValue(entity);
        }

        return (ExpandoObject) shapedObject;
    }

    public List<ExpandoObject> ShapeCollectionData<T>(IEnumerable<T> entities, string? fields)
    {
        HashSet<string> fieldSet = fields?
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(f => f.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        PropertyInfo[] propertyInfos = PropertiesCache.GetOrAdd(
            typeof(T), 
            t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));

        if (fieldSet.Any())
        {
            propertyInfos = propertyInfos
                .Where(p => fieldSet.Contains(p.Name))
                .ToArray();
        }

        List<ExpandoObject> shapedObjects = [];

        foreach (T entity in entities)
        {
            IDictionary<string, object?> shapedObject = new ExpandoObject();

            foreach (PropertyInfo propertyInfo in propertyInfos)
            {
                shapedObject[propertyInfo.Name] = propertyInfo.GetValue(entity);
            }

            shapedObjects.Add((ExpandoObject)shapedObject);
        }

        return shapedObjects;
    }

    public bool Validate<T>(string? fields)
    {
        if (string.IsNullOrWhiteSpace(fields))
        {
            return true;
        }

        HashSet<string> fieldSet = fields
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(f => f.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        PropertyInfo[] propertyInfos = PropertiesCache.GetOrAdd(
            typeof(T), 
            t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));

        return fieldSet.All(f => propertyInfos.Any(p => p.Name.Equals(f, StringComparison.OrdinalIgnoreCase)));
    }
}
