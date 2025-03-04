using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DevHabit.Api.DTOs.HabitTags;

public sealed record UpsertHabitTagsDto
{
    public List<string> TagIds { get; init; }
}
