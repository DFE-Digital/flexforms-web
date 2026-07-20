using AutoFixture;
using AutoFixture.AutoNSubstitute;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Infrastructure.Services;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Services;

public class DerivedCollectionFlowServiceTests
{
    private readonly IFixture _fixture;
    private readonly DerivedCollectionFlowService _service;

    public DerivedCollectionFlowServiceTests()
    {
        _fixture = new Fixture().Customize(new AutoNSubstituteCustomization { ConfigureMembers = true });
        
        _fixture.Customize<Condition>(ob => ob.Without(rule => rule.Conditions));
        
        _service = _fixture.Create<DerivedCollectionFlowService>();
    }
    
    [Fact]
    public void GenerateItemsFromSourceField_for_collection_correctly_decodes_sourceJson()
    {
        var fieldId = _fixture.Create<string>();
        var sourceJson = "[{\"foo\":{\"id\":\"123456\",\"name\":\"some foo\"},\"bar\":123}]";
        var formData = new Dictionary<string, object>
        {
            [fieldId] = sourceJson,
        };
        var config = _fixture.Build<DerivedCollectionFlowConfiguration>()
            .With(c => c.SourceType, "collection")
            .Create();

        var result = _service.GenerateItemsFromSourceField(fieldId, formData, config);
        
        Assert.NotEmpty(result);
        var item = Assert.Single(result);
        Assert.Equal("123456", item.Id);
        Assert.Equal("some foo", item.DisplayName);
        Assert.Equal("Not signed yet", item.Status);
    }
    
    [Fact]
    public void GenerateItemsFromSourceField_for_collection_correctly_html_escapes_sourceJson()
    {
        var fieldId = _fixture.Create<string>();
        var sourceJson = "[{\"foo\":\"{&quot;id&quot;:&quot;123456&quot;,&quot;name&quot;:&quot;some foo&quot;}\",\"bar\":123}]";
        var formData = new Dictionary<string, object>
        {
            [fieldId] = sourceJson,
        };
        var config = _fixture.Build<DerivedCollectionFlowConfiguration>()
            .With(c => c.SourceType, "collection")
            .Create();

        var result = _service.GenerateItemsFromSourceField(fieldId, formData, config);
        
        Assert.NotEmpty(result);
        var item = Assert.Single(result);
        Assert.Equal("123456", item.Id);
        Assert.Equal("some foo", item.DisplayName);
        Assert.Equal("Not signed yet", item.Status);
    }

    [Theory]
    [InlineData("[{\"foo\":\"{&quot;id&quot;:&quot;123456&quot;,&quot;name&quot;:&quot;some foo&quot;}\",\"bar\":123,\"Data[foo]\":\"{\\\"id\\\":\\\"456789\\\",\\\"name\\\":\\\"another foo\\\"}\"}]")]
    [InlineData("[{\"foo\":\"{&quot;id&quot;:&quot;123456&quot;,&quot;name&quot;:&quot;some foo&quot;}\",\"bar\":123,\"Data_foo\":\"{\\\"id\\\":\\\"456789\\\",\\\"name\\\":\\\"another foo\\\"}\"}]")]
    public void GenerateItemsFromSourceField_does_not_return_duplicate_entries_when_source_collection_is_modified(string sourceJson)
    {
        // A derived collection is dependent on a collection field, e.g. `trustsSearch-field-flow`.
        // When a user adds a collection item, the `sourceJson` for the derived collection contains a value with that
        // key.
        // When a user modifies the collection item, an additional key is added to the `sourceJson` (in the case of the
        // example above, `Data[trustsSearch-field-flow]` or `Data_trustsSearch-field-flow`).
        // This causes the item to be duplicated in the returned list, so keys of these formats need to be ignored.
        
        var fieldId = _fixture.Create<string>();
        var formData = new Dictionary<string, object>
        {
            [fieldId] = sourceJson,
        };
        var config = _fixture.Build<DerivedCollectionFlowConfiguration>()
            .With(c => c.SourceType, "collection")
            .Create();

        var result = _service.GenerateItemsFromSourceField(fieldId, formData, config);
        
        Assert.NotEmpty(result);
        var item = Assert.Single(result);
        Assert.Equal("123456", item.Id);
        Assert.Equal("some foo", item.DisplayName);
        Assert.Equal("Not signed yet", item.Status);
    }
}