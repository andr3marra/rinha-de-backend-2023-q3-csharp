FROM mcr.microsoft.com/dotnet/nightly/sdk:8.0-preview AS build-env

WORKDIR /app

# Copy everything
COPY ./src ./
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/nightly/aspnet:8.0-preview
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "rinha-de-backend-2023-q3-csharp.dll"]