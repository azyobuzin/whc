FROM wagahigh

USER root
RUN wget -O - https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > /etc/apt/trusted.gpg.d/microsoft.gpg && \
    sh -c 'echo "deb [arch=amd64] https://packages.microsoft.com/repos/microsoft-ubuntu-zesty-prod zesty main" > /etc/apt/sources.list.d/dotnetdev.list' && \
    apt-get update -y && \
    apt-get install -y dotnet-runtime-2.0.0 && \
    rm -rf /var/lib/apt/lists/*

USER wagahigh

COPY bin /app/toa
COPY run_toa.sh /

CMD /run_toa.sh
