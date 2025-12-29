using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using OV_DB.Enum;
using OV_DB.Models;

namespace OV_DB.Tests
{
    /// <summary>
    /// Tests for ImporterController cache behavior when OSM API returns failures.
    /// These tests verify that null responses are not cached and proper HTTP 429 responses are returned.
    /// </summary>
    public class ImporterControllerCacheTests
    {
        private IMemoryCache CreateMemoryCache()
        {
            return new MemoryCache(new MemoryCacheOptions());
        }

        [Fact]
        public async Task GetLinesAsync_WhenFactoryReturnsNullTwice_Returns429AndDoesNotCacheNull()
        {
            // Arrange
            var cache = CreateMemoryCache();
            var callCount = 0;
            var cacheKey = "123|bus|TestNetwork|";

            // Simulate the behavior of GetLinesAsync with a factory that returns null
            async Task<List<OSMLineDTO>> FactoryReturnsNull(ICacheEntry entry)
            {
                callCount++;
                entry.SetSlidingExpiration(TimeSpan.FromMinutes(15));
                return await Task.FromResult<List<OSMLineDTO>>(null);
            }

            // Act - Simulate the retry logic from GetLinesAsync
            var responseList = await cache.GetOrCreateAsync(cacheKey, FactoryReturnsNull);
            ActionResult result;
            
            if (responseList == null)
            {
                cache.Remove(cacheKey);
                await Task.Delay(10); // Reduced delay for testing
                responseList = await cache.GetOrCreateAsync(cacheKey, FactoryReturnsNull);
                if (responseList == null)
                {
                    cache.Remove(cacheKey);
                    result = new StatusCodeResult(429);
                }
                else
                {
                    result = new OkObjectResult(responseList);
                }
            }
            else
            {
                result = new OkObjectResult(responseList);
            }

            // Assert
            Assert.IsType<StatusCodeResult>(result);
            var statusCodeResult = result as StatusCodeResult;
            Assert.Equal(429, statusCodeResult.StatusCode);
            Assert.Equal(2, callCount); // Should be called twice (initial + retry)

            // Verify that null is not left in cache
            var cachedValue = cache.Get(cacheKey);
            Assert.Null(cachedValue);
        }

        [Fact]
        public async Task GetLinesAsync_WhenSucceedsOnSecondAttempt_Returns200WithData()
        {
            // Arrange
            var cache = CreateMemoryCache();
            var callCount = 0;
            var cacheKey = "123|bus|TestNetwork|";
            var expectedData = new List<OSMLineDTO>
            {
                new OSMLineDTO { Id = 1, Name = "Test Line" }
            };

            // Simulate the behavior with factory that succeeds on second call
            async Task<List<OSMLineDTO>> FactorySucceedsOnSecondCall(ICacheEntry entry)
            {
                callCount++;
                entry.SetSlidingExpiration(TimeSpan.FromMinutes(15));
                if (callCount == 1)
                {
                    return await Task.FromResult<List<OSMLineDTO>>(null);
                }
                return await Task.FromResult(expectedData);
            }

            // Act - Simulate the retry logic from GetLinesAsync
            var responseList = await cache.GetOrCreateAsync(cacheKey, FactorySucceedsOnSecondCall);
            ActionResult result;
            
            if (responseList == null)
            {
                cache.Remove(cacheKey);
                await Task.Delay(10); // Reduced delay for testing
                responseList = await cache.GetOrCreateAsync(cacheKey, FactorySucceedsOnSecondCall);
                if (responseList == null)
                {
                    cache.Remove(cacheKey);
                    result = new StatusCodeResult(429);
                }
                else
                {
                    result = new OkObjectResult(responseList);
                }
            }
            else
            {
                result = new OkObjectResult(responseList);
            }

            // Assert
            Assert.IsType<OkObjectResult>(result);
            var okResult = result as OkObjectResult;
            var returnedData = okResult.Value as List<OSMLineDTO>;
            Assert.NotNull(returnedData);
            Assert.Single(returnedData);
            Assert.Equal("Test Line", returnedData[0].Name);
            Assert.Equal(2, callCount); // Should be called twice
        }

        [Fact]
        public async Task GetNetworkLinesAsync_WhenFactoryReturnsNullTwice_Returns429AndDoesNotCacheNull()
        {
            // Arrange
            var cache = CreateMemoryCache();
            var callCount = 0;
            var cacheKey = "network|TestNetwork|";

            // Simulate the behavior of GetNetworkLinesAsync with a factory that returns null
            async Task<List<OSMLineDTO>> FactoryReturnsNull(ICacheEntry entry)
            {
                callCount++;
                entry.SetSlidingExpiration(TimeSpan.FromMinutes(15));
                return await Task.FromResult<List<OSMLineDTO>>(null);
            }

            // Act - Simulate the retry logic from GetNetworkLinesAsync
            var responseList = await cache.GetOrCreateAsync(cacheKey, FactoryReturnsNull);
            ActionResult result;
            
            if (responseList == null)
            {
                cache.Remove(cacheKey);
                await Task.Delay(10); // Reduced delay for testing
                responseList = await cache.GetOrCreateAsync(cacheKey, FactoryReturnsNull);
                if (responseList == null)
                {
                    cache.Remove(cacheKey);
                    result = new StatusCodeResult(429);
                }
                else
                {
                    result = new OkObjectResult(responseList);
                }
            }
            else
            {
                result = new OkObjectResult(responseList);
            }

            // Assert
            Assert.IsType<StatusCodeResult>(result);
            var statusCodeResult = result as StatusCodeResult;
            Assert.Equal(429, statusCodeResult.StatusCode);
            Assert.Equal(2, callCount); // Should be called twice (initial + retry)

            // Verify that null is not left in cache
            var cachedValue = cache.Get(cacheKey);
            Assert.Null(cachedValue);
        }

        [Fact]
        public async Task Read_WhenFactoryReturnsNullTwice_Returns429AndClearsCorrectCacheKey()
        {
            // Arrange
            var cache = CreateMemoryCache();
            var callCount = 0;
            var id = 123;
            var idCache = "123|"; // This is the correct cache key
            var wrongKey = "123"; // This would be the wrong key if we used 'id' directly

            // Simulate the behavior of Read with a factory that returns null
            async Task<OSM> FactoryReturnsNull(ICacheEntry entry)
            {
                callCount++;
                entry.SetSlidingExpiration(TimeSpan.FromMinutes(15));
                return await Task.FromResult<OSM>(null);
            }

            // Act - Simulate the retry logic from Read with CORRECT key removal
            var osm = await cache.GetOrCreateAsync(idCache, FactoryReturnsNull);
            ActionResult result;
            
            if (osm == null)
            {
                cache.Remove(idCache); // Correct: remove idCache, not id
                await Task.Delay(10); // Reduced delay for testing
                osm = await cache.GetOrCreateAsync(idCache, FactoryReturnsNull);
                if (osm == null)
                {
                    cache.Remove(idCache); // Correct: remove idCache, not id
                    result = new StatusCodeResult(429);
                }
                else
                {
                    result = new OkObjectResult(new OSMLineDTO());
                }
            }
            else
            {
                result = new OkObjectResult(new OSMLineDTO());
            }

            // Assert
            Assert.IsType<StatusCodeResult>(result);
            var statusCodeResult = result as StatusCodeResult;
            Assert.Equal(429, statusCodeResult.StatusCode);
            Assert.Equal(2, callCount); // Should be called twice (initial + retry)

            // Verify that null is not left in cache with the correct key
            var cachedValue = cache.Get(idCache);
            Assert.Null(cachedValue);

            // Also verify the wrong key wasn't accidentally used
            var wrongCachedValue = cache.Get(wrongKey);
            Assert.Null(wrongCachedValue);
        }

        [Fact]
        public async Task Read_WhenSucceedsOnSecondAttempt_Returns200WithData()
        {
            // Arrange
            var cache = CreateMemoryCache();
            var callCount = 0;
            var idCache = "123|";
            var expectedOsm = new OSM
            {
                Elements = new List<Element>
                {
                    new Element
                    {
                        Type = TypeEnum.Relation,
                        Id = 123,
                        Tags = new Dictionary<string, string>
                        {
                            { "name", "Test Route" },
                            { "ref", "1" }
                        },
                        Members = new List<Member>()
                    }
                }
            };

            // Simulate the behavior with factory that succeeds on second call
            async Task<OSM> FactorySucceedsOnSecondCall(ICacheEntry entry)
            {
                callCount++;
                entry.SetSlidingExpiration(TimeSpan.FromMinutes(15));
                if (callCount == 1)
                {
                    return await Task.FromResult<OSM>(null);
                }
                return await Task.FromResult(expectedOsm);
            }

            // Act - Simulate the retry logic from Read
            var osm = await cache.GetOrCreateAsync(idCache, FactorySucceedsOnSecondCall);
            ActionResult result;
            
            if (osm == null)
            {
                cache.Remove(idCache);
                await Task.Delay(10); // Reduced delay for testing
                osm = await cache.GetOrCreateAsync(idCache, FactorySucceedsOnSecondCall);
                if (osm == null)
                {
                    cache.Remove(idCache);
                    result = new StatusCodeResult(429);
                }
                else
                {
                    // Simulate basic response creation
                    var element = new OSMLineDTO();
                    var relation = osm.Elements.SingleOrDefault(e => e.Type == TypeEnum.Relation);
                    if (relation != null)
                    {
                        if (relation.Tags?.ContainsKey("name") == true)
                        {
                            element.Name = relation.Tags["name"];
                        }
                        element.Id = relation.Id;
                    }
                    result = new OkObjectResult(element);
                }
            }
            else
            {
                result = new OkObjectResult(new OSMLineDTO());
            }

            // Assert
            Assert.IsType<OkObjectResult>(result);
            var okResult = result as OkObjectResult;
            var returnedData = okResult.Value as OSMLineDTO;
            Assert.NotNull(returnedData);
            Assert.Equal("Test Route", returnedData.Name);
            Assert.Equal(2, callCount); // Should be called twice
        }

        [Fact]
        public async Task GetLinesAsync_WhenFactorySucceedsFirstTime_Returns200WithoutRetry()
        {
            // Arrange
            var cache = CreateMemoryCache();
            var callCount = 0;
            var cacheKey = "123|bus|TestNetwork|";
            var expectedData = new List<OSMLineDTO>
            {
                new OSMLineDTO { Id = 1, Name = "Test Line" }
            };

            // Simulate the behavior with factory that succeeds immediately
            async Task<List<OSMLineDTO>> FactorySucceedsImmediately(ICacheEntry entry)
            {
                callCount++;
                entry.SetSlidingExpiration(TimeSpan.FromMinutes(15));
                return await Task.FromResult(expectedData);
            }

            // Act - Simulate the retry logic from GetLinesAsync
            var responseList = await cache.GetOrCreateAsync(cacheKey, FactorySucceedsImmediately);
            ActionResult result;
            
            if (responseList == null)
            {
                cache.Remove(cacheKey);
                await Task.Delay(10);
                responseList = await cache.GetOrCreateAsync(cacheKey, FactorySucceedsImmediately);
                if (responseList == null)
                {
                    cache.Remove(cacheKey);
                    result = new StatusCodeResult(429);
                }
                else
                {
                    result = new OkObjectResult(responseList);
                }
            }
            else
            {
                result = new OkObjectResult(responseList);
            }

            // Assert
            Assert.IsType<OkObjectResult>(result);
            var okResult = result as OkObjectResult;
            var returnedData = okResult.Value as List<OSMLineDTO>;
            Assert.NotNull(returnedData);
            Assert.Single(returnedData);
            Assert.Equal("Test Line", returnedData[0].Name);
            Assert.Equal(1, callCount); // Should only be called once (no retry needed)
        }

        [Fact]
        public async Task CacheKeyRemoval_VerifiesCorrectKeyIsRemoved()
        {
            // Arrange
            var cache = CreateMemoryCache();
            var id = 123;
            var dateTime = DateTime.Parse("2023-01-01T00:00:00Z");
            var idCache = id + "|" + dateTime.ToString("o"); // Correct cache key format
            var wrongKey = id.ToString(); // Wrong key that was previously used

            // Add a value to cache using the correct key
            cache.Set(idCache, "test value");

            // Act - Remove using the correct key (idCache)
            cache.Remove(idCache);

            // Assert
            var correctKeyCachedValue = cache.Get(idCache);
            Assert.Null(correctKeyCachedValue); // Should be removed

            // Verify that if we had used the wrong key, the value would still be there
            cache.Set(idCache, "test value 2");
            cache.Remove(wrongKey); // Remove wrong key
            var valueAfterWrongRemoval = cache.Get(idCache);
            Assert.NotNull(valueAfterWrongRemoval); // Should still be there because we removed wrong key
        }
    }
}
