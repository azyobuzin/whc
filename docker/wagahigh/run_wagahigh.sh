#!/bin/bash

Xvfb :0 -screen 0 640x394x24 &
x11vnc -display WAIT:0 -shared -forever -q &
sleep 0.5

cd ~/.wine/drive_c/wagahigh
DISPLAY=:0 wine 'ワガママハイスペック.exe'
