using GovUK.Dfe.FlexForms.Application.FormEngine;

namespace GovUK.Dfe.FlexForms.Application.Tests.FormEngine;

public class PostedFormDataBinderTests
{
    private readonly PostedFormDataBinder _binder = new();

    [Fact]
    public void Bind_sanitises_single_field_and_keeps_existing_values()
    {
        var existing = new Dictionary<string, object> { ["kept"] = "yes" };
        var form = Fields(("Data[someField]", ["<b>hi</b>"]));

        var data = _binder.Bind(form, existing);

        Assert.Equal("&lt;b&gt;hi&lt;/b&gt;", data["someField"]);
        Assert.Equal("yes", data["kept"]);
    }

    [Fact]
    public void Bind_writes_normalised_autocomplete_field_id()
    {
        var form = Fields(("Data[Data_trustsSearch]", ["Acme Trust"]));

        var data = _binder.Bind(form);

        Assert.Equal("Acme Trust", data["Data_trustsSearch"]);
        Assert.Equal("Acme Trust", data["trustsSearch"]);
    }

    [Fact]
    public void Bind_stores_multi_value_fields_as_arrays()
    {
        var form = Fields(("Data[choices]", ["a", "b"]));

        var data = _binder.Bind(form);

        var values = Assert.IsType<string[]>(data["choices"]);
        Assert.Equal(["a", "b"], values);
    }

    [Fact]
    public void ApplyDateParts_composes_iso_date_when_year_is_four_digits()
    {
        var data = new Dictionary<string, object>();
        var form = Fields(
            ("Data[dob]-day", ["7"]),
            ("Data[dob]-month", ["8"]),
            ("Data[dob]-year", ["2024"]));

        _binder.ApplyDateParts(form, data);

        Assert.Equal("2024-08-07", data["dob"]);
    }

    [Fact]
    public void ApplyDateParts_leaves_joined_parts_when_year_is_not_four_digits()
    {
        var data = new Dictionary<string, object>();
        var form = Fields(
            ("Data[dob].Day", ["7"]),
            ("Data[dob].Month", ["8"]),
            ("Data[dob].Year", ["24"]));

        _binder.ApplyDateParts(form, data);

        Assert.Equal("24-8-7", data["dob"]);
    }

    [Fact]
    public void ApplyDateParts_joins_invalid_calendar_dates()
    {
        var data = new Dictionary<string, object>();
        var form = Fields(
            ("Data[dob]-day", ["31"]),
            ("Data[dob]-month", ["2"]),
            ("Data[dob]-year", ["2024"]));

        _binder.ApplyDateParts(form, data);

        Assert.Equal("2024-2-31", data["dob"]);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> Fields(
        params (string Key, string[] Values)[] items) =>
        items.ToDictionary(i => i.Key, i => (IReadOnlyList<string>)i.Values, StringComparer.Ordinal);
}
