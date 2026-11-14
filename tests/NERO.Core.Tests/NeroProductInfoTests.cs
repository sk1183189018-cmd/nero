using NERO.Core;
using Xunit;

namespace NERO.Core.Tests;

public sealed class NeroProductInfoTests
{
    [Fact]
    public void ProductName_IsNero() => Assert.Equal("NERO", NeroProductInfo.ProductName);

    [Fact]
    public void Platform_IsWindows() => Assert.Equal("Windows", NeroProductInfo.Platform);
}
