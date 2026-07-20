using System.Text.Json;
using AutoFixture;
using GovUK.Dfe.FlexForms.Web.Pages.FormEngine;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Pages.FormEngine;

public class JsonHelpersTests
{
    private readonly IFixture _fixture = new Fixture();
    
    private static readonly object Shop = new
    {
        inventory = new
        {
            fruit = new
            {
                apples = 150,
                bananas = 145,
                cherries = 1250,
            },
            vegetables = new
            {
                carrots = 100,
                potatoes = 125,
                tomatoes = 150,
            },
        },
        staff = new
        {
            managers = new[] { "Alice" },
            cashiers = new[] { "Bob", "Charlie" },
        },
        company = "Jim's Greengrocers"
    };

    [Theory]
    [InlineData("[\"an\", \"array\"]", "array")]
    [InlineData("\"a string\"", "string")]
    [InlineData("1234.56789", "number")]
    [InlineData("true", "boolean")]
    [InlineData("false", "boolean")]
    [InlineData("null", "null")]
    public void EvaluatePath_throws_exception_if_element_is_not_object(string json, string expectedInMessage)
    {
        var element = JsonSerializer.Deserialize<JsonElement>(json);

        var exception = Assert.Throws<ArgumentException>(() => element.EvaluatePath("some.path"));
        Assert.Contains(expectedInMessage, exception.Message);
    }

    [Theory]
    [InlineData("inventory.fruit.apples", "150")]
    [InlineData("inventory.vegetables", "{\"carrots\":100,\"potatoes\":125,\"tomatoes\":150}")]
    [InlineData("staff.managers", "[\"Alice\"]")]
    public void EvaluatePath_returns_expected_value_for_path(string path, string expectedJson)
    {
        var element = JsonSerializer.SerializeToElement(Shop);
        
        var expectedResult = JsonSerializer.Deserialize<JsonElement>(expectedJson);
        var result = element.EvaluatePath(path);
        
        Assert.Equivalent(expectedResult, result);
    }

    [Theory]
    [InlineData("inventory.fruit.pineapple")]
    [InlineData("inventory.deli")]
    [InlineData("staff.accountants")]
    [InlineData("address.line1")]
    public void EvaluatePath_returns_null_for_unmatched_paths(string path)
    {
        var element = JsonSerializer.SerializeToElement(Shop);
        
        var result = element.EvaluatePath(path);
        
        Assert.Null(result);
    }

    [Theory]
    [InlineData("staff.managers.count")]
    [InlineData("company.name")]
    [InlineData("inventory.fruit.apples.price")]
    public void EvaluatePath_returns_null_if_path_has_subpath_for_a_property_that_is_not_an_object(string path)
    {
        var element = JsonSerializer.SerializeToElement(Shop);

        var result = element.EvaluatePath(path);
        
        Assert.Null(result);
    }
}