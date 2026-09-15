#!/usr/bin/env python3
"""Compare privacy-reviewed Rhino minimal-host strace directories.

The comparison intentionally focuses on file lookup/access failures and socket
connect calls. It emits normalized operation/path keys rather than complete raw
strace lines, reducing (but not eliminating) disclosure risk.
"""

from __future__ import annotations

import argparse
import collections
import glob
import json
import re
import sys
from dataclasses import dataclass
from pathlib import Path

CALL_RE = re.compile(r"^(?:\[pid\s+\d+\]\s+)?(?P<call>[a-zA-Z0-9_]+)\((?P<args>.*)\)\s+=\s+(?P<result>.+)$")
QUOTED_RE = re.compile(r'"(?:\\.|[^"\\])*"')
ERRNO_RE = re.compile(r"^-1\s+([A-Z0-9_]+)\b")
PID_PATH_RE = re.compile(r"/proc/\d+(?=/|$)")
CLR_SHM_RE = re.compile(r"/dev/shm/(?:sem\.)?clr[a-zA-Z0-9]+")
GUID_RE = re.compile(r"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b")

FILE_CALLS = {"openat", "access", "statx", "readlink"}
RELEVANT_ERRNOS = {"ENOENT", "EACCES", "EPERM"}


@dataclass(frozen=True)
class Event:
    operation: str
    outcome: str
    target: str


def parse_mapping(value: str) -> tuple[str, str]:
    if "=" not in value:
        raise argparse.ArgumentTypeError("mapping must be OLD=REPLACEMENT")
    old, replacement = value.split("=", 1)
    if not old:
        raise argparse.ArgumentTypeError("mapping OLD value cannot be empty")
    return old.rstrip("/"), replacement.rstrip("/")


def decode_quoted(value: str) -> str:
    try:
        return json.loads(value)
    except json.JSONDecodeError:
        return value[1:-1]


def normalize(value: str, mappings: list[tuple[str, str]]) -> str:
    for old, replacement in sorted(mappings, key=lambda item: len(item[0]), reverse=True):
        if value == old or value.startswith(old + "/"):
            value = replacement + value[len(old):]
            break
    value = PID_PATH_RE.sub("/proc/<PID>", value)
    value = CLR_SHM_RE.sub("/dev/shm/<CLR_SHM>", value)
    value = GUID_RE.sub("<GUID>", value)
    return value


def trace_files(directory: Path) -> list[Path]:
    candidates = []
    for name in glob.glob(str(directory / "trace*")):
        path = Path(name)
        if path.is_file() and not path.name.endswith((".log", ".txt")):
            candidates.append(path)
    return sorted(candidates)


def collect(directory: Path, mappings: list[tuple[str, str]]) -> tuple[collections.Counter[Event], int, int]:
    files = trace_files(directory)
    if not files:
        raise ValueError(f"no trace* files found in {directory}")

    events: collections.Counter[Event] = collections.Counter()
    parsed_calls = 0
    ignored_lines = 0

    for path in files:
        with path.open("r", encoding="utf-8", errors="replace") as stream:
            for raw_line in stream:
                line = raw_line.strip()
                match = CALL_RE.match(line)
                if not match:
                    ignored_lines += 1
                    continue
                parsed_calls += 1
                call = match.group("call")
                args = match.group("args")
                result = match.group("result")
                errno_match = ERRNO_RE.match(result)
                errno = errno_match.group(1) if errno_match else "SUCCESS"

                if call in FILE_CALLS:
                    if errno not in RELEVANT_ERRNOS:
                        continue
                    quoted = QUOTED_RE.findall(args)
                    target = decode_quoted(quoted[0]) if quoted else "<NO_QUOTED_PATH>"
                    events[Event(call, errno, normalize(target, mappings))] += 1
                elif call == "connect":
                    family_match = re.search(r"sa_family=(AF_[A-Z0-9_]+)", args)
                    family = family_match.group(1) if family_match else "AF_UNKNOWN"
                    quoted = QUOTED_RE.findall(args)
                    target = decode_quoted(quoted[-1]) if quoted else "<NO_SOCKET_PATH>"
                    outcome = errno if errno != "SUCCESS" else "SUCCESS"
                    events[Event(call, outcome, f"{family}:{normalize(target, mappings)}")] += 1

    return events, len(files), ignored_lines


def print_table(title: str, counter: collections.Counter[Event], limit: int) -> None:
    print(f"\n## {title}\n")
    if not counter:
        print("None.")
        return
    print("| Count | Operation | Outcome | Normalized target |")
    print("| ---: | --- | --- | --- |")
    rows = sorted(counter.items(), key=lambda item: (item[0].operation, item[0].outcome, item[0].target))
    for event, count in rows[:limit]:
        target = event.target.replace("|", "\\|").replace("`", "\\`")
        print(f"| {count} | `{event.operation}` | `{event.outcome}` | `{target}` |")
    if len(rows) > limit:
        print(f"\n_Truncated: showing {limit} of {len(rows)} distinct rows._")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("left", type=Path, help="first directory containing trace* files")
    parser.add_argument("right", type=Path, help="second directory containing trace* files")
    parser.add_argument("--left-label", default="left")
    parser.add_argument("--right-label", default="right")
    parser.add_argument(
        "--map",
        action="append",
        default=[],
        type=parse_mapping,
        metavar="OLD=REPLACEMENT",
        help="normalize a path prefix in both inputs; repeat as needed",
    )
    parser.add_argument("--limit", type=int, default=250, help="maximum rows per difference table")
    args = parser.parse_args()

    if args.limit < 1:
        parser.error("--limit must be at least 1")

    try:
        left, left_files, left_ignored = collect(args.left, args.map)
        right, right_files, right_ignored = collect(args.right, args.map)
    except (OSError, ValueError) as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2

    left_only = left - right
    right_only = right - left
    common = left & right

    print("# Rhino strace comparison")
    print("\nReview this normalized output before sharing; it can still contain local paths or names.\n")
    print("| Input | Trace files | Selected event instances | Distinct selected events | Unparsed lines |")
    print("| --- | ---: | ---: | ---: | ---: |")
    print(f"| {args.left_label} | {left_files} | {sum(left.values())} | {len(left)} | {left_ignored} |")
    print(f"| {args.right_label} | {right_files} | {sum(right.values())} | {len(right)} | {right_ignored} |")
    print(f"\nCommon selected event instances: **{sum(common.values())}**")
    print_table(f"Only or more frequent in {args.left_label}", left_only, args.limit)
    print_table(f"Only or more frequent in {args.right_label}", right_only, args.limit)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
