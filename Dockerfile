FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY DiscordMusicBot.slnx .
COPY Directory.Build.props .
COPY DiscordMusicBot.App/DiscordMusicBot.App.csproj DiscordMusicBot.App/
COPY DiscordMusicBot.Core/DiscordMusicBot.Core.csproj DiscordMusicBot.Core/
COPY DiscordMusicBot.Domain/DiscordMusicBot.Domain.csproj DiscordMusicBot.Domain/
COPY DiscordMusicBot.Infrastructure/DiscordMusicBot.Infrastructure.csproj DiscordMusicBot.Infrastructure/

RUN dotnet restore DiscordMusicBot.slnx

COPY . .
RUN dotnet publish DiscordMusicBot.App/DiscordMusicBot.App.csproj \
    -c Release \
    -r linux-x64 \
    -o /app/publish

FROM alpine:3.21 AS native-deps
RUN apk add --no-cache curl unzip tar xz
RUN curl -sL https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-linux64-gpl.tar.xz \
    | tar -xJ --strip-components=2 --wildcards '*/bin/ffmpeg'
RUN curl -sL -o /tmp/libdave.zip https://github.com/discord/libdave/releases/download/v1.1.1/cpp/libdave-Linux-X64-boringssl.zip \
    && unzip -q /tmp/libdave.zip lib/libdave.so -d /tmp/libdave \
    && mv /tmp/libdave/lib/libdave.so /libdave.so \
    && rm -rf /tmp/libdave /tmp/libdave.zip

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV DATABASE_PATH=/app/data/database.db

RUN apt-get update && apt-get install -y --no-install-recommends \
    libopus0 libsodium23 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=native-deps /ffmpeg /usr/local/bin/ffmpeg
COPY --from=native-deps /libdave.so .
COPY --from=build /app/publish .

RUN ln -s /usr/lib/x86_64-linux-gnu/libopus.so.0 libopus.so && \
    ln -s /usr/lib/x86_64-linux-gnu/libsodium.so.23 libsodium.so

VOLUME /app/data

ENTRYPOINT ["dotnet", "DiscordMusicBot.App.dll"]
