using Newtonsoft.Json;
using RulesEngine.Models;
using System.Dynamic;
using System.Globalization;
using Xunit.Abstractions;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Services
{
    public class DataValidatorTest(ITestOutputHelper output)
    {
        private readonly DataValidator validator = new("Services/rules.json");

        [Fact]
        public async Task ValidateData_ValidData_ReturnsTrueAsync()
        {
            var data = new ExpandoObject() as IDictionary<string, object>;
            data.Add(ToTitleCase("start-year"), 2026);
            data.Add(ToTitleCase("end-year"), 2027);
            data.Add(ToTitleCase("local-authority"), "LA1");
            output.WriteLine($"Data: {JsonConvert.SerializeObject(data)}");

            // Act
            IEnumerable<string> errors = await validator.ValidateAsync(data);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public async Task ValidateData_StartYearTooEarly_ReturnsFalseAsync()
        {
            var data = new ExpandoObject() as IDictionary<string, object>;
            data.Add(ToTitleCase("start-year"), 1999);
            data.Add(ToTitleCase("end-year"), 2027);
            data.Add(ToTitleCase("local-authority"), "LA1");
            output.WriteLine($"Data: {JsonConvert.SerializeObject(data)}");

            // Act
            IEnumerable<string> errors = await validator.ValidateAsync(data);

            // Assert
            Assert.NotEmpty(errors);
            output.WriteLine($"Errors: {JsonConvert.SerializeObject(errors)}");
        }

        [Fact]
        public async Task ValidateData_EndYearBeforeStartYear_ReturnsFalseAsync()
        {
            var data = new ExpandoObject() as IDictionary<string, object>;
            data.Add(ToTitleCase("start-year"), 2026);
            data.Add(ToTitleCase("end-year"), 2025);
            data.Add(ToTitleCase("local-authority"), "LA1");
            output.WriteLine($"Data: {JsonConvert.SerializeObject(data)}");

            // Act
            IEnumerable<string> errors = await validator.ValidateAsync(data);

            // Assert
            Assert.NotEmpty(errors);
            output.WriteLine($"Errors: {JsonConvert.SerializeObject(errors)}");
        }


        [Fact]
        public async Task ValidateData_NoLocalAuthority_ReturnsFalseAsync()
        {
            var data = new ExpandoObject() as IDictionary<string, object>;
            data.Add(ToTitleCase("start-year"), 2026);
            data.Add(ToTitleCase("end-year"), 2027);
            data.Add(ToTitleCase("local-authority"), "");
            output.WriteLine($"Data: {JsonConvert.SerializeObject(data)}");

            // Act
            IEnumerable<string> errors = await validator.ValidateAsync(data);

            // Assert
            Assert.NotEmpty(errors);
            output.WriteLine($"Errors: {JsonConvert.SerializeObject(errors)}");
        }

        private static string ToTitleCase(string name)
        {
            name = name.Replace("-", " ");
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name).Replace(" ", string.Empty);
        }
    }

    // TODO move this to a separate file and namespace, e.g., GovUK.Dfe.FlexForms.Web.Services
    internal class DataValidator(string rulesPath)
    {
        internal async Task<IEnumerable<string>> ValidateAsync(dynamic data)
        {
            string rulesJson = File.ReadAllText(rulesPath);
            Workflow[]? workflows = JsonConvert.DeserializeObject<Workflow[]>(rulesJson);
            RulesEngine.RulesEngine rulesEngine = new(workflows);
            IEnumerable<RuleResultTree> results = await rulesEngine.ExecuteAllRulesAsync("WorkflowA", data);
            return results.Where(result => !result.IsSuccess).Select(result => $"Rule: {result.Rule.RuleName}, Exception: {result.ExceptionMessage}.");
        }
    }
}
