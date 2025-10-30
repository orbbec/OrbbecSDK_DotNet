#!/bin/bash

set -e

SOLUTION_FILE="OrbbecSharp.sln"
CONFIGURATION="Release"
BASE_OUTPUT_DIR="$(pwd)/bin"

if [ $# -ge 1 ]; then
    CONFIGURATION=$1
fi

if [ $# -ge 2 ]; then
    BASE_OUTPUT_DIR=$(realpath "$2")
fi

OUTPUT_DIR="$BASE_OUTPUT_DIR/$CONFIGURATION"

echo "======================================="
echo " Building Solution"
echo "---------------------------------------"
echo " Solution:      $SOLUTION_FILE"
echo " Configuration: $CONFIGURATION"
echo " Output Dir:    $OUTPUT_DIR"
echo "======================================="

# ========= build =========
dotnet clean "$SOLUTION_FILE" -c "$CONFIGURATION" > /dev/null
dotnet build "$SOLUTION_FILE" -c "$CONFIGURATION" --output "$OUTPUT_DIR"

echo ""
echo "✅ Build completed successfully."
echo "📁 Output files are located in:"
echo "   $OUTPUT_DIR"
echo "======================================="