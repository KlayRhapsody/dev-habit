namespace DevHabit.Api.DTOs.Common;

public interface ICollectionResponse<T>
{
    List<T> Item { get; init; }
}
