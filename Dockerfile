FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY DiscordMusicBot.slnx .
COPY Directory.Build.props .
COPY DiscordMusicBot.App/DiscordMusicBot.App.csproj DiscordMusicBot.App/
COPY DiscordMusicBot.Core/DiscordMusicBot.Core.csproj DiscordMusicBot.Core/
COPY DiscordMusicBot.Domain/DiscordMusicBot.Domain.csproj DiscordMusicBot.Domain/

RUN dotnet restore DiscordMusicBot.slnx

COPY . .
RUN dotnet publish DiscordMusicBot.App/DiscordMusicBot.App.csproj \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

RUN apt-get update && \
    apt-get install -y --no-install-recommends ffmpeg libopus0 libsodium23 && \
    rm -rf /var/lib/apt/lists/* && \
    mkdir -p /app/runtimes/linux-x64/native && \
    ln -s /usr/lib/x86_64-linux-gnu/libopus.so.0 /app/runtimes/linux-x64/native/libopus.so && \
    ln -s /usr/lib/x86_64-linux-gnu/libsodium.so.23 /app/runtimes/linux-x64/native/libsodium.so

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "DiscordMusicBot.App.dll"]
