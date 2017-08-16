#!/bin/bash

xvfb-run -as '-screen 0 640x394x24' wine 'C:\Program Files\Mono\bin\mono.exe' 'C:\toa\Toa.exe' -d 'C:\wagahigh'
