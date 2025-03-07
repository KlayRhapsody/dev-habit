using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DevHabit.Api.Services.Sorting;

// Reverse: age 30 (desc) -> birthdate 1995 (asc)
public sealed record SortMapping(string SortField, string PropertyName, bool Reverse = false);
