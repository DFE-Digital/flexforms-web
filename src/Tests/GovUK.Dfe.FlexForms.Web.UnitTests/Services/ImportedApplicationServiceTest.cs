using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Web.Services;
using Moq;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Services
{
    public class ImportedApplicationServiceTest
    {
        private const string ApplicationReference = "TEST-APPLICATION-REFERENCE";
        private static readonly Guid TemplateId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        private readonly Mock<IApplicationsClient> mockClient;
        private readonly ImportedApplicationService service;
        private readonly JsonSerializerOptions jsonSerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
        private readonly ITestOutputHelper output;

        public ImportedApplicationServiceTest(ITestOutputHelper output)
        {
            mockClient = new Mock<IApplicationsClient>();
            service = new ImportedApplicationService(mockClient.Object);
            this.output = output;
        }

        [Fact]
        public async Task SaveApplication()
        {
            // TODO can frontend put multiple API calls in a transaction? NO - need new endpoint

            ApplicationDto createdApplication = new()
            {
                ApplicationId = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                ApplicationReference = ApplicationReference,
                Status = ApplicationStatus.Created
            };
            mockClient.Setup(c => c.CreateApplicationAsync(It.IsAny<CreateApplicationRequest>()))
                .Callback<CreateApplicationRequest, CancellationToken>((request, cancellationToken) =>
                {
                    Assert.Equal(TemplateId, request.TemplateId);
                    Assert.Equal("{}", request.InitialResponseBody);
                })
                .ReturnsAsync(createdApplication);

            var expectedData = new Dictionary<string, object>
            {
                { "start-year", new { Value = "2026", Completed = true } },
                { "end-year", new { Value = "2027", Completed = true } },
                { "local-authority", new { Value = "LA1", Completed = true } }
            };
            string expectedJson = JsonSerializer.Serialize(expectedData, jsonSerializerOptions);
            ApplicationResponseDto applicationResponse = new(
                Guid.Parse("00000000-0000-0000-0000-000000000002"),
                createdApplication.ApplicationReference,
                createdApplication.ApplicationId,
                string.Empty,
                DateTime.UtcNow,
                Guid.Parse("00000000-0000-0000-0000-000000000003")
            );
            mockClient.Setup(c => c.AddApplicationResponseAsync(It.IsAny<Guid>(), It.IsAny<AddApplicationResponseRequest>()))
                .Callback<Guid, AddApplicationResponseRequest, CancellationToken>((applicationId, request, cancellationToken) =>
                {
                    Assert.Equal(createdApplication.ApplicationId, applicationId);
                    byte[] data = Convert.FromBase64String(request.ResponseBody);
                    string responseJson = Encoding.UTF8.GetString(data);
                    output.WriteLine($"Response body: {Environment.NewLine}{responseJson}");
                    Assert.Equal(expectedJson, responseJson);
                })
                .ReturnsAsync(applicationResponse);

            ApplicationDto submittedApplication = new()
            {
                ApplicationId = createdApplication.ApplicationId, 
                ApplicationReference = createdApplication.ApplicationReference,
                Status = ApplicationStatus.Submitted
            };
            mockClient.Setup(c => c.SubmitApplicationAsync(createdApplication.ApplicationId))
                .ReturnsAsync(submittedApplication);

            Dictionary<string, object> data = new()
            {
                { "start-year", "2026" },
                { "end-year", "2027" },
                { "local-authority", "LA1" }
            };

            bool isSaved = await service.SaveApplicationAsync(ApplicationReference, data);

            mockClient.VerifyAll();
            Assert.True(isSaved);
        }
    }
}
