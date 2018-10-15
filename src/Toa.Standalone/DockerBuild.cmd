dotnet publish -c Release -r ubuntu.18.04-x64
rmdir "..\..\docker\toa\bin" /s /q
xcopy "bin\Release\netcoreapp2.1\ubuntu.18.04-x64\publish" "..\..\docker\toa\bin\" /s
docker build -t toa "..\..\docker\toa"
