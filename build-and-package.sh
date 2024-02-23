#!/bin/bash
rm -rf bin/Release
dotnet publish -c Release -r linux-x64 --self-contained
dotnet publish -c Release -r win-x64 --self-contained
mv bin/Release/net6.0/linux-x64 bin/Release/net6.0/linux-x64-contained
mv bin/Release/net6.0/win-x64 bin/Release/net6.0/win-x64-contained
dotnet publish -c Release -r linux-x64 --no-self-contained
dotnet publish -c Release -r win-x64 --no-self-contained

cd bin/Release/net6.0/linux-x64/publish
7z a ../../FileDiff-linux64.7z * -mx9
cd ../../
cd win-x64/publish
7z a ../../FileDiff-win64.7z * -mx9
cd ../../
cd linux-x64-contained/publish
7z a ../../FileDiff-linux64-contained.7z * -mx9
cd ../../
cd win-x64-contained/publish
7z a ../../FileDiff-win64-contained.7z * -mx9
cd ../../
rm -rf linux-x64 win-x64 win-x64-contained linux-x64-contained
echo Complete
