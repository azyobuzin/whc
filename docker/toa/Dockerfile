FROM wagahigh

# ログ出力のためにタイムゾーンを設定
RUN wine reg add 'HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\TimeZoneInformation' /v TimeZoneKeyName /t REG_SZ /d 'Tokyo Standard Time' /f

# Mono が未実装関数を呼び出して躓くので MS 製のものを使っていく
RUN winetricks vcrun2015 && winetricks win7

RUN wget -O /root/.wine/drive_c/mono.msi https://download.mono-project.com/archive/5.2.0/windows-installer/mono-5.2.0.215-gtksharp-2.12.45-win32-0.msi && \
    wine msiexec /i 'C:\mono.msi' /quiet && \
    rm /root/.wine/drive_c/mono.msi

ADD bin /root/.wine/drive_c/toa/
ADD run_toa.sh /

EXPOSE 51203
CMD sh /run_toa.sh
