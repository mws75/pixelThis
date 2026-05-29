#!/bin/bash
export DOTNET_ROOT=/usr/local/share/dotnet
exec /usr/local/share/dotnet/dotnet run --project "$(dirname "$0")/src/PixelThis/PixelThis.csproj"
