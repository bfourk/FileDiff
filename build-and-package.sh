#!/bin/bash

# Cleanup
rm -rf bin/Release
rm -rf packages

# Build projects
mkdir packages
dotnet publish -c Release -r linux-x64 --self-contained
dotnet publish -c Release -r win-x64 --self-contained
mv bin/Release/net8.0/linux-x64 bin/Release/net8.0/linux-x64-contained
mv bin/Release/net8.0/win-x64 bin/Release/net8.0/win-x64-contained
dotnet publish -c Release -r linux-x64 --no-self-contained
dotnet publish -c Release -r win-x64 --no-self-contained

# Compress projects into archives
cd bin/Release/net8.0/linux-x64/publish
rm *.pdb
7z a ../../../../../packages/FileDiff-linux64.7z * -mx9
cd ../../
cd win-x64/publish
rm *.pdb
7z a ../../../../../packages/FileDiff-win64.7z * -mx9
cd ../../
cd linux-x64-contained/publish
rm *.pdb
7z a ../../../../../packages/FileDiff-linux64-contained.7z * -mx9
cd ../../
cd win-x64-contained/publish
rm *.pdb
7z a ../../../../../packages/FileDiff-win64-contained.7z * -mx9
cd ../../../../..

# Cleanup
rm -rf bin/Release
rm -rf obj/Release
echo Complete