FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
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
    -r linux-musl-x64 \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS runtime
WORKDIR /app

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

RUN apk add --no-cache icu-libs ffmpeg opus libsodium

COPY --from=build /app/publish .

RUN ln -s /usr/lib/libopus.so.0 libopus.so && \
    ln -s /usr/lib/libsodium.so.26 libsodium.so

ENTRYPOINT ["dotnet", "DiscordMusicBot.App.dll"]
