dotnet publish -c Release -r ubuntu.18.04-x64
rmdir "..\..\docker\ashe\bin" /s /q
xcopy "bin\Release\netcoreapp2.1\ubuntu.18.04-x64\publish" "..\..\docker\ashe\bin\" /s
docker build -t ashe "..\..\docker\ashe"
