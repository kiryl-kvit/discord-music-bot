#!/usr/bin/env bash

LIBDAVE_PATH="./DiscordMusicBot.App/libdave.so"
LIBDAVE_URL="https://github.com/discord/libdave/releases/download/v1.1.1/cpp/libdave-Linux-X64-boringssl.zip"

if [ ! -f "$LIBDAVE_PATH" ]; then
    echo "libdave.so not found, downloading..."
    TMPDIR=$(mktemp -d)
    curl -sL "$LIBDAVE_URL" -o "$TMPDIR/libdave.zip"
    unzip -q "$TMPDIR/libdave.zip" lib/libdave.so -d "$TMPDIR"
    cp "$TMPDIR/lib/libdave.so" "$LIBDAVE_PATH"
    rm -rf "$TMPDIR"
    echo "libdave.so downloaded."
fi

dotnet run --project ./DiscordMusicBot.App/DiscordMusicBot.App.csproj
