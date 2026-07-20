#!/bin/sh
set -eu
addgroup -g 5000 vmail 2>/dev/null || true
adduser -D -H -u 5000 -G vmail vmail 2>/dev/null || true
HASH="$(doveadm pw -s SHA512-CRYPT -p "$MAIL_PASSWORD")"
printf '%s:%s\n' "$MAIL_USER" "$HASH" > /etc/dovecot/users
chown -R vmail:vmail /var/mail
exec dovecot -F
