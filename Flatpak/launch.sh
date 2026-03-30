#!/bin/bash
# Launch script for Retro Multi Tools running inside a Flatpak sandbox.
# Sets up required environment variables before executing the .NET application.

# Ensure the .NET runtime can find ICU libraries for globalization support.
# The freedesktop runtime includes ICU; this variable tells .NET where to look.
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Forward XDG_RUNTIME_DIR for Discord IPC socket access
# (mapped by Flatpak when --socket permissions are granted)

exec /app/lib/retromultitools/RetroMultiTools "$@"
