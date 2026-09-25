using System.Globalization;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Components;

/// <summary>
/// Fluent GOV.UK table assertions for the applications listing.
/// Chain methods then await VerifyAsync() so Playwright retries still apply.
/// </summary>
public sealed class ApplicationsTable
{
    private readonly IPage _page;
    private readonly ILocator _table;
    private readonly List<Func<Task>> _assertions = [];
    private string _reference = string.Empty;

    public ApplicationsTable(IPage page)
    {
        _page = page;
        _table = page.GetByRole(AriaRole.Table);
    }

    public ApplicationsTable WithReference(string reference)
    {
        _reference = reference;
        return this;
    }

    public ApplicationsTable HasTableHeaders(IReadOnlyList<string> headers)
    {
        Enqueue(async () =>
        {
            var headerCells = HeaderCells();
            await Assertions.Expect(headerCells).ToHaveCountAsync(headers.Count);
            for (var i = 0; i < headers.Count; i++)
            {
                await Assertions.Expect(headerCells.Nth(i)).ToHaveTextAsync(headers[i]);
            }
        });
        return this;
    }

    public ApplicationsTable HasNumberOfRows(int expected)
    {
        Enqueue(async () => await Assertions.Expect(BodyRows()).ToHaveCountAsync(expected));
        return this;
    }

    public ApplicationsTable ColumnHasValue(string tableColumn, string expectedValue)
    {
        Enqueue(async () =>
        {
            var cell = await CellForColumnAsync(tableColumn);
            await Assertions.Expect(cell).ToHaveTextAsync(expectedValue);
        });
        return this;
    }

    public ApplicationsTable ColumnHasValueWithLink(string tableColumn, string expectedValue, string href)
    {
        Enqueue(async () =>
        {
            var cell = await CellForColumnAsync(tableColumn);
            var link = CellLink(cell);
            await Assertions.Expect(link).ToContainTextAsync(expectedValue);
            await Assertions.Expect(link).ToHaveAttributeAsync("href", href);
        });
        return this;
    }

    public async Task VerifyAsync()
    {
        foreach (var assertion in _assertions)
        {
            await assertion();
        }
    }

    private void Enqueue(Func<Task> assertion) => _assertions.Add(assertion);

    private ILocator HeaderCells() => _table.GetByRole(AriaRole.Columnheader);

    private ILocator BodyRows() => _table.GetByRole(AriaRole.Row).Filter(new LocatorFilterOptions { Has = _page.GetByRole(AriaRole.Cell) });

    private ILocator CellLink(ILocator cell) => cell.GetByRole(AriaRole.Link);

    private async Task<ILocator> CellForColumnAsync(string tableColumn)
    {
        if (string.IsNullOrEmpty(_reference))
        {
            throw new InvalidOperationException("Reference is not set. Call WithReference() before asserting a table cell value.");
        }

        var headerCells = HeaderCells();
        await Assertions.Expect(headerCells.Filter(new LocatorFilterOptions { HasText = tableColumn })).ToHaveCountAsync(1);

        var columnIndex = await headerCells.EvaluateAllAsync<int>(
            "(headers, column) => headers.findIndex((header) => header.textContent?.trim() === column)",
            tableColumn);

        if (columnIndex < 0)
        {
            throw new InvalidOperationException($"Table column \"{tableColumn}\" was not found.");
        }

        var row = BodyRows().Filter(new LocatorFilterOptions
        {
            Has = _page.GetByRole(AriaRole.Cell, new PageGetByRoleOptions { Name = _reference, Exact = true }),
        });

        return row.GetByRole(AriaRole.Cell).Nth(columnIndex);
    }
}

public static class ApplicationsTableFormatting
{
    public static string FormatApplicationDisplayDate(DateTime? date = null)
    {
        var value = date ?? DateTime.Now;
        return value.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("en-GB"));
    }
}
