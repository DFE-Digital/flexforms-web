# Set the major version of dotnet
ARG DOTNET_VERSION=10.0

# Stage 1 - Build the app using the dotnet SDK
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-azurelinux3.0 AS build
WORKDIR /build
ARG APP_VERSION=0.0.0-local

# Copy the solution file and source code
COPY ./GovUK.Dfe.FlexForms.Web.sln ./
COPY ./src/ ./src/

# Mount GitHub Token as a Docker secret, add NuGet source, and build the solution
RUN --mount=type=secret,id=github_token \
    --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore GovUK.Dfe.FlexForms.Web.sln && \
    dotnet build GovUK.Dfe.FlexForms.Web.sln --no-restore -c Release -p:Version=${APP_VERSION} -p:InformationalVersion=${APP_VERSION} && \
    dotnet publish GovUK.Dfe.FlexForms.Web.sln --no-build -o /app

# Stage 2 - Build a runtime environment
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-azurelinux3.0 AS final
WORKDIR /app
LABEL org.opencontainers.image.source="https://github.com/DFE-Digital/flexforms-web"
LABEL org.opencontainers.image.description="Flexforms - Web"

COPY --from=build /app /app
COPY ./script/web-docker-entrypoint.sh /app/docker-entrypoint.sh
RUN chmod +x ./docker-entrypoint.sh

USER $APP_UID
