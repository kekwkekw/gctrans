"""Bounded HTTP helpers for the existing known source endpoints only."""
import json
import socket
import ssl
import time
from urllib.parse import urlsplit

ALLOWED_HOST = "cdn-r18.gc.dmmgames.com"
MAX_METADATA_BYTES = 16 * 1024 * 1024


def transport_failure_code(error):
    """Classify typed causes only; never emit signed URLs or exception text."""
    import httpx
    causes, seen = [error], set()
    for _ in range(8):
        if not causes:
            break
        cause = causes.pop(0)
        if id(cause) in seen:
            continue
        seen.add(id(cause))
        if isinstance(cause, (TimeoutError, httpx.TimeoutException)):
            return "TIMEOUT"
        if isinstance(cause, ssl.SSLError):
            return "TLS"
        if isinstance(cause, socket.gaierror):
            return "DNS"
        causes.extend(c for c in (getattr(cause, "__cause__", None), getattr(cause, "__context__", None)) if c is not None)
    return "TRANSPORT"


def fetch_bytes(client, url, *, params=None, attempts=3, max_bytes=16*1024*1024,
                cancelled=lambda: False, sleeper=time.sleep):
    parsed = urlsplit(url)
    if parsed.scheme != "https" or parsed.hostname != ALLOWED_HOST or parsed.port not in (None, 443) or parsed.username or parsed.password:
        raise ValueError("SOURCE_HOST_REJECTED")
    if type(attempts) is not int or not 1 <= attempts <= 3:
        raise ValueError("SOURCE_RETRY_LIMIT")
    for attempt in range(attempts):
        if cancelled():
            raise ValueError("SOURCE_CANCELLED")
        retry = False
        retry_reason = "TRANSPORT"
        try:
            with client.stream("GET", url, params=params, follow_redirects=False) as response:
                status = response.status_code
                if 300 <= status < 400:
                    raise ValueError("SOURCE_REDIRECT_REJECTED")
                if status == 429 or 500 <= status < 600:
                    retry = True
                    retry_reason = "RATE_LIMIT" if status == 429 else "SERVER_STATUS"
                elif status != 200:
                    raise ValueError("SOURCE_HTTP_STATUS_REJECTED")
                else:
                    length = response.headers.get("content-length")
                    if length and (not length.isdigit() or int(length) > max_bytes):
                        raise ValueError("SOURCE_HTTP_SIZE")
                    parts, size = [], 0
                    for chunk in response.iter_bytes():
                        if cancelled():
                            raise ValueError("SOURCE_CANCELLED")
                        size += len(chunk)
                        if size > max_bytes:
                            raise ValueError("SOURCE_HTTP_SIZE")
                        parts.append(chunk)
                    return b"".join(parts)
        except (TimeoutError, OSError) as error:
            retry = True
            retry_reason = transport_failure_code(error)
        except Exception as error:
            import httpx
            if isinstance(error, (httpx.TimeoutException, httpx.TransportError)):
                retry = True
                retry_reason = transport_failure_code(error)
            else:
                raise
        if not retry or attempt + 1 == attempts:
            raise ValueError("SOURCE_HTTP_RETRIES_EXHAUSTED_" + retry_reason)
        sleeper(min(2 * (attempt + 1), 4))
    raise ValueError("SOURCE_HTTP_RETRIES_EXHAUSTED")


def fetch_json(client, url, *, attempts=3, cancelled=lambda: False):
    raw = fetch_bytes(client, url, attempts=attempts, max_bytes=MAX_METADATA_BYTES, cancelled=cancelled)
    try:
        value = json.loads(raw)
    except (ValueError, UnicodeError):
        raise ValueError("SOURCE_METADATA_JSON") from None
    if not isinstance(value, dict):
        raise ValueError("SOURCE_METADATA_SCHEMA")
    return value
