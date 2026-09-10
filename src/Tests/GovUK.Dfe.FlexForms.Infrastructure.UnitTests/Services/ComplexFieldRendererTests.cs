using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Infrastructure.Services;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Services;

public class UploadComplexFieldRendererTests
{
    private readonly UploadComplexFieldRenderer _renderer = new();

    [Fact]
    public void FieldType_is_upload()
    {
        Assert.Equal("upload", _renderer.FieldType);
    }

    [Fact]
    public void Render_includes_label_error_tooltip_and_required_marker()
    {
        var html = _renderer.Render(
            new ComplexFieldConfiguration(),
            "supporting-doc",
            currentValue: "",
            errorMessage: "Select a file",
            label: "Upload evidence",
            tooltip: "PDF only",
            isRequired: true);

        Assert.Contains("supporting-doc-upload-input", html);
        Assert.Contains("Upload evidence", html);
        Assert.Contains("Select a file", html);
        Assert.Contains("PDF only", html);
        Assert.Contains("govuk-form-group--error", html);
        Assert.Contains("govuk-visually-hidden\">required", html);
    }
}

public class AutocompleteComplexFieldRendererTests
{
    private readonly AutocompleteComplexFieldRenderer _renderer = new();

    [Fact]
    public void FieldType_is_autocomplete()
    {
        Assert.Equal("autocomplete", _renderer.FieldType);
    }

    [Fact]
    public void Render_uses_field_id_when_configuration_id_missing()
    {
        var html = _renderer.Render(
            new ComplexFieldConfiguration
            {
                FieldType = "autocomplete",
                AllowMultiple = true,
                MaxSelections = 2,
                MinLength = 2,
                Placeholder = "Search schools"
            },
            "school",
            currentValue: "[]",
            errorMessage: "",
            label: "School",
            tooltip: "Start typing",
            isRequired: false);

        Assert.Contains("school-complex-field-container", html);
        Assert.Contains("name=\"Data[school]\"", html);
        Assert.Contains("data-allow-multiple=\"true\"", html);
        Assert.Contains("data-max-selections=\"2\"", html);
        Assert.Contains("Start typing", html);
        Assert.Contains("You can select multiple options.", html);
    }

    [Fact]
    public void Render_uses_configuration_id_when_present()
    {
        var html = _renderer.Render(
            new ComplexFieldConfiguration { Id = "establishment", MinLength = 3 },
            "school",
            currentValue: "",
            errorMessage: "Pick one",
            label: "School",
            tooltip: "",
            isRequired: true);

        Assert.Contains("establishment-complex-field-container", html);
        Assert.Contains("Pick one", html);
    }

    [Fact]
    public void Render_uses_stable_ids_for_label_and_aria_and_does_not_use_label_text()
    {
        var html = _renderer.Render(
            new ComplexFieldConfiguration { Id = "trust", MinLength = 3 },
            "incomingTrustSearch",
            currentValue: "",
            errorMessage: "",
            label: "Find a trust",
            tooltip: "Enter at least 3 characters",
            isRequired: true);

        Assert.Contains("id=\"trust-complex-field-label\"", html);
        Assert.Contains("for=\"trust-complex-field\"", html);
        Assert.Contains("data-input-id=\"trust-complex-field\"", html);
        Assert.Contains("data-label-id=\"trust-complex-field-label\"", html);
        Assert.Contains("id=\"trust-complex-field-tooltip\"", html);
        Assert.Contains("data-describedby=\"trust-complex-field-tooltip trust-complex-field-hint\"", html);
        Assert.DoesNotContain("Find a trust-label", html);
        Assert.DoesNotContain("aria-labelledby=\"Find a trust-label\"", html);
        Assert.DoesNotContain("aria-describedby=\"trust-complex-field-hint\"", html);
    }
}

public class CompositeComplexFieldRendererTests
{
    private readonly CompositeComplexFieldRenderer _renderer = new();

    [Fact]
    public void FieldType_is_composite()
    {
        Assert.Equal("composite", _renderer.FieldType);
    }

    [Fact]
    public void Render_includes_subfield_hint_and_error()
    {
        var configuration = new ComplexFieldConfiguration
        {
            AdditionalProperties = new Dictionary<string, object>
            {
                ["SubFields"] = "name,address"
            }
        };

        var html = _renderer.Render(
            configuration,
            "address-block",
            currentValue: "",
            errorMessage: "Enter address details",
            label: "Address",
            tooltip: "Use postcode lookup",
            isRequired: true);

        Assert.Contains("Composite field - name,address", html);
        Assert.Contains("Enter address details", html);
        Assert.Contains("Use postcode lookup", html);
        Assert.Contains("data-module=\"composite-field\"", html);
    }
}
