﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace LongevityWorldCup.Website.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BitcoinController(IHttpClientFactory httpClientFactory, IMemoryCache cache) : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly IMemoryCache _cache = cache;

        [HttpGet("btcusd")]
        public async Task<IActionResult> GetBtcUsd()
        {
            const string cacheKey = "btcToUsdRate";
            if (_cache.TryGetValue(cacheKey, out decimal cachedUsdRate))
            {
                return Ok(new { btcToUsdRate = cachedUsdRate });
            }

            var client = _httpClientFactory.CreateClient();
            decimal usdRate;

            try
            {
                // Primary API
                var response = await client.GetAsync("https://api.coingecko.com/api/v3/simple/price?ids=bitcoin&vs_currencies=usd");
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonString);
                    var btcElement = doc.RootElement.GetProperty("bitcoin");
                    usdRate = btcElement.GetProperty("usd").GetDecimal();

                    // Cache the result for 1 minute
                    _cache.Set(cacheKey, usdRate, TimeSpan.FromMinutes(1));

                    return Ok(new { btcToUsdRate = usdRate });
                }
            }
            catch
            {
                // Fallback API
                try
                {
                    var fallbackResponse = await client.GetAsync("https://blockchain.info/ticker");
                    if (fallbackResponse.IsSuccessStatusCode)
                    {
                        var jsonString = await fallbackResponse.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(jsonString);
                        usdRate = doc.RootElement.GetProperty("USD").GetProperty("last").GetDecimal();

                        // Cache the result for 1 minute
                        _cache.Set(cacheKey, usdRate, TimeSpan.FromMinutes(1));

                        return Ok(new { btcToUsdRate = usdRate });
                    }
                }
                catch
                {
                    return StatusCode(500, "Both primary and fallback APIs failed for BTC to USD rate.");
                }
            }

            return StatusCode(500, "Error fetching BTC to USD rate.");
        }

        [HttpGet("total-received")]
        public async Task<IActionResult> GetTotalReceived(string address)
        {
            var cacheKey = $"totalReceived_{address}";
            if (_cache.TryGetValue(cacheKey, out long cachedTotalReceived))
            {
                return Ok(new { totalReceivedSatoshis = cachedTotalReceived });
            }

            var client = _httpClientFactory.CreateClient();
            long totalReceivedSatoshis;

            try
            {
                // Primary API
                var response = await client.GetAsync($"https://api.blockcypher.com/v1/btc/main/addrs/{address}");
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonString);
                    totalReceivedSatoshis = doc.RootElement.GetProperty("total_received").GetInt64();

                    // Cache the result for 1 minute
                    _cache.Set(cacheKey, totalReceivedSatoshis, TimeSpan.FromMinutes(1));

                    return Ok(new { totalReceivedSatoshis });
                }
            }
            catch
            {
                // Fallback API
                try
                {
                    var fallbackResponse = await client.GetAsync($"https://blockchain.info/rawaddr/{address}");
                    if (fallbackResponse.IsSuccessStatusCode)
                    {
                        var jsonString = await fallbackResponse.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(jsonString);
                        totalReceivedSatoshis = doc.RootElement.GetProperty("total_received").GetInt64();

                        // Cache the result for 1 minute
                        _cache.Set(cacheKey, totalReceivedSatoshis, TimeSpan.FromMinutes(1));

                        return Ok(new { totalReceivedSatoshis });
                    }
                }
                catch
                {
                    return StatusCode(500, "Both primary and fallback APIs failed for total received.");
                }
            }

            return StatusCode(500, "Error fetching total received for address.");
        }
    }
}