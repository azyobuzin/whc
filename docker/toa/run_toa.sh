#!/bin/bash

Xvfb :0 -screen 0 640x394x24 -listen tcp -ac &
sleep 0.5

export DISPLAY=:0
wine start 'C:\wagahigh\ワガママハイスペック.exe'

sleep 5
dotnet /app/toa/toa.dll
