using GovUK.Dfe.FlexForms.Infrastructure.Stores;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Stores;

public class HttpFormSessionStoreTests
{
    [Fact]
    public void GetString_SetString_Remove_round_trip_http_session()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Session = new MemorySession();
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        var store = new HttpFormSessionStore(accessor);

        store.SetString("TemplateId", "abc");
        Assert.Equal("abc", store.GetString("TemplateId"));
        Assert.Contains("TemplateId", store.Keys);

        store.Remove("TemplateId");
        Assert.Null(store.GetString("TemplateId"));
        Assert.DoesNotContain("TemplateId", store.Keys);
    }

    [Fact]
    public void GetString_throws_when_http_context_is_missing()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);
        var store = new HttpFormSessionStore(accessor);

        Assert.Throws<InvalidOperationException>(() => store.GetString("any"));
    }

    private sealed class MemorySession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new(StringComparer.Ordinal);

        public bool IsAvailable => true;
        public string Id => "test";
        public IEnumerable<string> Keys => _store.Keys;
        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }
}
