FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY DiscordMusicBot.slnx .
COPY Directory.Build.props .
COPY DiscordMusicBot.App/DiscordMusicBot.App.csproj DiscordMusicBot.App/
COPY DiscordMusicBot.Core/DiscordMusicBot.Core.csproj DiscordMusicBot.Core/
COPY DiscordMusicBot.Domain/DiscordMusicBot.Domain.csproj DiscordMusicBot.Domain/
COPY DiscordMusicBot.DataAccess/DiscordMusicBot.DataAccess.csproj DiscordMusicBot.DataAccess/

RUN dotnet restore DiscordMusicBot.slnx

COPY . .
RUN dotnet publish DiscordMusicBot.App/DiscordMusicBot.App.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

RUN apt-get update && \
    apt-get install -y --no-install-recommends ffmpeg && \
    rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "DiscordMusicBot.App.dll"]
