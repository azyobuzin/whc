FROM wagahigh

# Mono が未実装関数を呼び出して躓くので MS 製 VC++ ランタイムを使っていく
RUN xvfb-run -a winetricks vcrun2015 win7

RUN wget -O ~/.wine/drive_c/mono.msi https://download.mono-project.com/archive/5.2.0/windows-installer/mono-5.2.0.215-gtksharp-2.12.45-win32-0.msi && \
    wine msiexec /i 'C:\mono.msi' /quiet && \
    rm ~/.wine/drive_c/mono.msi

# ログ出力のためにタイムゾーンを設定
RUN wine reg add 'HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\TimeZoneInformation' /v TimeZoneKeyName /t REG_SZ /d 'Tokyo Standard Time' /f

COPY bin /home/wagahigh/.wine/drive_c/toa/
COPY run_toa.sh /

USER root
RUN chown -R wagahigh:wagahigh /home/wagahigh/.wine/drive_c/toa && \
    chmod a+rx /run_toa.sh
USER wagahigh

EXPOSE 51203
CMD /run_toa.sh
