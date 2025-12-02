using OnlyT.Avalonia.WebServer;

namespace OnlyT.Avalonia.Tests.WebServer;

[TestClass]
public class ApiThrottlerTests
{
    [TestMethod]
    public void IsRequestAllowed_FirstRequest_ReturnsTrue()
    {
        // Arrange
        using var throttler = new ApiThrottler(maxRequestsPerWindow: 10, windowSeconds: 60);
        var ip = "192.168.1.1";

        // Act
        var result = throttler.IsRequestAllowed(ip);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsRequestAllowed_UnderLimit_ReturnsTrue()
    {
        // Arrange
        using var throttler = new ApiThrottler(maxRequestsPerWindow: 10, windowSeconds: 60);
        var ip = "192.168.1.1";

        // Act - Make 9 requests
        for (int i = 0; i < 9; i++)
        {
            Assert.IsTrue(throttler.IsRequestAllowed(ip));
        }

        // Assert - 10th request should still be allowed
        Assert.IsTrue(throttler.IsRequestAllowed(ip));
    }

    [TestMethod]
    public void IsRequestAllowed_AtLimit_ReturnsFalse()
    {
        // Arrange
        using var throttler = new ApiThrottler(maxRequestsPerWindow: 10, windowSeconds: 60);
        var ip = "192.168.1.1";

        // Act - Make 10 requests (hit the limit)
        for (int i = 0; i < 10; i++)
        {
            throttler.IsRequestAllowed(ip);
        }

        // Assert - 11th request should be blocked
        Assert.IsFalse(throttler.IsRequestAllowed(ip));
    }

    [TestMethod]
    public void IsRequestAllowed_DifferentIps_IndependentLimits()
    {
        // Arrange
        using var throttler = new ApiThrottler(maxRequestsPerWindow: 5, windowSeconds: 60);
        var ip1 = "192.168.1.1";
        var ip2 = "192.168.1.2";

        // Act - Exhaust limit for ip1
        for (int i = 0; i < 5; i++)
        {
            throttler.IsRequestAllowed(ip1);
        }

        // Assert - ip2 should still be allowed
        Assert.IsTrue(throttler.IsRequestAllowed(ip2));
        Assert.IsFalse(throttler.IsRequestAllowed(ip1));
    }

    [TestMethod]
    public void IsRequestAllowed_EmptyIp_ReturnsTrue()
    {
        // Arrange
        using var throttler = new ApiThrottler(maxRequestsPerWindow: 10, windowSeconds: 60);

        // Act
        var result = throttler.IsRequestAllowed("");

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsRequestAllowed_NullIp_ReturnsTrue()
    {
        // Arrange
        using var throttler = new ApiThrottler(maxRequestsPerWindow: 10, windowSeconds: 60);

        // Act
        var result = throttler.IsRequestAllowed(null!);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void GetRemainingRequests_NoRequests_ReturnsMax()
    {
        // Arrange
        using var throttler = new ApiThrottler(maxRequestsPerWindow: 100, windowSeconds: 60);
        var ip = "192.168.1.1";

        // Act
        var remaining = throttler.GetRemainingRequests(ip);

        // Assert
        Assert.AreEqual(100, remaining);
    }

    [TestMethod]
    public void GetRemainingRequests_AfterRequests_ReturnsCorrectCount()
    {
        // Arrange
        using var throttler = new ApiThrottler(maxRequestsPerWindow: 100, windowSeconds: 60);
        var ip = "192.168.1.1";

        // Act - Make 30 requests
        for (int i = 0; i < 30; i++)
        {
            throttler.IsRequestAllowed(ip);
        }
        var remaining = throttler.GetRemainingRequests(ip);

        // Assert
        Assert.AreEqual(70, remaining);
    }

    [TestMethod]
    public void GetRemainingRequests_EmptyIp_ReturnsMax()
    {
        // Arrange
        using var throttler = new ApiThrottler(maxRequestsPerWindow: 100, windowSeconds: 60);

        // Act
        var remaining = throttler.GetRemainingRequests("");

        // Assert
        Assert.AreEqual(100, remaining);
    }

    [TestMethod]
    public void GetRemainingRequests_AtLimit_ReturnsZero()
    {
        // Arrange
        using var throttler = new ApiThrottler(maxRequestsPerWindow: 10, windowSeconds: 60);
        var ip = "192.168.1.1";

        // Act - Exhaust limit
        for (int i = 0; i < 10; i++)
        {
            throttler.IsRequestAllowed(ip);
        }
        var remaining = throttler.GetRemainingRequests(ip);

        // Assert
        Assert.AreEqual(0, remaining);
    }

    [TestMethod]
    public void Reset_ClearsAllTracking()
    {
        // Arrange
        using var throttler = new ApiThrottler(maxRequestsPerWindow: 10, windowSeconds: 60);
        var ip = "192.168.1.1";

        // Exhaust limit
        for (int i = 0; i < 10; i++)
        {
            throttler.IsRequestAllowed(ip);
        }
        Assert.IsFalse(throttler.IsRequestAllowed(ip)); // Verify blocked

        // Act
        throttler.Reset();

        // Assert
        Assert.IsTrue(throttler.IsRequestAllowed(ip)); // Should be allowed again
        Assert.AreEqual(9, throttler.GetRemainingRequests(ip)); // Made 1 request above, so 10-1=9
    }

    [TestMethod]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var throttler = new ApiThrottler();

        // Act & Assert
        throttler.Dispose();
        throttler.Dispose(); // Double dispose should also not throw
    }

    [TestMethod]
    public void Constructor_CustomValues_AppliesCorrectly()
    {
        // Arrange & Act
        using var throttler = new ApiThrottler(maxRequestsPerWindow: 5, windowSeconds: 30);
        var ip = "192.168.1.1";

        // Assert
        Assert.AreEqual(5, throttler.GetRemainingRequests(ip));

        // Exhaust the 5 request limit
        for (int i = 0; i < 5; i++)
        {
            throttler.IsRequestAllowed(ip);
        }
        Assert.IsFalse(throttler.IsRequestAllowed(ip));
    }

    [TestMethod]
    public void IsRequestAllowed_MultipleIpsWithDifferentLoad_TracksCorrectly()
    {
        // Arrange
        using var throttler = new ApiThrottler(maxRequestsPerWindow: 10, windowSeconds: 60);
        var ip1 = "10.0.0.1";
        var ip2 = "10.0.0.2";
        var ip3 = "10.0.0.3";

        // Act - Different request counts for each IP
        for (int i = 0; i < 3; i++) throttler.IsRequestAllowed(ip1);
        for (int i = 0; i < 7; i++) throttler.IsRequestAllowed(ip2);
        for (int i = 0; i < 10; i++) throttler.IsRequestAllowed(ip3);

        // Assert
        Assert.AreEqual(7, throttler.GetRemainingRequests(ip1));
        Assert.AreEqual(3, throttler.GetRemainingRequests(ip2));
        Assert.AreEqual(0, throttler.GetRemainingRequests(ip3));

        Assert.IsTrue(throttler.IsRequestAllowed(ip1));
        Assert.IsTrue(throttler.IsRequestAllowed(ip2));
        Assert.IsFalse(throttler.IsRequestAllowed(ip3));
    }
}
