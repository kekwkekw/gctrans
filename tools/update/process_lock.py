"""Small nonblocking OS lock whose ownership ends with the process.

The stable lock file is never unlinked: deleting a live locked inode can admit
another writer on POSIX. A leftover empty file has no ownership and is reusable.
"""
from __future__ import annotations

import json
import os
from pathlib import Path


class ProcessFileLock:
    def __init__(self, path: Path, busy_code="SOURCE_STATE_WRITER_BUSY"):
        self.path = Path(path)
        self.busy_code = busy_code
        self.stream = None

    def __enter__(self):
        self.path.parent.mkdir(parents=True, exist_ok=True)
        if any(p.is_symlink() or (hasattr(p, "is_junction") and p.is_junction())
               for p in (self.path, *self.path.parents)):
            raise ValueError("WRITER_LOCK_REPARSE_PATH")
        stream = self.path.open("a+b", buffering=0)
        try:
            stream.seek(0)
            if os.name == "nt":
                import msvcrt
                msvcrt.locking(stream.fileno(), msvcrt.LK_NBLCK, 1)
            else:
                import fcntl
                fcntl.flock(stream.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
        except OSError:
            stream.close()
            raise ValueError(self.busy_code) from None
        self.stream = stream
        # Informational only. Liveness comes from the kernel lock, never this PID.
        try:
            stream.seek(0)
            stream.truncate()
            stream.write(json.dumps({"pid": os.getpid(), "ownership": "OS_PROCESS_LOCK"}).encode())
        except BaseException:
            self.__exit__()
            raise
        return self

    def __exit__(self, *ignored):
        if self.stream is not None:
            stream = self.stream
            self.stream = None
            try:
                stream.seek(0)
                if os.name == "nt":
                    import msvcrt
                    msvcrt.locking(stream.fileno(), msvcrt.LK_UNLCK, 1)
                else:
                    import fcntl
                    fcntl.flock(stream.fileno(), fcntl.LOCK_UN)
            finally:
                stream.close()
