
using System.Collections.Concurrent;

namespace DevHabit.Api.Services;

public sealed class InMemoryETagStore
{
    private static readonly ConcurrentDictionary<string, string> ETags = new();

    public string GetETag(string resourceUri)
    {
        return ETags.GetOrAdd(resourceUri, _ => string.Empty);
    }

    public void SetETag(string resourceUri, string eTag)
    {
        ETags.AddOrUpdate(resourceUri, eTag, (_, _) => eTag);
    }

    public void RemoveETag(string resourceUri)
    {
        ETags.TryRemove(resourceUri, out _);
    }
}
