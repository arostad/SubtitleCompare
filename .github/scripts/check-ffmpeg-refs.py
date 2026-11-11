#!/usr/bin/env python3
"""Check that the missing-FFmpeg dialog's URLs and winget packages still resolve.

Parses URLs and winget package ids/queries from the dialog source files so the
check stays in sync when the text changes. Network errors are retried once.
A 4xx or a missing package fails immediately.

This job is intentionally not a dependency of the Windows release job: a
transient outage of an outside site should turn this check red without
blocking a new Latest build.
"""

from __future__ import annotations

import json
import os
import re
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]

# User-facing FFmpeg install guidance lives in these files.
SOURCE_FILES = [
    Path("src/SubtitleCompare.App/FfmpegMissingWindow.xaml.cs"),
    Path("src/SubtitleCompare.App/FfmpegMissingWindow.xaml"),
    Path("src/SubtitleCompare.Core/Ffmpeg/FfmpegNotFoundException.cs"),
]

# Official downloads page used to verify the dialog's "they list" claim.
# Not taken from the dialog (which links to https://ffmpeg.org/), so this
# one URL is fixed on purpose.
FFMPEG_DOWNLOAD_PAGE = "https://ffmpeg.org/download.html"
GYAN_MARKER = "gyan.dev"

WINGET_PKGS_REPO = "microsoft/winget-pkgs"
USER_AGENT = "SubtitleCompare-CI/1.0 (+https://github.com/arostad/SubtitleCompare)"
TIMEOUT_SEC = 30
RETRY_SLEEP_SEC = 8

# xmlns="http://..." is a schema, not a link we show users.
XMLNS_RE = re.compile(r"\sxmlns(?::\w+)?=\"[^\"]*\"")
URL_RE = re.compile(r"https?://[^\s'\"<>)\\\]]+")
WINGET_ID_FLAG_RE = re.compile(r"--id(?:\s+|=)([A-Za-z0-9][A-Za-z0-9._-]+)")
WINGET_INSTALL_RE = re.compile(
    r"winget\s+install\s+(?:--id(?:\s+|=))?([A-Za-z0-9][A-Za-z0-9._-]+)",
    re.IGNORECASE,
)
# e.g. "the official Windows build they list (Gyan.FFmpeg)"
PAREN_PACKAGE_RE = re.compile(r"\(([A-Z][A-Za-z0-9]+(?:\.[A-Z][A-Za-z0-9]+)+)\)")
VERSION_DIR_RE = re.compile(r"^\d+(?:\.\d+)*$")
TRAILING_PUNCT_RE = re.compile(r"[.,;:]+$")


class CheckError(Exception):
    pass


def main() -> int:
    failures: list[str] = []
    sources = read_sources()
    urls = extract_urls(sources)
    package_ids, queries = extract_winget_refs(sources)

    print("Source files:")
    for path in SOURCE_FILES:
        print(f"  {path}")
    print(f"URLs: {', '.join(urls) if urls else '(none)'}")
    print(f"Winget package ids: {', '.join(package_ids) if package_ids else '(none)'}")
    print(f"Winget queries/monikers: {', '.join(queries) if queries else '(none)'}")

    if not urls:
        failures.append("No http(s) URLs found in the FFmpeg dialog source files.")
    if not package_ids and not queries:
        failures.append("No winget package ids or install queries found in the FFmpeg dialog source files.")

    for url in urls:
        try:
            check_url(url)
            print(f"OK  URL  {url}")
        except CheckError as exc:
            failures.append(str(exc))
            print(f"FAIL URL  {url}: {exc}")

    package_monikers: dict[str, set[str]] = {}
    for package_id in package_ids:
        try:
            monikers = check_winget_package(package_id)
            package_monikers[package_id] = monikers
            moniker_note = f" (monikers: {', '.join(sorted(monikers))})" if monikers else ""
            print(f"OK  winget id  {package_id}{moniker_note}")
        except CheckError as exc:
            failures.append(str(exc))
            print(f"FAIL winget id  {package_id}: {exc}")

    for query in queries:
        try:
            check_winget_query(query, package_monikers)
            print(f"OK  winget query  {query}")
        except CheckError as exc:
            failures.append(str(exc))
            print(f"FAIL winget query  {query}: {exc}")

    try:
        check_official_windows_build_listing()
        print(f"OK  {FFMPEG_DOWNLOAD_PAGE} still lists {GYAN_MARKER}")
    except CheckError as exc:
        failures.append(str(exc))
        print(f"FAIL official listing: {exc}")

    if failures:
        print("\nFFmpeg dialog reference check failed:")
        for item in failures:
            print(f"  - {item}")
        return 1

    print("\nAll FFmpeg dialog references still resolve.")
    return 0


def read_sources() -> dict[Path, str]:
    texts: dict[Path, str] = {}
    missing = [p for p in SOURCE_FILES if not (REPO_ROOT / p).is_file()]
    if missing:
        names = ", ".join(str(p) for p in missing)
        raise SystemExit(f"Missing expected FFmpeg dialog source file(s): {names}")
    for path in SOURCE_FILES:
        texts[path] = (REPO_ROOT / path).read_text(encoding="utf-8")
    return texts


def extract_urls(sources: dict[Path, str]) -> list[str]:
    found: list[str] = []
    seen: set[str] = set()
    for text in sources.values():
        cleaned = XMLNS_RE.sub("", text)
        for raw in URL_RE.findall(cleaned):
            url = TRAILING_PUNCT_RE.sub("", raw)
            if url not in seen:
                seen.add(url)
                found.append(url)
    return found


def extract_winget_refs(sources: dict[Path, str]) -> tuple[list[str], list[str]]:
    ids: list[str] = []
    queries: list[str] = []
    seen: set[str] = set()

    def add(value: str, bucket: list[str]) -> None:
        if value not in seen:
            seen.add(value)
            bucket.append(value)

    for text in sources.values():
        for match in WINGET_ID_FLAG_RE.findall(text):
            add(match, ids if "." in match else queries)
        for match in WINGET_INSTALL_RE.findall(text):
            add(match, ids if "." in match else queries)
        for match in PAREN_PACKAGE_RE.findall(text):
            add(match, ids)
    return ids, queries


def check_url(url: str) -> None:
    code, _, err = http_get(url)
    if err is not None:
        raise CheckError(f"{url} could not be reached after retry: {err}")
    if code is None or code < 200 or code >= 400:
        raise CheckError(f"{url} returned HTTP {code}")
    # 2xx and 3xx after following redirects are accepted.


def check_winget_package(package_id: str) -> set[str]:
    path = winget_manifest_dir(package_id)
    listing = github_json(f"https://api.github.com/repos/{WINGET_PKGS_REPO}/contents/{path}")
    if not isinstance(listing, list):
        raise CheckError(f"{package_id}: unexpected GitHub API response for {path}")

    version_dirs = [
        item["name"]
        for item in listing
        if item.get("type") == "dir" and VERSION_DIR_RE.match(item.get("name") or "")
    ]
    if not version_dirs:
        raise CheckError(
            f"{package_id}: {WINGET_PKGS_REPO}/{path} has no version manifests"
        )

    latest = max(version_dirs, key=version_key)
    files = github_json(
        f"https://api.github.com/repos/{WINGET_PKGS_REPO}/contents/{path}/{latest}"
    )
    if not isinstance(files, list):
        raise CheckError(f"{package_id}: unexpected GitHub API response for {path}/{latest}")

    yaml_files = [item for item in files if (item.get("name") or "").endswith((".yaml", ".yml"))]
    if not yaml_files:
        raise CheckError(f"{package_id}: version {latest} has no manifest YAML")

    monikers: set[str] = set()
    for item in yaml_files:
        raw_url = item.get("download_url")
        if not raw_url:
            continue
        code, body, err = http_get(raw_url, accept="application/octet-stream")
        if err is not None or not body or code is None or code >= 400:
            continue
        text = body.decode("utf-8", errors="replace")
        for line in text.splitlines():
            m = re.match(r"Moniker:\s*(\S+)", line)
            if m:
                monikers.add(m.group(1).strip().strip("\"'"))
    print(f"     {package_id} latest version in winget-pkgs: {latest}")
    return monikers


def check_winget_query(query: str, package_monikers: dict[str, set[str]]) -> None:
    lowered = query.lower()
    for package_id, monikers in package_monikers.items():
        if any(m.lower() == lowered for m in monikers):
            print(f"     {query} is a moniker of {package_id}")
            return
        # winget also matches PackageName / id suffix in some cases; moniker is
        # the documented reason `winget install ffmpeg` resolves.
    raise CheckError(
        f"`winget install {query}` is still mentioned, but none of the checked "
        f"packages declare Moniker: {query}"
    )


def check_official_windows_build_listing() -> None:
    code, body, err = http_get(FFMPEG_DOWNLOAD_PAGE)
    if err is not None:
        raise CheckError(
            f"{FFMPEG_DOWNLOAD_PAGE} could not be reached after retry: {err}"
        )
    if code is None or code < 200 or code >= 400:
        raise CheckError(f"{FFMPEG_DOWNLOAD_PAGE} returned HTTP {code}")
    text = (body or b"").decode("utf-8", errors="replace").lower()
    if GYAN_MARKER.lower() not in text:
        raise CheckError(
            f"{FFMPEG_DOWNLOAD_PAGE} no longer mentions {GYAN_MARKER}, so the "
            "dialog's 'official Windows build they list' claim is no longer true"
        )


def winget_manifest_dir(package_id: str) -> str:
    parts = package_id.split(".")
    if len(parts) < 2:
        raise CheckError(f"{package_id} does not look like a winget package id")
    first = parts[0][0].lower()
    return "manifests/" + first + "/" + "/".join(parts)


def version_key(name: str) -> tuple[int, ...]:
    return tuple(int(part) for part in name.split("."))


def github_json(url: str):
    headers = {"Accept": "application/vnd.github+json"}
    token = os.environ.get("GITHUB_TOKEN") or os.environ.get("GH_TOKEN")
    if token:
        headers["Authorization"] = f"Bearer {token}"
    code, body, err = http_get(url, accept=headers["Accept"], extra_headers=headers)
    if err is not None:
        raise CheckError(f"{url} could not be reached after retry: {err}")
    if code == 404:
        raise CheckError(f"{url} returned HTTP 404")
    if code is None or code < 200 or code >= 400:
        raise CheckError(f"{url} returned HTTP {code}")
    try:
        return json.loads((body or b"").decode("utf-8"))
    except json.JSONDecodeError as exc:
        raise CheckError(f"{url} did not return JSON: {exc}") from exc


def http_get(
    url: str,
    *,
    accept: str = "*/*",
    extra_headers: dict[str, str] | None = None,
    retry: bool = True,
) -> tuple[int | None, bytes | None, Exception | None]:
    headers = {"User-Agent": USER_AGENT, "Accept": accept}
    if extra_headers:
        headers.update(extra_headers)
    request = urllib.request.Request(url, headers=headers, method="GET")
    try:
        with urllib.request.urlopen(request, timeout=TIMEOUT_SEC) as response:
            return response.getcode(), response.read(), None
    except urllib.error.HTTPError as exc:
        # urllib already followed redirects. Retry only server/network failures.
        if exc.code >= 500 and retry:
            time.sleep(RETRY_SLEEP_SEC)
            return http_get(url, accept=accept, extra_headers=extra_headers, retry=False)
        body = b""
        try:
            body = exc.read()
        except Exception:
            pass
        return exc.code, body, None
    except (urllib.error.URLError, TimeoutError, OSError) as exc:
        if retry:
            time.sleep(RETRY_SLEEP_SEC)
            return http_get(url, accept=accept, extra_headers=extra_headers, retry=False)
        return None, None, exc


if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        sys.exit(130)
