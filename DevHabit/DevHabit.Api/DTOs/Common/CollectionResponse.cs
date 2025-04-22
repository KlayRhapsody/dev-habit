

namespace DevHabit.Api.DTOs.Common;

public class CollectionResponse<T> : ICollectionResponse<T>, ILinksResponse
{
    public List<T> Items { get; init; }
    public List<LinkDto> Links { get; set; }
}
