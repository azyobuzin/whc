FROM wagahigh

COPY toa.exe /home/wagahigh/.wine/drive_c/
COPY run_toa.sh /

USER root
RUN chown wagahigh:wagahigh /home/wagahigh/.wine/drive_c/toa.exe && \
    chmod a+rx /run_toa.sh
USER wagahigh

EXPOSE 51203
CMD /run_toa.sh
