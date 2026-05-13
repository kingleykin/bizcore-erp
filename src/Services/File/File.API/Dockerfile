FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Services/File/File.API/File.API.csproj", "src/Services/File/File.API/"]
COPY ["src/BuildingBlocks/Bizcore.BuildingBlocks/Bizcore.BuildingBlocks.csproj", "src/BuildingBlocks/Bizcore.BuildingBlocks/"]
COPY ["src/BuildingBlocks/Bizcore.BuildingBlocks.Storage/Bizcore.BuildingBlocks.Storage.csproj", "src/BuildingBlocks/Bizcore.BuildingBlocks.Storage/"]
RUN dotnet restore "./src/Services/File/File.API/File.API.csproj"
COPY . .
WORKDIR "/src/src/Services/File/File.API"
RUN dotnet build "./File.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./File.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "File.API.dll"]
