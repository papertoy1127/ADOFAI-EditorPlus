dotnet "C:\Program Files\dotnet\sdk\6.0.200\MSBuild.dll" /p:Configuration=Release
mkdir Release
cd ./Release
mkdir EditorPlus
cp ../EditorPlus/bin/Release/EditorPlus.dll ./EditorPlus/EditorPlus.dll

cp ../dlls/*.dll ./EditorPlus/
cp ../Info.json ./EditorPlus/Info.json
tar -acf EditorPlus-Release.zip EditorPlus
mv EditorPlus-Release.zip ../
cd ../
rm -rf Release
pause
