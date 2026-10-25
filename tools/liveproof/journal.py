"""Undo journal for the live-proof scripts.

Every change a proof makes to an instance is written here BEFORE it happens (the original of an
edited resource) or right after it (the id of a created one), and the file is saved after every
entry. A run that dies half-way is undone with `run.py restore`, which replays the journal in
reverse and shrinks it entry by entry, so restoring twice is safe.
"""
import json
import os

from client import ApiError


class Journal:
    def __init__(self, path=None):
        self.path = os.path.expanduser(path or os.environ.get("LIVEPROOF_STATE", "liveproof-state.json"))
        self.entries = []
        if os.path.exists(self.path):
            with open(self.path, encoding="utf-8") as f:
                self.entries = json.load(f)

    def _save(self):
        temp = self.path + ".tmp"
        # The file can hold full resources (client settings, notification URLs): keep it private.
        fd = os.open(temp, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
        with os.fdopen(fd, "w", encoding="utf-8") as f:
            json.dump(self.entries, f)
        os.replace(temp, self.path)

    def will_edit(self, path, original):
        """Call before editing: `path` is the PUT url, `original` the resource as it is now."""
        self.entries.append({"op": "put", "path": path, "body": original})
        self._save()

    def created(self, path):
        """Call right after creating: `path` is the DELETE url of the new resource."""
        self.entries.append({"op": "delete", "path": path})
        self._save()

    def restore(self, api, log=print):
        failures = 0
        while self.entries:
            entry = self.entries[-1]
            try:
                if entry["op"] == "put":
                    api.put(entry["path"], entry["body"])
                    log(f"restored   {entry['path']}")
                else:
                    api.delete(entry["path"])
                    log(f"removed    {entry['path']}")
            except ApiError as error:
                if entry["op"] == "delete" and error.status == 404:
                    log(f"gone       {entry['path']}")
                else:
                    # Keep the entry (and everything below it) so the next `restore` retries.
                    log(f"FAILED     {error}")
                    failures += 1
                    break
            self.entries.pop()
            self._save()
        return failures
