# Use the official .NET 9.0 SDK image to build and publish the app
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5144

RUN apt-get update && apt-get install -y curl

# Use the official .NET SDK to build the project
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["AuthService.csproj", "./"]
RUN dotnet restore "./AuthService.csproj"

# Copy everything and build the app
COPY . .
RUN dotnet publish "AuthService.csproj" -c Release -o /app/publish

# Final runtime image
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AuthService.dll"]