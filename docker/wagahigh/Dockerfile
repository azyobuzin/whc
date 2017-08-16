FROM ubuntu:17.04

RUN useradd -m wagahigh

ADD wagahigh.tar.xz /home/wagahigh/.wine/drive_c/wagahigh/
RUN chown -R wagahigh:wagahigh /home/wagahigh

# apt-get update がクソ遅いので日本サーバーに変更
RUN sed -i 's|//archive\.ubuntu\.com|//jp\.archive\.ubuntu\.com|g' /etc/apt/sources.list

RUN dpkg --add-architecture i386 && \
    apt-get update -y && \
    apt-get install -y wget apt-transport-https software-properties-common && \
    wget -O - https://dl.winehq.org/wine-builds/Release.key | apt-key add - && \
    apt-add-repository -y https://dl.winehq.org/wine-builds/ubuntu/ && \
    apt-get update -y && \
    apt-get install -y locales make winehq-devel xvfb vnc4server xdotool && \
    rm -rf /var/lib/apt/lists/*

RUN locale-gen ja_JP.UTF-8

# 各種環境変数
# WINEDLLOVERRIDES: Wine の初回起動時に Mono と Gecko を入れろダイアログを表示させない
# W_OPT_UNATTENDED: winetricks に GUI なしでインストールしてもらう
ENV LANG="ja_JP.UTF-8" \
    WINEARCH=win32 \
    WINEDLLOVERRIDES="mscoree=d;mshtml=d" \
    W_OPT_UNATTENDED=1

# パッケージリポジトリの winetricks は古いので直接ダウンロード
RUN wget -O - https://github.com/Winetricks/winetricks/archive/20170614.tar.gz | tar -xzf - && \
    make -C winetricks-20170614 install && \
    rm -rf winetricks-20170614

COPY run_wagahigh.sh /
RUN chmod a+rx /run_wagahigh.sh

# Wine は通常ユーザーで実行する
USER wagahigh
WORKDIR /home/wagahigh

RUN winetricks wenquanyi

EXPOSE 5900 6000
CMD /run_wagahigh.sh
