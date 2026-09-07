using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Options;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TaskModel = GovUK.Dfe.FlexForms.Domain.Models.Task;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Services;

public class ApplicationTerminologyProviderTests
{
    [Fact]
    public void Properties_ShouldCapitaliseConfiguredTerms()
    {
        var provider = new ApplicationTerminologyProvider(Options.Create(new ApplicationTerminologyOptions
        {
            Singular = "reform plan",
            Plural = "reform plans"
        }));

        Assert.Equal("reform plan", provider.Singular);
        Assert.Equal("Reform plan", provider.SingularCapitalised);
        Assert.Equal("reform plans", provider.Plural);
        Assert.Equal("Reform plans", provider.PluralCapitalised);
    }

    [Fact]
    public void Capitalise_ShouldReturnEmpty_WhenValueIsEmpty()
    {
        var provider = new ApplicationTerminologyProvider(Options.Create(new ApplicationTerminologyOptions
        {
            Singular = "",
            Plural = ""
        }));

        Assert.Equal("", provider.SingularCapitalised);
        Assert.Equal("", provider.PluralCapitalised);
    }
}

public class EventTypeRegistryTests
{
    [Fact]
    public void GetEventType_ShouldResolveDiscoveredAndRegisteredTypes()
    {
        var registry = new EventTypeRegistry(NullLogger<EventTypeRegistry>.Instance);
        var catalogue = registry.GetCatalogue();
        Assert.NotEmpty(catalogue);

        var first = catalogue[0];
        Assert.Same(first.ClrType, registry.GetEventType(first.EventTypeName));
        Assert.Null(registry.GetEventType(""));
        Assert.Null(registry.GetEventType("DefinitelyMissingEvent"));

        registry.Register(typeof(string));
        Assert.Equal(typeof(string), registry.GetEventType(nameof(String)));
        Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
    }
}

public class FormNavigationServiceTests
{
    [Fact]
    public void UrlHelpers_ShouldBuildExpectedRoutes()
    {
        var history = Substitute.For<INavigationHistoryService>();
        var navigation = new FormNavigationService(history);

        Assert.Equal("/applications/REF-1/t1", navigation.GetNextPageUrl("p1", "t1", "REF-1"));
        Assert.Equal("/applications/REF-1/t1", navigation.GetPreviousPageUrl("p1", "t1", "REF-1"));
        Assert.Equal("/applications/REF-1/t1", navigation.GetTaskSummaryUrl("t1", "REF-1"));
        Assert.Equal("/applications/REF-1/preview", navigation.GetApplicationPreviewUrl("REF-1"));
        Assert.Equal("/applications/REF-1", navigation.GetTaskListUrl("REF-1"));
        Assert.Equal("/applications/REF-1/t1", navigation.GetCollectionFlowSummaryUrl("t1", "REF-1"));
        Assert.Equal("/applications/REF-1/t1/flow/f1/i1", navigation.GetStartSubFlowUrl("t1", "REF-1", "f1", "i1"));
        Assert.Equal("/applications/REF-1/t1/flow/f1/i1/p2", navigation.GetSubFlowPageUrl("t1", "REF-1", "f1", "i1", "p2"));
        Assert.True(navigation.CanNavigateToPage("p1", "t1", "REF-1"));
        Assert.False(navigation.CanNavigateToPage("", "t1", "REF-1"));
    }

    [Fact]
    public void GetBackLinkUrl_ShouldPreferHistoryThenFallBack()
    {
        var history = Substitute.For<INavigationHistoryService>();
        history.Peek(Arg.Any<string>()).Returns("/applications/REF-1/t1/p1");
        var navigation = new FormNavigationService(history);

        Assert.Equal("/applications/REF-1/t1/p1?nav=back", navigation.GetBackLinkUrl("p1", "t1", "REF-1"));

        history.Peek(Arg.Any<string>()).Returns((string?)null);
        Assert.Equal("/applications/REF-1/t1", navigation.GetBackLinkUrl("p1", "t1", "REF-1"));
        Assert.Equal("/applications/REF-1", navigation.GetBackLinkUrl("", "t1", "REF-1"));
        Assert.Equal("/applications/REF-1", navigation.GetBackLinkUrl("", "", "REF-1"));
    }

    [Fact]
    public void GetNextNavigationTargetAfterSave_ShouldHonourReturnToSummaryAndNextPage()
    {
        var navigation = new FormNavigationService(Substitute.For<INavigationHistoryService>());
        var first = new Page { PageId = "p1", Slug = "p1", Title = "p1", Description = "p1", PageOrder = 1, Fields = [], ReturnToSummaryPage = true };
        var second = new Page { PageId = "p2", Slug = "p2", Title = "p2", Description = "p2", PageOrder = 2, Fields = [], ReturnToSummaryPage = false };
        var task = new TaskModel { TaskId = "t1", TaskName = "t", TaskOrder = 1, TaskStatusString = "NotStarted", Pages = [first, second] };

        Assert.Equal("/applications/REF-1/t1", navigation.GetNextNavigationTargetAfterSave(first, task, "REF-1"));

        first.ReturnToSummaryPage = false;
        Assert.Equal("/applications/REF-1/t1/p2", navigation.GetNextNavigationTargetAfterSave(first, task, "REF-1"));
        Assert.Equal("/applications/REF-1/t1", navigation.GetNextNavigationTargetAfterSave(second, task, "REF-1"));
    }
}

public class ComplexFieldRendererFactoryTests
{
    [Fact]
    public void GetRenderer_ShouldMatchTypeThenFallBackToAutocomplete()
    {
        var upload = Substitute.For<IComplexFieldRenderer>();
        upload.FieldType.Returns("upload");
        var autocomplete = Substitute.For<IComplexFieldRenderer>();
        autocomplete.FieldType.Returns("autocomplete");
        var factory = new ComplexFieldRendererFactory([upload, autocomplete]);

        Assert.Same(upload, factory.GetRenderer("UPLOAD"));
        Assert.Same(autocomplete, factory.GetRenderer("missing"));
    }
}

public class NavigationHistoryServiceTests
{
    [Fact]
    public void PushPeekPop_ShouldIgnoreBlanksAndAvoidDuplicates()
    {
        var session = Substitute.For<IFormSessionStore>();
        string? stored = null;
        session.GetString(Arg.Any<string>()).Returns(_ => stored);
        session.When(s => s.SetString(Arg.Any<string>(), Arg.Any<string>()))
            .Do(call => stored = call.ArgAt<string>(1));
        var history = new NavigationHistoryService(session, NullLogger<NavigationHistoryService>.Instance);

        history.Push(" ", "/a");
        history.Push("scope", " ");
        Assert.Null(history.Peek(" "));
        Assert.Null(history.Pop(" "));

        history.Push("scope", "/a");
        history.Push("scope", "/a");
        history.Push("scope", "/b");
        Assert.Equal("/b", history.Peek("scope"));
        Assert.Equal("/b", history.Pop("scope"));
        Assert.Equal("/a", history.Peek("scope"));

        history.Clear("scope");
        session.Received().Remove(Arg.Is<string>(k => k.Contains("scope")));
        history.Clear(" ");
    }
}

public class FileUploadServiceTests
{
    [Fact]
    public async System.Threading.Tasks.Task Methods_ShouldDelegateToClient_AndRethrow()
    {
        var client = Substitute.For<IApplicationsClient>();
        var service = new FileUploadService(client, NullLogger<FileUploadService>.Instance);
        var appId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var upload = new UploadDto { Id = fileId, OriginalFileName = "a.pdf" };
        client.UploadFileAsync(appId, "a.pdf", null, Arg.Any<FileParameter>(), Arg.Any<CancellationToken>())
            .Returns(upload);
        client.GetFilesForApplicationAsync(appId, Arg.Any<CancellationToken>()).Returns([upload]);
        using var stream = new MemoryStream();
        client.DownloadFileAsync(fileId, appId, Arg.Any<CancellationToken>())
            .Returns(new FileResponse(200, new Dictionary<string, IEnumerable<string>>(), stream, Substitute.For<IDisposable>(), Substitute.For<IDisposable>()));

        Assert.Equal(fileId, (await service.UploadFileAsync(appId, "a.pdf")).Id);
        Assert.Single(await service.GetFilesForApplicationAsync(appId));
        Assert.Equal(200, (await service.DownloadFileAsync(fileId, appId)).StatusCode);
        await service.DeleteFileAsync(fileId, appId);
        await client.Received().DeleteFileAsync(fileId, appId, Arg.Any<CancellationToken>());

        client.UploadFileAsync(appId, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileParameter>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("up"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadFileAsync(appId, "a.pdf"));
        client.DownloadFileAsync(fileId, appId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("down"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DownloadFileAsync(fileId, appId));
        client.DeleteFileAsync(fileId, appId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("del"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteFileAsync(fileId, appId));
    }
}

public class TemplateManagementServiceTests
{
    [Fact]
    public async System.Threading.Tasks.Task LoadTemplateAsync_ShouldUseApplicationSchemaWhenPresent()
    {
        var parser = Substitute.For<IFormTemplateParser>();
        var provider = Substitute.For<IFormTemplateProvider>();
        var template = new FormTemplate { TemplateId = "tpl", TemplateName = "tpl", Description = "tpl", TaskGroups = [] };
        parser.ParseAsync(Arg.Any<Stream>()).Returns(template);
        var service = new TemplateManagementService(provider, parser, NullLogger<TemplateManagementService>.Instance);

        var loaded = await service.LoadTemplateAsync("tpl", new ApplicationDto
        {
            ApplicationId = Guid.NewGuid(),
            TemplateSchema = new TemplateSchemaDto
            {
                TemplateId = Guid.NewGuid(),
                TemplateVersionId = Guid.NewGuid(),
                VersionNumber = "1",
                JsonSchema = """{"templateId":"tpl"}"""
            }
        });

        Assert.Same(template, loaded);
        await provider.DidNotReceive().GetTemplateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async System.Threading.Tasks.Task LoadTemplateAsync_ShouldUseProvider_WhenSchemaMissing()
    {
        var parser = Substitute.For<IFormTemplateParser>();
        var provider = Substitute.For<IFormTemplateProvider>();
        var template = new FormTemplate { TemplateId = "tpl", TemplateName = "tpl", Description = "tpl", TaskGroups = [] };
        provider.GetTemplateAsync("tpl", Arg.Any<CancellationToken>()).Returns(template);
        var service = new TemplateManagementService(provider, parser, NullLogger<TemplateManagementService>.Instance);

        Assert.Same(template, await service.LoadTemplateAsync("tpl"));
    }

    [Fact]
    public void FindTaskAndPage_ShouldReturnMatchingItems()
    {
        var page = new Page { PageId = "p1", Slug = "p1", Title = "p1", Description = "p1", PageOrder = 1, Fields = [] };
        var task = new TaskModel { TaskId = "t1", TaskName = "t", TaskOrder = 1, TaskStatusString = "NotStarted", Pages = [page] };
        var group = new TaskGroup { GroupId = "g1", GroupName = "g", GroupOrder = 1, GroupStatus = "NotStarted", Tasks = [task] };
        var template = new FormTemplate { TemplateId = "tpl", TemplateName = "tpl", Description = "tpl", TaskGroups = [group] };
        var service = new TemplateManagementService(
            Substitute.For<IFormTemplateProvider>(),
            Substitute.For<IFormTemplateParser>(),
            NullLogger<TemplateManagementService>.Instance);

        var foundTask = service.FindTask(template, "t1");
        Assert.Equal("t1", foundTask.Task.TaskId);
        var foundPage = service.FindPage(template, "p1");
        Assert.Equal("p1", foundPage.Page.PageId);
    }
}
