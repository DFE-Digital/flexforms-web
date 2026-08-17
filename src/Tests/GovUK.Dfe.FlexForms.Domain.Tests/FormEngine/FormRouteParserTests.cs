using GovUK.Dfe.FlexForms.Domain.FormEngine;

namespace GovUK.Dfe.FlexForms.Domain.Tests.FormEngine;

public class FormRouteParserTests
{
    [Theory]
    [InlineData("flow/f1/i1/p1", "f1", "i1", "p1")]
    [InlineData("FLOW/f1/i1", "f1", "i1", "")]
    [InlineData("flow/f1/i1/", "f1", "i1", "")]
    public void TryParseCollectionFlow_accepts_valid_routes(
        string pageId,
        string flowId,
        string instanceId,
        string flowPageId)
    {
        Assert.True(FormRouteParser.TryParseCollectionFlow(pageId, out var route));
        Assert.Equal(flowId, route.FlowId);
        Assert.Equal(instanceId, route.InstanceId);
        Assert.Equal(flowPageId, route.PageId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("task-page")]
    [InlineData("flow/only-flow-id")]
    [InlineData("f1/derived/item1")]
    public void TryParseCollectionFlow_rejects_non_collection_routes(string? pageId)
    {
        Assert.False(FormRouteParser.TryParseCollectionFlow(pageId, out var route));
        Assert.Equal(default, route);
    }

    [Theory]
    [InlineData("df1/derived/item1/p1", "df1", "item1", "p1")]
    [InlineData("df1/DERIVED/item1", "df1", "item1", "")]
    public void TryParseDerivedFlow_accepts_valid_routes(
        string pageId,
        string flowId,
        string itemId,
        string derivedPageId)
    {
        Assert.True(FormRouteParser.TryParseDerivedFlow(pageId, out var route));
        Assert.Equal(flowId, route.FlowId);
        Assert.Equal(itemId, route.ItemId);
        Assert.Equal(derivedPageId, route.PageId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("flow/f1/i1/p1")]
    [InlineData("df1/other/item1")]
    [InlineData("derived/item1")]
    public void TryParseDerivedFlow_rejects_non_derived_routes(string? pageId)
    {
        Assert.False(FormRouteParser.TryParseDerivedFlow(pageId, out _));
    }

    [Theory]
    [InlineData("flow/f1/i1/p1", true)]
    [InlineData("flow/f1", true)]
    [InlineData("task-page", false)]
    [InlineData(null, false)]
    public void LooksLikeCollectionFlow_uses_the_flow_prefix(string? pageId, bool expected)
    {
        Assert.Equal(expected, FormRouteParser.LooksLikeCollectionFlow(pageId));
    }

    [Theory]
    [InlineData("APP-1", "task-1", "flow/f1/i1/p1", "APP-1:task-1:flow:f1:i1")]
    [InlineData("APP-1", "task-1", "task-page", "APP-1:task-1")]
    [InlineData("APP-1", "task-1", "", "APP-1:task-1")]
    [InlineData("APP-1", "task-1", "df1/derived/item1", "APP-1:task-1")]
    public void HistoryScope_includes_flow_instance_only_for_collection_routes(
        string reference,
        string taskId,
        string pageId,
        string expected)
    {
        Assert.Equal(expected, FormRouteParser.HistoryScope(reference, taskId, pageId));
    }
}
