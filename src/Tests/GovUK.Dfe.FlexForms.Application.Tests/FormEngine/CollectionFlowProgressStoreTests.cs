using GovUK.Dfe.FlexForms.Application.FormEngine;
using GovUK.Dfe.FlexForms.Domain.Caching;

namespace GovUK.Dfe.FlexForms.Application.Tests.FormEngine;

public class CollectionFlowProgressStoreTests
{
    private readonly InMemoryFormSessionStore _session = new();
    private readonly CollectionFlowProgressStore _store;

    public CollectionFlowProgressStoreTests()
    {
        _store = new CollectionFlowProgressStore(_session);
    }

    [Fact]
    public void Load_returns_empty_when_session_has_no_progress()
    {
        var result = _store.Load("flow", "instance");
        Assert.Empty(result);
    }

    [Fact]
    public void Save_merges_into_existing_progress()
    {
        _store.Save("flow", "instance", new Dictionary<string, object> { ["a"] = "1" });
        _store.Save("flow", "instance", new Dictionary<string, object> { ["b"] = "2" });

        var result = _store.Load("flow", "instance");

        Assert.Equal("1", result["a"]?.ToString());
        Assert.Equal("2", result["b"]?.ToString());
        Assert.False(string.IsNullOrEmpty(_session.GetString(FormSessionKeys.FlowProgress("flow", "instance"))));
    }

    [Fact]
    public void SetField_updates_a_single_key()
    {
        _store.Save("flow", "instance", new Dictionary<string, object> { ["a"] = "1" });
        _store.SetField("flow", "instance", "a", "updated");

        var result = _store.Load("flow", "instance");
        Assert.Equal("updated", result["a"]?.ToString());
    }

    [Fact]
    public void Clear_removes_progress()
    {
        _store.Save("flow", "instance", new Dictionary<string, object> { ["a"] = "1" });
        _store.Clear("flow", "instance");

        Assert.Empty(_store.Load("flow", "instance"));
        Assert.Null(_session.GetString(FormSessionKeys.FlowProgress("flow", "instance")));
    }
}
