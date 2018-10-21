#!/bin/sh
xvfb-run -als '-screen 0 640x394x24 -listen tcp -ac' /app/toa/toa -d 'C:\wagahigh' $@
