FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["src/Workers/Devoplus.JobForge/Devoplus.JobForge.csproj", "Workers/Devoplus.JobForge/"]
RUN dotnet restore "Workers/Devoplus.JobForge/Devoplus.JobForge.csproj"

COPY ["src/Workers/Devoplus.JobForge/", "Workers/Devoplus.JobForge/"]

WORKDIR "/src/Workers/Devoplus.JobForge"
RUN dotnet publish "Devoplus.JobForge.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Devoplus.JobForge.dll"]
