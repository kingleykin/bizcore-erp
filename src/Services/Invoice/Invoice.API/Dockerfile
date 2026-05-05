FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Services/Invoice/Invoice.API/Invoice.API.csproj", "src/Services/Invoice/Invoice.API/"]
COPY ["src/BuildingBlocks/Bizcore.BuildingBlocks/Bizcore.BuildingBlocks.csproj", "src/BuildingBlocks/Bizcore.BuildingBlocks/"]
RUN dotnet restore "./src/Services/Invoice/Invoice.API/Invoice.API.csproj"
COPY . .
WORKDIR "/src/src/Services/Invoice/Invoice.API"
RUN dotnet build "./Invoice.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Invoice.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Invoice.API.dll"]
