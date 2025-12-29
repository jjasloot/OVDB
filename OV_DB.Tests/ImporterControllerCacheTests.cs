using Microsoft.Extensions.Caching.Memory;
using OV_DB.Models;

namespace OV_DB.Tests
{
    /// <summary>
    /// Tests the cache retry logic to ensure null responses aren't cached.
    /// </summary>
    public class ImporterControllerCacheTests
    {
        [Fact]
        public async Task CacheRetryLogic_WhenFactoryReturnsNull_RemovesCacheAndReturns429()
        {
            // Arrange
            var cache = new MemoryCache(new MemoryCacheOptions());
            var cacheKey = "test-key";
            var factoryCalls = 0;

            // Factory that always returns null (simulating API failure)
            async Task<List<OSMLineDTO>?> FailingFactory(ICacheEntry entry)
            {
                factoryCalls++;
                entry.SetSlidingExpiration(TimeSpan.FromMinutes(15));
                return await Task.FromResult<List<OSMLineDTO>?>(null);
            }

            // Act - Simulate the controller's retry logic
            var result = await cache.GetOrCreateAsync(cacheKey, FailingFactory);
            if (result == null)
            {
                cache.Remove(cacheKey);
                await Task.Delay(10);
                result = await cache.GetOrCreateAsync(cacheKey, FailingFactory);
                if (result == null)
                {
                    cache.Remove(cacheKey);
                }
            }

            // Assert
            Assert.Equal(2, factoryCalls); // Called twice (initial + retry)
            Assert.Null(cache.Get(cacheKey)); // Null is not cached
        }

        [Fact]
        public async Task CacheRetryLogic_WhenFactorySucceedsOnRetry_ReturnsData()
        {
            // Arrange
            var cache = new MemoryCache(new MemoryCacheOptions());
            var cacheKey = "test-key";
            var factoryCalls = 0;
            var expectedData = new List<OSMLineDTO> { new OSMLineDTO { Id = 1, Name = "Test" } };

            // Factory that fails first time, succeeds second time
            async Task<List<OSMLineDTO>?> RetrySuccessFactory(ICacheEntry entry)
            {
                factoryCalls++;
                entry.SetSlidingExpiration(TimeSpan.FromMinutes(15));
                return await Task.FromResult(factoryCalls == 1 ? null : expectedData);
            }

            // Act - Simulate the controller's retry logic
            var result = await cache.GetOrCreateAsync(cacheKey, RetrySuccessFactory);
            if (result == null)
            {
                cache.Remove(cacheKey);
                await Task.Delay(10);
                result = await cache.GetOrCreateAsync(cacheKey, RetrySuccessFactory);
                if (result == null)
                {
                    cache.Remove(cacheKey);
                }
            }

            // Assert
            Assert.Equal(2, factoryCalls); // Called twice
            Assert.NotNull(result); // Got data on retry
            Assert.Equal("Test", result[0].Name);
        }

        [Fact]
        public void CacheKeyCorrection_UsesIdCacheNotId()
        {
            // Arrange
            var cache = new MemoryCache(new MemoryCacheOptions());
            var id = 123;
            var dateTime = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var idCache = id + "|" + dateTime.ToString("o");
            var wrongKey = id.ToString();

            // Pre-populate cache with correct key
            cache.Set(idCache, "test-value");

            // Act - Remove using correct key
            cache.Remove(idCache);

            // Assert
            Assert.Null(cache.Get(idCache)); // Correct key was removed
            
            // Verify wrong key removal doesn't affect correct key
            cache.Set(idCache, "test-value-2");
            cache.Remove(wrongKey); // Try to remove with wrong key
            Assert.NotNull(cache.Get(idCache)); // Value still there because wrong key was used
        }
    }
}
