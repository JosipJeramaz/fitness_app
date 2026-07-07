using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FitnessChallenge.Infrastructure.Data.Converters;

/// <summary>
/// Stores <see cref="DateTimeOffset"/> as UTC ticks (a <see cref="long"/>). SQLite has no native
/// DateTimeOffset type and cannot translate comparisons on its TEXT form; an integer column makes
/// <c>&lt;=</c>/ordering translate cleanly and index efficiently. All values are UTC by construction.
/// </summary>
public sealed class DateTimeOffsetToTicksConverter : ValueConverter<DateTimeOffset, long>
{
    public DateTimeOffsetToTicksConverter()
        : base(
            value => value.UtcTicks,
            ticks => new DateTimeOffset(ticks, TimeSpan.Zero))
    {
    }
}
