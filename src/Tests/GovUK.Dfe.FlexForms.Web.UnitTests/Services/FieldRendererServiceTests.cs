using AutoFixture;
using AutoFixture.Kernel;
using GovUK.Dfe.CoreLibs.Testing.AutoFixture.Customizations;
using GovUK.Dfe.CoreLibs.Testing.Helpers;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Web.Services;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Task = System.Threading.Tasks.Task;
using TaskModel = GovUK.Dfe.FlexForms.Domain.Models.Task;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Services;

public class FieldRendererServiceTests
{
    private readonly IFixture _fixture;
    private readonly IServiceProvider _serviceProvider;
    private readonly FieldRendererService _service;

    public FieldRendererServiceTests()
    {
        _fixture = FixtureFactoryHelper.ConfigureFixtureFactory([
            typeof(NSubstituteWithMembersCustomization),
            typeof(OmitCircularReferenceCustomization),
            typeof(RazorPageCustomization)
        ]);

        _fixture.Customize<ValidationRule>(ob => ob.Without(rule => rule.Condition));

        _serviceProvider = Substitute.For<IServiceProvider>();
        _serviceProvider.GetService(Arg.Any<Type>())
            .Returns(call => _fixture.Create(call.Arg<Type>(), new SpecimenContext(_fixture)));

        _fixture.Register(() => _serviceProvider);

        _service = _fixture.Create<FieldRendererService>();
    }

    [Theory]
    [InlineData("some text", "some text")]
    [InlineData("&#x1F44D;", "👍")]
    [InlineData("&lt;script&gt;alert(&#x27;hello&#x27;)&lt;/script&gt;", "<script>alert('hello')</script>")]
    public async Task RenderFieldAsync_returns_expected_model_with_unsanitised_value_for_field(string currentValue, string expectedModelCurrentValue)
    {
        var htmlHelper = Substitute.For([typeof(IHtmlHelper), typeof(IViewContextAware)], []) as IHtmlHelper;

        object? capturedModel = null;
        htmlHelper!.PartialAsync(Arg.Any<string>(), Arg.Do<object?>(obj => capturedModel = obj), null)
            .Returns(_fixture.Create<IHtmlContent>());

        _serviceProvider.GetService(typeof(IHtmlHelper)).Returns(htmlHelper);

        var field = _fixture.Build<Field>().With(f => f.Type, "text").Create();
        var prefix = _fixture.Create<string>();
        var errorMessage = _fixture.Create<string>();
        var currentTask = _fixture.Create<TaskModel>();
        var currentPage = _fixture.Create<Page>();

        var result = await _service.RenderFieldAsync(field, prefix, currentValue, Array.Empty<string>(), errorMessage, currentTask, currentPage);

        Assert.NotNull(result);

        _ = htmlHelper.Received()!.PartialAsync(
            Arg.Any<string>(),
            capturedModel,
            null
        );

        Assert.NotNull(capturedModel);
        var model = Assert.IsType<FieldViewModel>(capturedModel);
        Assert.Equal(field, model.Field);
        Assert.Equal(prefix, model.Prefix);
        Assert.Equal(expectedModelCurrentValue, model.CurrentValue);
        Assert.Equal(errorMessage, model.ErrorMessage);
        Assert.Equal(currentTask, model.CurrentTask);
        Assert.Equal(currentPage, model.CurrentPage);
    }

    [Theory]
    [InlineData("text", "~/Views/Shared/Fields/_TextField.cshtml")]
    [InlineData("email", "~/Views/Shared/Fields/_EmailField.cshtml")]
    [InlineData("select", "~/Views/Shared/Fields/_SelectField.cshtml")]
    [InlineData("text-area", "~/Views/Shared/Fields/_TextAreaField.cshtml")]
    [InlineData("radios", "~/Views/Shared/Fields/_RadiosField.cshtml")]
    [InlineData("character-count", "~/Views/Shared/Fields/_CharacterCountField.cshtml")]
    [InlineData("date", "~/Views/Shared/Fields/_DateInputField.cshtml")]
    [InlineData("autocomplete", "~/Views/Shared/Fields/_AutocompleteField.cshtml")]
    [InlineData("complexField", "~/Views/Shared/Fields/_ComplexField.cshtml")]
    public async Task RenderFieldAsync_returns_expected_view_name(string fieldType, string expectedViewName)
    {
        var htmlHelper = Substitute.For([typeof(IHtmlHelper), typeof(IViewContextAware)], []) as IHtmlHelper;
        _serviceProvider.GetService(typeof(IHtmlHelper)).Returns(htmlHelper);

        var field = _fixture.Build<Field>().With(f => f.Type, fieldType).Create();
        var prefix = _fixture.Create<string>();
        var currentValue = _fixture.Create<string>();
        var errorMessage = _fixture.Create<string>();
        var currentTask = _fixture.Create<TaskModel>();
        var currentPage = _fixture.Create<Page>();

        var result = await _service.RenderFieldAsync(field, prefix, currentValue, Array.Empty<string>(), errorMessage, currentTask, currentPage);

        Assert.NotNull(result);

        _ = htmlHelper.Received()!.PartialAsync(
            expectedViewName,
            Arg.Any<object?>(),
            null
        );
    }

    [Fact]
    public async Task RenderFieldAsync_throws_NotSupportedException_when_field_type_is_not_supported()
    {
        var htmlHelper = Substitute.For([typeof(IHtmlHelper), typeof(IViewContextAware)], []) as IHtmlHelper;
        _serviceProvider.GetService(typeof(IHtmlHelper)).Returns(htmlHelper);

        var field = _fixture.Create<Field>();
        var prefix = _fixture.Create<string>();
        var currentValue = _fixture.Create<string>();
        var errorMessage = _fixture.Create<string>();
        var currentTask = _fixture.Create<TaskModel>();
        var currentPage = _fixture.Create<Page>();

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _service.RenderFieldAsync(field, prefix, currentValue, Array.Empty<string>(), errorMessage, currentTask, currentPage));
    }

    [Fact]
    public async Task RenderFieldAsync_throws_InvalidOperationException_when_IHtmlHelper_is_not_IViewContextAware()
    {
        var htmlHelper = Substitute.For<IHtmlHelper>();
        _serviceProvider.GetService(typeof(IHtmlHelper)).Returns(htmlHelper);

        var field = _fixture.Build<Field>().With(f => f.Type, "text").Create();
        var prefix = _fixture.Create<string>();
        var currentValue = _fixture.Create<string>();
        var errorMessage = _fixture.Create<string>();
        var currentTask = _fixture.Create<TaskModel>();
        var currentPage = _fixture.Create<Page>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.RenderFieldAsync(field, prefix, currentValue, Array.Empty<string>(), errorMessage, currentTask, currentPage));
    }

    [Fact]
    public async Task RenderFieldAsync_throws_InvalidOperationException_when_ActionContext_is_null()
    {
        var htmlHelper = Substitute.For([typeof(IHtmlHelper), typeof(IViewContextAware)], []) as IHtmlHelper;
        _serviceProvider.GetService(typeof(IHtmlHelper)).Returns(htmlHelper);

        var actionContextAccessor = Substitute.For<IActionContextAccessor>();
        actionContextAccessor.ActionContext.Returns(null as ActionContext);

        _serviceProvider.GetService(typeof(IActionContextAccessor)).Returns(actionContextAccessor);

        var field = _fixture.Build<Field>().With(f => f.Type, "text").Create();
        var prefix = _fixture.Create<string>();
        var currentValue = _fixture.Create<string>();
        var errorMessage = _fixture.Create<string>();
        var currentTask = _fixture.Create<TaskModel>();
        var currentPage = _fixture.Create<Page>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.RenderFieldAsync(field, prefix, currentValue, Array.Empty<string>(), errorMessage, currentTask, currentPage));
    }
}