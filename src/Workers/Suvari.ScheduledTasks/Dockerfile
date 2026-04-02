FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["src/Workers/Suvari.ScheduledTasks/Suvari.ScheduledTasks.csproj", "Workers/Suvari.ScheduledTasks/"]
RUN dotnet restore "Workers/Suvari.ScheduledTasks/Suvari.ScheduledTasks.csproj"

COPY ["src/Workers/Suvari.ScheduledTasks/", "Workers/Suvari.ScheduledTasks/"]

WORKDIR "/src/Workers/Suvari.ScheduledTasks"
RUN dotnet publish "Suvari.ScheduledTasks.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Suvari.ScheduledTasks.dll"]
