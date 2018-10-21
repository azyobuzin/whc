#!/bin/sh
xvfb-run -als '-screen 0 640x394x24 -listen tcp -ac' /app/ashe/ashe -d 'C:\wagahigh' $@
