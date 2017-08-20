$ErrorActionPreference = "Stop"
cargo build
.\CopyBin.ps1
$imageId = docker build -qt toa '..\..\docker\toa'
docker run -it --rm $imageId
