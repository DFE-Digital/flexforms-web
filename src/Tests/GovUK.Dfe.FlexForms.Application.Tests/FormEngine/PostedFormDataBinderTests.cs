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
    public void Bind_ignores_non_data_form_keys()
    {
        var form = Fields(
            ("handler", ["Page"]),
            ("Data[kept]", ["value"]));

        var data = _binder.Bind(form);

        Assert.Equal("value", data["kept"]);
        Assert.False(data.ContainsKey("handler"));
    }

    [Fact]
    public void Bind_stores_empty_string_when_posted_value_list_is_empty()
    {
        var existing = new Dictionary<string, object> { ["field"] = "previous" };
        var form = Fields(("Data[field]", Array.Empty<string>()));

        var data = _binder.Bind(form, existing);

        Assert.Equal(string.Empty, data["field"]);
    }

    [Fact]
    public void Bind_stores_single_string_when_only_one_checkbox_value_is_posted()
    {
        var form = Fields(("Data[choices]", ["", "only"]));

        var data = _binder.Bind(form);

        Assert.Equal("only", data["choices"]);
    }

    [Fact]
    public void Bind_stores_empty_array_when_all_multi_values_are_blank()
    {
        var form = Fields(("Data[choices]", ["", "   "]));

        var data = _binder.Bind(form);

        Assert.Equal(Array.Empty<string>(), data["choices"]);
    }

    [Fact]
    public void Bind_clears_checkbox_field_when_only_empty_value_is_posted()
    {
        var existing = new Dictionary<string, object>
        {
            ["priorities"] = new[] { "Attendance concerns", "Attainment" }
        };
        var form = Fields(("Data[priorities]", [""]));

        var data = _binder.Bind(form, existing);

        Assert.Equal(string.Empty, data["priorities"]);
    }

    [Fact]
    public void Bind_ignores_empty_sentinel_when_real_checkbox_values_are_posted()
    {
        var form = Fields(("Data[choices]", ["", "a", "b"]));

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

    [Fact]
    public void ApplyDateParts_encodes_html_instead_of_storing_tags()
    {
        var data = new Dictionary<string, object>();
        var form = Fields(
            ("Data[notes]-day", ["<script>"]),
            ("Data[notes]-month", ["1"]),
            ("Data[notes]-year", ["2024"]));

        _binder.ApplyDateParts(form, data);

        Assert.DoesNotContain("<script>", data["notes"].ToString());
        Assert.Contains("&lt;script&gt;", data["notes"].ToString());
    }

    [Fact]
    public void ApplyDateParts_does_not_turn_decimal_day_into_a_valid_date()
    {
        var data = new Dictionary<string, object>();
        var form = Fields(
            ("Data[dob]-day", ["1.5"]),
            ("Data[dob]-month", ["1"]),
            ("Data[dob]-year", ["2024"]));

        _binder.ApplyDateParts(form, data);

        Assert.Equal("2024-1-1.5", data["dob"]);
        Assert.NotEqual("2024-01-15", data["dob"]);
    }

    [Fact]
    public void ApplyDateParts_ignores_non_date_form_keys()
    {
        var data = new Dictionary<string, object>();
        var form = Fields(
            ("Data[dob]-day", ["1"]),
            ("Data[dob]-month", ["2"]),
            ("Data[dob]-year", ["2024"]),
            ("__RequestVerificationToken", ["token"]));

        _binder.ApplyDateParts(form, data);

        Assert.Equal("2024-02-01", data["dob"]);
    }

    [Fact]
    public void ApplyDateParts_skips_composition_when_no_part_has_a_value()
    {
        var data = new Dictionary<string, object> { ["dob"] = "old" };
        var form = Fields(
            ("Data[dob]-day", [""]),
            ("Data[dob]-month", [""]),
            ("Data[dob]-year", [""]));

        _binder.ApplyDateParts(form, data);

        Assert.Equal("old", data["dob"]);
    }

    [Fact]
    public void ApplyDateParts_writes_normalised_field_id_for_data_prefix_keys()
    {
        var data = new Dictionary<string, object>();
        var form = Fields(
            ("Data[Data_startDate]-day", ["3"]),
            ("Data[Data_startDate]-month", ["4"]),
            ("Data[Data_startDate]-year", ["2025"]));

        _binder.ApplyDateParts(form, data);

        Assert.Equal("2025-04-03", data["Data_startDate"]);
        Assert.Equal("2025-04-03", data["startDate"]);
    }

    [Fact]
    public void ApplyDateParts_joins_parts_when_values_are_not_all_numeric()
    {
        var data = new Dictionary<string, object>();
        var form = Fields(
            ("Data[dob]-day", ["ab"]),
            ("Data[dob]-month", ["cd"]),
            ("Data[dob]-year", ["ef"]));

        _binder.ApplyDateParts(form, data);

        Assert.Equal("ef-cd-ab", data["dob"]);
    }

    [Fact]
    public void ApplyDateParts_treats_whitespace_only_day_part_as_empty()
    {
        var data = new Dictionary<string, object>();
        var form = Fields(
            ("Data[dob]-day", ["   "]),
            ("Data[dob]-month", ["1"]),
            ("Data[dob]-year", ["2024"]));

        _binder.ApplyDateParts(form, data);

        Assert.Equal("2024-1-", data["dob"]);
    }

    [Fact]
    public void Bind_treats_null_single_value_as_empty_string()
    {
        var form = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["Data[field]"] = new string?[] { null! }
        };

        var data = _binder.Bind(form);

        Assert.Equal(string.Empty, data["field"]);
    }

    [Fact]
    public void Bind_ignores_null_entries_in_multi_value_fields()
    {
        var form = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["Data[choices]"] = new string?[] { null!, "a", "b" }
        };

        var data = _binder.Bind(form);

        var values = Assert.IsType<string[]>(data["choices"]);
        Assert.Equal(["a", "b"], values);
    }

    [Fact]
    public void ApplyDateParts_treats_empty_value_list_as_blank_part()
    {
        var data = new Dictionary<string, object>();
        var form = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["Data[dob]-day"] = Array.Empty<string>(),
            ["Data[dob]-month"] = ["2"],
            ["Data[dob]-year"] = ["2024"]
        };

        _binder.ApplyDateParts(form, data);

        Assert.Equal("2024-2-", data["dob"]);
    }

    [Fact]
    public void ApplyDateParts_composes_when_year_part_is_not_posted()
    {
        var data = new Dictionary<string, object>();
        var form = Fields(
            ("Data[dob]-day", ["1"]),
            ("Data[dob]-month", ["2"]));

        _binder.ApplyDateParts(form, data);

        Assert.Equal("-2-1", data["dob"]);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> Fields(
        params (string Key, string[] Values)[] items) =>
        items.ToDictionary(i => i.Key, i => (IReadOnlyList<string>)i.Values, StringComparer.Ordinal);
}
