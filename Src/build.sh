#!/bin/bash
set -e

DOTNET=$(command -v dotnet) || {
  echo 'dotnet' not found.
  echo you need to install dotnet-sdk-7.0
  echo Linux: see https://learn.microsoft.com/en-us/dotnet/core/install/linux
  echo MacOS: see https://learn.microsoft.com/en-us/dotnet/core/install/macos
  exit 1
}
$DOTNET build --configuration Release
