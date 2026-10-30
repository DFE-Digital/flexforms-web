using GovUK.Dfe.FlexForms.Application.DisplayCopy;
using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Application.Tests.DisplayCopy;

public class TemplateCopyContextTests
{
    [Fact]
    public void Use_SetsTheActiveTemplate()
    {
        var context = new TemplateCopyContext();
        var template = new FormTemplate
        {
            TemplateId = "form-001",
            TemplateName = "Visits",
            Description = "Visits",
            TaskGroups = []
        };

        context.Use(template);

        Assert.Same(template, context.Template);
    }

    [Fact]
    public void TryUseJson_ReturnsFalse_WhenJsonIsBlank()
    {
        var context = new TemplateCopyContext();

        Assert.False(context.TryUseJson(null));
        Assert.False(context.TryUseJson("   "));
        Assert.Null(context.Template);
    }

    [Fact]
    public void TryUseJson_ReturnsFalse_WhenJsonIsInvalid()
    {
        var context = new TemplateCopyContext();

        Assert.False(context.TryUseJson("not-json"));
        Assert.Null(context.Template);
    }

    [Fact]
    public void TryUseJson_LoadsTemplate_WhenJsonIsValid()
    {
        var context = new TemplateCopyContext();
        const string json = """
            {
              "templateId": "form-001",
              "templateName": "Visits",
              "description": "Visits",
              "taskGroups": [],
              "displayCopy": {
                "terminology": { "singular": "visit", "plural": "visits" }
              }
            }
            """;

        Assert.True(context.TryUseJson(json));
        Assert.Equal("form-001", context.Template!.TemplateId);
        Assert.Equal("visit", context.Template.DisplayCopy!.Terminology!.Singular);
    }
}
