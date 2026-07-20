FROM alpine:3.21
RUN apk add --no-cache dovecot dovecot-lmtpd && mkdir -p /var/mail /run/dovecot
COPY dovecot.conf /etc/dovecot/dovecot.conf
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh
EXPOSE 143
ENTRYPOINT ["/entrypoint.sh"]
