# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081
EXPOSE 5001


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/GovUK.Dfe.FlexForms.Web/GovUK.Dfe.FlexForms.Web.csproj", "src/GovUK.Dfe.FlexForms.Web/"]
COPY ["src/GovUK.Dfe.FlexForms.Application/GovUK.Dfe.FlexForms.Application.csproj", "src/GovUK.Dfe.FlexForms.Application/"]
COPY ["src/GovUK.Dfe.FlexForms.Domain/GovUK.Dfe.FlexForms.Domain.csproj", "src/GovUK.Dfe.FlexForms.Domain/"]
COPY ["src/GovUK.Dfe.FlexForms.Infrastructure/GovUK.Dfe.FlexForms.Infrastructure.csproj", "src/GovUK.Dfe.FlexForms.Infrastructure/"]
RUN dotnet restore "./src/GovUK.Dfe.FlexForms.Web/GovUK.Dfe.FlexForms.Web.csproj"
COPY . .
WORKDIR "/src/src/GovUK.Dfe.FlexForms.Web"
RUN dotnet build "./GovUK.Dfe.FlexForms.Web.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./GovUK.Dfe.FlexForms.Web.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GovUK.Dfe.FlexForms.Web.dll"]