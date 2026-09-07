using System.Text;
using System.Text.Json;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Domain.Templates;

namespace GovUK.Dfe.FlexForms.Domain.Tests.Templates;

public class StarterFormTemplateSchemaTests
{
    [Fact]
    public void Create_ShouldBuildMinimalValidTemplate()
    {
        var template = StarterFormTemplateSchema.Create(Guid.NewGuid().ToString(), "  Transfer  ");

        Assert.Equal("Transfer", template.TemplateName);
        Assert.Equal(StarterFormTemplateSchema.DefaultVersionNumber, "1.0.0");
        Assert.Single(template.TaskGroups);
        var task = template.TaskGroups[0].Tasks[0];
        Assert.Equal("starter-group", template.TaskGroups[0].GroupId);
        Assert.Equal("starter-task", task.TaskId);
        Assert.Equal("starter-page", task.Pages![0].PageId);
        Assert.Equal("starter-text", task.Pages[0].Fields[0].FieldId);
    }

    [Fact]
    public void Create_ShouldThrow_WhenArgumentsAreMissing()
    {
        Assert.Throws<ArgumentException>(() => StarterFormTemplateSchema.Create(" ", "Name"));
        Assert.Throws<ArgumentException>(() => StarterFormTemplateSchema.Create("id", " "));
    }

    [Fact]
    public void CreateJson_ShouldRoundTrip()
    {
        var json = StarterFormTemplateSchema.CreateJson("tpl-1", "Starter");
        var template = JsonSerializer.Deserialize<FormTemplate>(json);

        Assert.Equal("tpl-1", template!.TemplateId);
        Assert.Equal("Starter", template.TemplateName);
    }

    [Fact]
    public void CreateBase64Json_ShouldDecodeToSameJson()
    {
        var json = StarterFormTemplateSchema.CreateJson("tpl-1", "Starter");
        var encoded = StarterFormTemplateSchema.CreateBase64Json("tpl-1", "Starter");

        Assert.Equal(json, Encoding.UTF8.GetString(Convert.FromBase64String(encoded)));
    }
}
