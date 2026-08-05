using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Web.Services;
using Moq;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Services
{
    public class ImportedApplicationServiceTest
    {
        private const string ApplicationReference = "TEST-APPLICATION-REFERENCE";
        private static readonly Guid TemplateId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        private readonly Mock<IApplicationsClient> mockClient;
        private readonly ImportedApplicationService service;

        public ImportedApplicationServiceTest()
        {
            mockClient = new Mock<IApplicationsClient>();
            service = new ImportedApplicationService(mockClient.Object);
        }

        [Fact]
        public async Task SaveApplication()
        {
            // TODO can frontend put multiple API calls in a transaction? NO - need new endpoint

            ApplicationDto createdApplication = new()
            {
                ApplicationId = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                ApplicationReference = ApplicationReference
            };
            mockClient.Setup(c => c.CreateApplicationAsync(It.IsAny<CreateApplicationRequest>()))
                .Callback<CreateApplicationRequest, CancellationToken>((request, cancellationToken) =>
                {
                    Assert.Equal(TemplateId, request.TemplateId);
                    Assert.Equal("{}", request.InitialResponseBody); // TODO check initial response body
                })
                .ReturnsAsync(createdApplication);

            ApplicationResponseDto applicationResponse = new(
                Guid.Parse("00000000-0000-0000-0000-000000000002"),
                createdApplication.ApplicationReference,
                createdApplication.ApplicationId,
                "{}",
                DateTime.UtcNow,
                Guid.Parse("00000000-0000-0000-0000-000000000003")
            );
            mockClient.Setup(c => c.AddApplicationResponseAsync(It.IsAny<Guid>(), It.IsAny<AddApplicationResponseRequest>()))
                .Callback<Guid, AddApplicationResponseRequest, CancellationToken>((applicationId, request, cancellationToken) =>
                {
                    Assert.Equal(createdApplication.ApplicationId, applicationId);
                    Assert.Equal("{}", request.ResponseBody); // TODO check response body
                })
                .ReturnsAsync(applicationResponse);

            ApplicationDto submittedApplication = new()
            {
                ApplicationId = createdApplication.ApplicationId
            };
            mockClient.Setup(c => c.SubmitApplicationAsync(createdApplication.ApplicationId))
                .ReturnsAsync(submittedApplication);

            Dictionary<string, object> data = new()
            {
                { "B1", "2026" },
                { "B2", "2027" },
                { "B3", "LA1" }
            };

            bool isSaved = await service.SaveApplicationAsync(ApplicationReference, data);

            mockClient.VerifyAll();
            Assert.True(isSaved);
        }
    }
}
