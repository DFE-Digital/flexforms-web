# Set the major version of dotnet
ARG DOTNET_VERSION=10.0
# Application to build (Transfers, Lsrp, etc.) - determines which configuration folder to use
ARG APPLICATION=Transfers

# Stage 1 - Build the app using the dotnet SDK
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-azurelinux3.0 AS build
ARG APPLICATION
WORKDIR /build

# Copy the solution file and source code
COPY ./GovUK.Dfe.FlexForms.Web.sln ./
COPY ./src/ ./src/

# Mount GitHub Token as a Docker secret, add NuGet source, and build the solution
RUN --mount=type=secret,id=github_token \
    --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore GovUK.Dfe.FlexForms.Web.sln && \
    dotnet build GovUK.Dfe.FlexForms.Web.sln --no-restore -c Release && \
    dotnet publish GovUK.Dfe.FlexForms.Web.sln --no-build -o /app

# Copy application-specific configuration to the published output
# The configurations folder is already inside the Web project and gets published
# But we ensure only the specific application's config is in the final image
RUN mkdir -p /app/configurations/${APPLICATION} && \
    cp -f ./src/GovUK.Dfe.FlexForms.Web/configurations/${APPLICATION}/appsettings*.json /app/configurations/${APPLICATION}/

# Stage 2 - Build a runtime environment
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-azurelinux3.0 AS final
ARG APPLICATION
WORKDIR /app
LABEL org.opencontainers.image.source="https://github.com/DFE-Digital/external-applications-web"
LABEL org.opencontainers.image.description="External Applications - App"

# Set the application name environment variable for runtime configuration loading
ENV APPLICATION_NAME=${APPLICATION}

COPY --from=build /app /app
COPY ./script/web-docker-entrypoint.sh /app/docker-entrypoint.sh
RUN chmod +x ./docker-entrypoint.sh

USER $APP_UID
