using Trailmark.Services;
using Xunit;

namespace Trailmark.Tests;

public sealed class MapTilerOptionsTests
{
    [Fact]
    public void GetStyleUrl_UsesOutdoorStyleAndEscapesKey()
    {
        var options = new MapTilerOptions("key with + symbols");

        var url = options.GetStyleUrl();

        Assert.Contains("/maps/outdoor-v2/style.json", url, StringComparison.Ordinal);
        Assert.Contains("key=key%20with%20%2B%20symbols", url, StringComparison.Ordinal);
    }
}
