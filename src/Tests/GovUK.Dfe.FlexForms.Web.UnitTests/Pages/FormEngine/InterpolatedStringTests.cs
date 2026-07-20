using System.Text.Json;
using GovUK.Dfe.FlexForms.Web.Pages.FormEngine;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Pages.FormEngine;

public class InterpolatedStringTests
{
    [Theory]
    [InlineData("{foo} was successful", new[] {"foo"}, new[] {"bar"}, "bar was successful")]
    [InlineData("Successfully did {bar} to {quux}", new[] {"bar", "quux"}, new[] {"xyzzy", "bleeb"}, "Successfully did xyzzy to bleeb")]
    public void Render_when_message_has_interpolation_and_data_is_not_null_then_return_interpolated_message(string message, string[] interpolationKeys, string[] interpolationValues, string expected)
    {
        var data = new Dictionary<string, object>();
        for (var i = 0; i < interpolationKeys.Length; i++)
        {
            data.Add(interpolationKeys[i], interpolationValues[i]);
        }
        
        var result = new InterpolatedString(message).Render(data);
        
        Assert.Equal(expected, result);
    }
    
    [Theory]
    [InlineData("{foo} was successful")]
    [InlineData("Successfully did {bar} to {quux}")]
    public void Render_when_message_has_interpolation_and_data_is_null_then_return_message_with_no_interpolation(string message)
    {
        var data = new Dictionary<string, object>();
        
        var result = new InterpolatedString(message).Render(data);
        
        Assert.Equal(message, result);
    }

    [Theory]
    [InlineData("{foo??The operation} was successful", "The operation was successful")]
    [InlineData("Successfully did { bar ?? something } to {quux  ??     another thing   }", "Successfully did something to another thing")]
    public void Render_when_message_has_interpolation_with_default_value_and_data_is_null_then_return_message_with_default_value(string message, string expectedMessage)
    {
        var data = new Dictionary<string, object>();
        
        var result = new InterpolatedString(message).Render(data);
        
        Assert.Equal(expectedMessage, result);
    }

    [Fact]
    public void Render_when_data_has_a_key_with_a_JsonElement_value_then_return_interpolated_message()
    {
        var message = "{foo} was successful";
        var obj = new
        {
            bar = "bar"
        };

        var data = new Dictionary<string, object>
        {
            { "foo", JsonSerializer.SerializeToElement(obj) }
        };
        
        var result = new InterpolatedString(message).Render(data);
        
        Assert.Equal("{\"bar\":\"bar\"} was successful", result);
    }

    [Fact]
    public void Render_when_data_has_a_key_with_a_JsonElement_value_then_subkeys_can_be_interpolated()
    {
        var message = "{ foo.bar.baz } was {foo.xyzzy}";
        var obj = new
        {
            bar = new
            {
                baz = "quux",
                nope = "nope"
            },
            xyzzy = "bleeb",
            nope = "nope"
        };

        var data = new Dictionary<string, object>
        {
            { "foo", JsonSerializer.SerializeToElement(obj) }
        };
        
        var result = new InterpolatedString(message).Render(data);
        
        Assert.Equal("quux was bleeb", result);
    }

    [Fact]
    public void Render_when_data_has_a_key_with_a_JsonElement_value_then_missing_subkeys_are_not_interpolated()
    {
        var message = "{ foo.bar.baz } was {foo.xyzzy}";
        
        var obj = new
        {
            bar = new
            {
                nope = "nope"
            },
            nope = "nope"
        };

        var data = new Dictionary<string, object>
        {
            { "foo", JsonSerializer.SerializeToElement(obj) }
        };
        
        var result = new InterpolatedString(message).Render(data);
        
        Assert.Equal("{ foo.bar.baz } was {foo.xyzzy}", result);
    }
    
    
    [Fact]
    public void Render_when_data_has_a_key_with_a_Dictionary_value_then_return_interpolated_message()
    {
        var message = "{foo} was successful";
        var value = new Dictionary<string, object>
        {
            ["bar"] = "bar"
        };

        var data = new Dictionary<string, object>
        {
            { "foo", value }
        };
        
        var result = new InterpolatedString(message).Render(data);
        
        Assert.Equal("{\"bar\":\"bar\"} was successful", result);
    }

    [Fact]
    public void Render_when_data_has_a_key_with_a_Dictionary_value_then_subkeys_can_be_interpolated()
    {
        var message = "{ foo.bar.baz } was {foo.xyzzy}";
        var value = new Dictionary<string, object>
        {
            ["bar"] = new Dictionary<string, object>
            {
                ["baz"] = "quux",
                ["nope"] = "nope"
            },
            ["xyzzy"] = "bleeb",
            ["nope"] = "nope"
        };

        var data = new Dictionary<string, object>
        {
            { "foo", value }
        };
        
        var result = new InterpolatedString(message).Render(data);
        
        Assert.Equal("quux was bleeb", result);
    }

    [Fact]
    public void Render_when_data_has_a_key_with_a_Dictionary_value_then_missing_subkeys_are_not_interpolated()
    {
        var message = "{ foo.bar.baz } was {foo.xyzzy}";
        
        var value = new Dictionary<string, object>
        {
            ["bar"] = new Dictionary<string, object>
            {
                ["nope"] = "nope"
            },
            ["nope"] = "nope"
        };

        var data = new Dictionary<string, object>
        {
            { "foo", value }
        };
        
        var result = new InterpolatedString(message).Render(data);
        
        Assert.Equal("{ foo.bar.baz } was {foo.xyzzy}", result);
    }

    [Fact]
    public void Render_when_interpolating_item_data_will_greedily_match_keys()
    {
        var message = "{ foo.bar.baz } was {foo.xyzzy}";

        var data = new Dictionary<string, object>
        {
            ["foo"] = new Dictionary<string, object>
            {
                ["bar"] = new Dictionary<string, object>
                {
                    ["baz"] = "nope",
                }
                ["xyzzy"] = "nope",
            },
            ["foo.bar"] = new Dictionary<string, object>
            {
                ["baz"] = "quux",
            },
            ["foo.xyzzy"] = "bleeb",
        };
        
        var result = new InterpolatedString(message).Render(data);
        
        Assert.Equal("quux was bleeb", result);
    }

    [Fact]
    public void Render_can_handle_nonalphanumeric_characters_in_keys()
    {
        var message = "{Data[incomingTrustsSearch-field-flow]} was successfully added to the thing";
        
        var data = new Dictionary<string, object>
        {
            {"Data[incomingTrustsSearch-field-flow]", JsonSerializer.SerializeToElement("A trust")}
        };
        
        var result = new InterpolatedString(message).Render(data);
        
        Assert.Equal("A trust was successfully added to the thing", result);
    }
}