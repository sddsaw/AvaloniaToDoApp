using System;
using Xunit;
using AvaloniaTodoApp.Services;

namespace AvaloniaTodoApp.Tests;

public class VersionComparisonTests
{
    [Theory]
    [InlineData("1.10.0", "1.9.0", true)]   // 字典序倒挂陷阱，数值比对必须识别为新版
    [InlineData("1.9.0", "1.10.0", false)]
    [InlineData("2.0.0", "1.99.99", true)]
    [InlineData("v1.2.0", "1.1.0", true)]   // 带 v 前缀兼容
    [InlineData("1.1.0", "v1.2.0", false)]
    [InlineData("1.0.0", "1.0.0", false)]   // 相同版本
    [InlineData("1.0.1", "1.0.0", true)]
    [InlineData("1.0.0.1", "1.0.0", true)]  // 四段版本号
    public void IsNewerVersion_ShouldCorrectlyCompareVersions(string latest, string current, bool expected)
    {
        var result = UpdateService.IsNewerVersion(latest, current);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("", "1.0.0", false)]
    [InlineData(" ", "1.0.0", false)]
    [InlineData(null, "1.0.0", false)]
    public void IsNewerVersion_InvalidLatest_ShouldReturnFalse(string? latest, string current, bool expected)
    {
        var result = UpdateService.IsNewerVersion(latest!, current);
        Assert.Equal(expected, result);
    }
}
