using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Web.Services;
using NSubstitute;
using Xunit.Abstractions;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Services
{
    public class ApplicationImporterTest
    {
        private readonly ITestOutputHelper output;
        private readonly ApplicationImporter applicationImporter;
        private readonly ITemplateManagementService mockTemplateManagementService;

        public ApplicationImporterTest(ITestOutputHelper output)
        {
            this.output = output;
            mockTemplateManagementService = Substitute.For<ITemplateManagementService>();
            applicationImporter = new ApplicationImporter(mockTemplateManagementService);
        }

        [Fact]
        public async System.Threading.Tasks.Task ImportApplication()
        {
            var templateId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            using FileStream fileStream = new(@"Services\application.xlsx", FileMode.Open);

            FormTemplate formTemplate = CreateTemplate(templateId);

            mockTemplateManagementService.LoadTemplateAsync(templateId.ToString()).Returns(formTemplate);

            // TODO get the mapping from an external source, e.g. a JSON file or API (database)
            SpreadsheetTemplateMapping templateMapping = new()
            {
                SheetName = "Sheet1",
                Maps = new Dictionary<string, string>()
                {
                    { "B1", "start-year" },
                    { "B2", "end-year" },
                    { "B3", "local-authority" }
                }
            };

            ApplicationImportResult result = await applicationImporter.ImportSpreadsheet(templateId, fileStream, templateMapping);

            Assert.NotNull(result);
            if (result.Errors != null && result.Errors.Any())
            {
                output.WriteLine($"Errors: {string.Join(", ", result.Errors)}");
            }
            Assert.True(result.Success);
            Assert.Null(result.Errors);
            Assert.NotNull(result.Template);
            Assert.Equal(formTemplate.TemplateId, result.Template!.TemplateId);
            Assert.NotNull(result.Data);
            Assert.Equal(3, result.Data.Count);
            Assert.Equal("2026", result.Data["B1"]);
            Assert.Equal("2027", result.Data["B2"]);
            Assert.Equal("LA1", result.Data["B3"]);
            output.WriteLine(string.Join(", ", result.Data.Select(kvp => $"{kvp.Key} '{kvp.Value}'")));
        }

        private static FormTemplate CreateTemplate(Guid templateId)
        {
            return new()
            {
                TemplateId = templateId.ToString(),
                TemplateName = "Test Template",
                Description = "Test Description",
                TaskGroups =
                [
                    new() {
                        GroupId = "TaskGroup1",
                        GroupName = "Task Group 1",
                        GroupOrder = 1,
                        GroupStatus = "OK",
                        Tasks =
                        [
                            new()
                            {
                                TaskId = "Task1",
                                TaskName = "Task 1",
                                TaskOrder = 1,
                                TaskStatusString = "OK",
                                Pages =
                                [
                                    new() {
                                        PageId = "Page1",
                                        Description = "Page One",
                                        Slug = "page-1",
                                        Title = "Page 1",
                                        PageOrder = 1,
                                        Fields =
                                        [
                                            new() { FieldId = "B1", Order = 1, Type = "string", Label = new Label{ Value = "start-year" } },
                                            new() { FieldId = "B2", Order = 2, Type = "string", Label = new Label{ Value = "end-year" } },
                                            new() { FieldId = "B3", Order = 3, Type = "string", Label = new Label{ Value = "local-authority" } }
                                        ]
                                    }
                                ]
                            }
                        ]
                    }
                ]
            };
        }
    }
}
