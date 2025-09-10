#!/bin/zsh

# build - Wrapper script for the build tool
# Usage: ./build [command] [arguments]

set -e

PROJECT_PATH="/var/projects/carboncore"
BUILD_PROJECT="$PROJECT_PATH/build/build.csproj"
BUILD_OUTPUT="$PROJECT_PATH/build/bin/Debug/build.dll"

# Ensure we're in the project directory
pushd "$PROJECT_PATH"

# Check if the build DLL exists and rebuild only if it doesn't
if [ ! -f "$BUILD_OUTPUT" ]; then
    echo "Building the build tool..."
    dotnet build "$BUILD_PROJECT" --configuration Debug --no-restore
fi
popd

# Run the build tool with all arguments passed to this script
dotnet "$BUILD_OUTPUT" "$@"