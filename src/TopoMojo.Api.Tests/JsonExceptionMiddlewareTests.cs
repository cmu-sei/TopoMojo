using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using TopoMojo.Api.Exceptions;
using TopoMojo.Hypervisor.Exceptions;
using Xunit;

namespace TopoMojo.Api.Tests;

public sealed class JsonExceptionMiddlewareTests
{
    [Fact]
    public async Task HypervisorDetail_GoesToTheErrorLogButNotTheResponse()
    {
        var middleware = new JsonExceptionMiddleware(
            _ => throw new HypervisorException("Ask an administrator.", "Disk [ds] a/b.vmdk is hosted."),
            NullLogger<JsonExceptionMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        using var cache = new MemoryCache(new MemoryCacheOptions());

        await middleware.Invoke(context, cache);

        context.Response.Body.Position = 0;
        string body = await new StreamReader(context.Response.Body).ReadToEndAsync();

        Assert.Equal(400, context.Response.StatusCode);
        Assert.Contains("Ask an administrator.", body);
        Assert.DoesNotContain("[ds] a/b.vmdk", body);

        var logged = cache.Get<List<TimestampedException>>(AppConstants.ErrorListCacheKey);
        Assert.Contains("Ask an administrator. Disk [ds] a/b.vmdk is hosted.", logged[0].Message);
    }
}
