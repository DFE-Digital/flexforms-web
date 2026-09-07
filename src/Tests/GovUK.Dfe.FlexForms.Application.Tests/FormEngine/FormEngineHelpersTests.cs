using GovUK.Dfe.FlexForms.Application.FormEngine;

namespace GovUK.Dfe.FlexForms.Application.Tests.FormEngine;

public class FormEngineCollectionItemsTests
{
    [Fact]
    public void Read_ShouldReturnEmpty_WhenMissingOrNotArray()
    {
        Assert.Empty(FormEngineCollectionItems.Read(new Dictionary<string, object>(), "members"));
        Assert.Empty(FormEngineCollectionItems.Read(new Dictionary<string, object> { ["members"] = "not-json" }, "members"));
        Assert.Empty(FormEngineCollectionItems.Read(new Dictionary<string, object> { ["members"] = "{}" }, "members"));
    }

    [Fact]
    public void Read_ShouldDeserializeItems()
    {
        var items = FormEngineCollectionItems.Read(
            new Dictionary<string, object> { ["members"] = """[{"id":"i1","name":"Ada"}]""" },
            "members");

        Assert.Single(items);
        Assert.Equal("i1", items[0]["id"].ToString());
    }

    [Fact]
    public void Read_ShouldReturnEmpty_WhenJsonIsInvalidArray()
    {
        Assert.Empty(FormEngineCollectionItems.Read(
            new Dictionary<string, object> { ["members"] = "[not-json]" },
            "members"));
    }
}

public class FormEngineOutcomeTests
{
    [Fact]
    public void Factories_ShouldSetKindAndPayload()
    {
        Assert.Equal(FormEngineOutcomeKind.StayOnPage, FormEngineOutcome.Stay(errorMessage: "e").Kind);
        Assert.Equal("/next", FormEngineOutcome.Redirect("/next").RedirectUrl);
        Assert.Equal("/Applications/Done", FormEngineOutcome.RedirectToPage("/Applications/Done").PageName);
        Assert.Equal(FormEngineOutcomeKind.NotFound, FormEngineOutcome.NotFound().Kind);
        Assert.Equal("bad", FormEngineOutcome.BadRequest("bad").ErrorMessage);

        using var stream = new MemoryStream();
        var file = FormEngineOutcome.File(stream, "text/plain", "a.txt");
        Assert.Equal(FormEngineOutcomeKind.FileDownload, file.Kind);
        Assert.Equal("a.txt", file.FileDownloadName);
    }
}

public class FormEngineConstantsTests
{
    [Fact]
    public void CreateDummyTemplate_ShouldHaveDummyIds()
    {
        var template = FormEngineConstants.CreateDummyTemplate();

        Assert.Equal("dummy", template.TemplateId);
        Assert.Empty(template.TaskGroups);
        Assert.Equal("UPLOAD_FIELD_SESSION_DATA", FormEngineConstants.UploadFieldSessionPlaceholder);
    }
}
