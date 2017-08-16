#!/bin/bash

Xvfb :0 -screen 0 640x394x24 &
x0vncserver -display :0 -SecurityTypes None &

cd ~/.wine/drive_c/wagahigh
DISPLAY=:0 wine 'ワガママハイスペック.exe'
