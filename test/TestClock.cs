using Microsoft.Extensions.Internal;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.Caching.Oracle;

public class TestClock : ISystemClock
{
    // Examples:
    // DateTime.Now:                6/29/2015 1:20:40 PM
    // DateTime.UtcNow:             6/29/2015 8:20:40 PM
    // DateTimeOffset.Now:          6/29/2015 1:20:40 PM - 07:00
    // DateTimeOffset.UtcNow:       6/29/2015 8:20:40 PM + 00:00
    // DateTimeOffset.UtcDateTime:  6/29/2015 8:20:40 PM

    public DateTimeOffset UtcNow { get; private set; } = new(2013, 1, 1, 1, 0, 0, TimeSpan.Zero);

    public TestClock Add(TimeSpan timeSpan)
    {
        UtcNow = UtcNow.Add(timeSpan);

        return this;
    }
}
