#!/bin/zsh

# build - Wrapper script for the build tool
# Usage: ./build [command] [arguments]

set -e

# Get the directory of this script, which is assumed to be the project root.
PROJECT_PATH="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
PROJECT_PATH="$PROJECT_PATH/build"
BUILD_PROJECT="$PROJECT_PATH/BuildTools.csproj"
BUILD_OUTPUT="$PROJECT_PATH/bin/Debug/net9.0/BuildTools.dll"

# Ensure we're in the project directory
pushd "$PROJECT_PATH" > /dev/null

# Check if the build DLL exists or if any .cs files are newer
if [ ! -f "$BUILD_OUTPUT" ] || [ -n "$(find . -name '*.cs' -newer "$BUILD_OUTPUT")" ]; then
    echo "Building the build tool..."
    dotnet build "$BUILD_PROJECT" --configuration Debug --no-restore
fi

popd > /dev/null

# Run the build tool with all arguments passed to this script
dotnet "$BUILD_OUTPUT" "$@"
