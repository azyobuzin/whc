FROM ubuntu:16.04

RUN dpkg --add-architecture i386 && \
    apt-get update -y && \
    apt-get install -y wget apt-utils apt-transport-https software-properties-common && \
    wget -nc https://dl.winehq.org/wine-builds/Release.key && \
    apt-key add Release.key && \
    rm Release.key && \
    apt-add-repository -y https://dl.winehq.org/wine-builds/ubuntu/ && \
    apt-get update -y && \
    apt-get install -y locales winehq-devel winetricks xvfb vnc4server xdotool && \
    rm -rf /var/lib/apt/lists/*

RUN locale-gen ja_JP.UTF-8
ENV LANG="ja_JP.UTF-8" \
    WINEDLLOVERRIDES="mshtml=d"

RUN wget -P /usr/share/wine/mono http://dl.winehq.org/wine/wine-mono/4.7.1/wine-mono-4.7.1.msi
RUN winetricks wenquanyi

ADD wagahigh.tar.xz /root/.wine/drive_c/wagahigh/
ADD run_wagahigh.sh /

EXPOSE 5900 6000
CMD sh /run_wagahigh.sh
