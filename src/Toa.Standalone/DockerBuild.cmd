dotnet publish -c Release
rmdir "..\..\docker\toa\bin" /s /q
xcopy "bin\Release\netcoreapp2.0\publish" "..\..\docker\toa\bin\" /s
docker build -t toa "..\..\docker\toa"
