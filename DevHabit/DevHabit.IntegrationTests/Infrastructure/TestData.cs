using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevHabit.Api.DTOs.Entries;
using DevHabit.Api.DTOs.Habits;
using DevHabit.Api.Entities;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DevHabit.IntegrationTests.Infrastructure;

public static class TestData
{
    public static class Habit
    {
        public static CreateHabitDto CreateReadingHabit() => new()
        {
            Name = "Read Books",
            Type = HabitType.Measurable,
            Frequency = new FrequencyDto
            {
                Type = FrequencyType.Daily,
                TimesPerPeriod = 1
            },
            Target = new TargetDto
            {
                Value = 30,
                Unit = "pages"
            }
        };

        public static CreateHabitDto CreateExerciseHabit() => new()
        {
            Name = "Exercise",
            Type = HabitType.Measurable,
            Frequency = new FrequencyDto
            {
                Type = FrequencyType.Daily,
                TimesPerPeriod = 1
            },
            Target = new TargetDto
            {
                Value = 30,
                Unit = "minutes"
            }
        };
    }

    public static class Entry
    {
        public static CreateEntryDto CreateEntry(string habitId, int value = 10, string? note = null) => new()
        {
            HabitId = habitId,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Value = value,
            Note = note
        };
    }
}
