FROM wagahigh

USER root

RUN wget -q https://packages.microsoft.com/config/ubuntu/18.04/packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb && \
    rm packages-microsoft-prod.deb && \
    apt-get update -y && \
    apt-get install -y dotnet-runtime-2.1 && \
    rm -rf /var/lib/apt/lists/*

USER wagahigh

COPY bin /app/toa

CMD xvfb-run -als '-screen 0 640x394x24 -listen tcp -ac' dotnet /app/toa/toa.dll -d 'C:\wagahigh'
