using System.Linq.Dynamic.Core;

namespace DevHabit.Api.Services.Sorting;

internal static class QueryableExtensions
{
    public static IQueryable<T> ApplySort<T>(
        this IQueryable<T> query, 
        string? sort, 
        SortMapping[] mappings, 
        string defaultSort = "Id")
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return query.OrderBy(defaultSort);
        }

        string[] sortFields = sort.Split(',')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();

        var orderByParts = new List<string>();
        foreach (string field in sortFields)
        {
            (string sortField, bool isDescending) = ParseSortField(field);

            SortMapping mapping = mappings.First(m => 
                m.SortField.Equals(sortField, StringComparison.OrdinalIgnoreCase));

            string direction = (isDescending, mapping.Reverse) switch
            {
                (false, false) => "Asc",
                (true, false) => "Desc",
                (false, true) => "Desc",
                (true, true) => "Asc",
            };

            orderByParts.Add($"{mapping.PropertyName} {direction}");
        }

        string order = string.Join(", ", orderByParts);

        return query.OrderBy(order);
    }

    private static (string SortField, bool IsDescending) ParseSortField(string field)
    {
        string[] parts = field.Split(' ');
        string sortField = parts[0];
        bool isDescending = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

        return (sortField, isDescending);
    }
}
