#!/usr/bin/env python3
"""Package a Unity Mono macOS .app without requiring macOS or third-party tools.

Usage:
  python PackageMac.py --app-directory PATH/第七港口.app --zip-path PATH/第七港口-macOS.zip

The ZIP must be outside the .app and must not already exist. The source bundle is
read-only. JSON on stdout records the verified package and observed Mach-O code
signature load commands; it does NOT attest signing, notarization, or execution.
On Windows this deliberately restores executable permission in ZIP Unix metadata.
"""

from __future__ import annotations

import argparse
from dataclasses import dataclass
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import plistlib
import stat
import struct
import sys
import time
from typing import BinaryIO
import zipfile


BLOCK_SIZE = 1024 * 1024
CPU_NAMES = {7: "x86", 0x01000007: "x86_64", 12: "arm", 0x0100000C: "arm64"}
REQUIRED_CPUS = {0x01000007, 0x0100000C}
THIN_MAGIC = {
    b"\xfe\xed\xfa\xce": (">", False), b"\xce\xfa\xed\xfe": ("<", False),
    b"\xfe\xed\xfa\xcf": (">", True), b"\xcf\xfa\xed\xfe": ("<", True),
}
FAT_MAGIC = {
    b"\xca\xfe\xba\xbe": (">", False), b"\xbe\xba\xfe\xca": ("<", False),
    b"\xca\xfe\xba\xbf": (">", True), b"\xbf\xba\xfe\xca": ("<", True),
}


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def read_at(stream: BinaryIO, offset: int, size: int) -> bytes:
    stream.seek(offset)
    data = stream.read(size)
    require(len(data) == size, "Truncated Mach-O data")
    return data


def stream_hash(stream: BinaryIO, size: int | None = None) -> str:
    digest = hashlib.sha256()
    remaining = size
    while remaining is None or remaining > 0:
        block = stream.read(BLOCK_SIZE if remaining is None else min(BLOCK_SIZE, remaining))
        if not block:
            require(remaining in (None, 0), "Unexpected end of file while hashing")
            break
        digest.update(block)
        if remaining is not None:
            remaining -= len(block)
    return digest.hexdigest()


def file_hash(path: Path) -> str:
    with path.open("rb") as stream:
        return stream_hash(stream)


def thin_slice(stream: BinaryIO, offset: int, size: int) -> dict:
    magic = read_at(stream, offset, 4)
    require(magic in THIN_MAGIC, "Fat Mach-O slice is not a real thin Mach-O")
    endian, is_64 = THIN_MAGIC[magic]
    header_size = 32 if is_64 else 28
    require(size >= header_size, "Truncated Mach-O header")
    cpu, subtype, file_type, command_count, command_bytes, _ = struct.unpack(
        endian + "6I", read_at(stream, offset + 4, 24))
    require(header_size + command_bytes <= size, "Mach-O load commands exceed slice")
    require(command_count <= command_bytes // 8, "Invalid Mach-O command count")
    cursor = header_size
    signatures = []
    for _ in range(command_count):
        require(cursor + 8 <= header_size + command_bytes, "Truncated load command")
        command, command_size = struct.unpack(endian + "II", read_at(stream, offset + cursor, 8))
        require(command_size >= 8 and command_size % (8 if is_64 else 4) == 0 and
                cursor + command_size <= header_size + command_bytes, "Invalid load command size")
        if command == 0x1D:  # LC_CODE_SIGNATURE: presence is not signature verification.
            require(command_size >= 16, "Truncated LC_CODE_SIGNATURE")
            data_offset, data_size = struct.unpack(endian + "II", read_at(stream, offset + cursor + 8, 8))
            require(data_offset + data_size <= size, "Code signature blob exceeds Mach-O slice")
            blob_magic = read_at(stream, offset + data_offset, 4).hex() if data_size >= 4 else None
            stream.seek(offset + data_offset)
            signatures.append({"offsetInSlice": data_offset, "bytes": data_size,
                               "blobMagicHex": blob_magic, "sha256": stream_hash(stream, data_size)})
        cursor += command_size
    require(cursor == header_size + command_bytes, "Mach-O command count/size total does not match")
    return {"cpuType": cpu, "cpuSubtype": subtype, "architecture": CPU_NAMES.get(cpu, hex(cpu)),
            "bits": 64 if is_64 else 32, "fileType": file_type, "offset": offset, "bytes": size,
            "codeSignatureLoadCommandPresent": bool(signatures), "codeSignatureBlobs": signatures}


def inspect_macho(path: Path) -> dict | None:
    """Validate thin/fat (32- or 64-bit table), both byte orders and each actual slice."""
    with path.open("rb") as stream:
        magic = stream.read(4)
        size = os.fstat(stream.fileno()).st_size
        if magic in THIN_MAGIC:
            return {"container": "thin", "slices": [thin_slice(stream, 0, size)]}
        if magic not in FAT_MAGIC:
            return None
        endian, is_64 = FAT_MAGIC[magic]
        count, = struct.unpack(endian + "I", read_at(stream, 4, 4))
        record_size = 32 if is_64 else 20
        table_end = 8 + count * record_size
        require(0 < count <= 64 and table_end <= size, "Invalid fat Mach-O architecture table")
        slices = []
        spans = []
        cpus = set()
        for index in range(count):
            record = struct.unpack(endian + ("IIQQII" if is_64 else "IIIII"),
                                   read_at(stream, 8 + index * record_size, record_size))
            cpu, subtype, slice_offset, slice_size, alignment = record[:5]
            require(slice_size > 0 and slice_offset >= table_end and slice_offset + slice_size <= size,
                    "Fat Mach-O slice is outside file")
            require(alignment <= 63 and slice_offset % (1 << alignment) == 0,
                    "Invalid fat Mach-O slice alignment")
            require((cpu, subtype) not in cpus, "Duplicate fat Mach-O architecture")
            cpus.add((cpu, subtype))
            require(all(slice_offset + slice_size <= start or slice_offset >= end for start, end in spans),
                    "Overlapping fat Mach-O slices")
            spans.append((slice_offset, slice_offset + slice_size))
            parsed = thin_slice(stream, slice_offset, slice_size)
            require(parsed["cpuType"] == cpu and parsed["cpuSubtype"] == subtype,
                    "Fat table architecture does not match actual Mach-O slice")
            slices.append(parsed)
        return {"container": "fat64" if is_64 else "fat32", "slices": slices}


class Utf8ZipInfo(zipfile.ZipInfo):
    # ZipFile normally only marks non-ASCII names UTF-8; mark every name explicitly.
    def _encodeFilenameFlags(self):
        return self.filename.encode("utf-8"), self.flag_bits | 0x800


@dataclass
class Entry:
    path: Path
    name: str
    kind: str
    mode: int
    modified: float
    size: int = 0
    digest: str = ""
    link: bytes | None = None
    macho: dict | None = None


def inside(path: Path, parent: Path) -> bool:
    return path == parent or parent in path.parents


def collect_entries(app: Path) -> list[Entry]:
    entries = []
    names = set()

    def visit(path: Path) -> None:
        source_stat = path.lstat()
        relative = path.relative_to(app.parent).as_posix()
        require("\\" not in relative and "\x00" not in relative, "Non-portable archive path: " + relative)
        require(not PurePosixPath(relative).is_absolute() and ".." not in PurePosixPath(relative).parts,
                "Unsafe archive path: " + relative)
        # A default macOS volume is case-insensitive; don't create an ambiguous extraction.
        require(relative.casefold() not in names, "Case-colliding archive path: " + relative)
        names.add(relative.casefold())
        mode = source_stat.st_mode
        if stat.S_ISLNK(mode):
            target = os.readlink(path)
            portable_target = target.replace("\\", "/") if os.name == "nt" else target
            require(not PurePosixPath(portable_target).is_absolute() and not os.path.isabs(target) and
                    ":" not in portable_target and "\x00" not in portable_target,
                    "Only relative, portable symlinks are supported: " + relative)
            resolved = path.resolve(strict=True)
            require(inside(resolved, app), "Symlink points outside app: " + relative)
            payload = portable_target.encode("utf-8")
            entries.append(Entry(path, relative, "symlink", stat.S_IFLNK | 0o777,
                                 source_stat.st_mtime, len(payload), hashlib.sha256(payload).hexdigest(), payload))
        elif stat.S_ISDIR(mode):
            require(not getattr(path, "is_junction", lambda: False)(), "Directory junction is not portable: " + relative)
            entries.append(Entry(path, relative + "/", "directory", stat.S_IFDIR | 0o755, source_stat.st_mtime))
            for child in sorted(path.iterdir(), key=lambda item: item.name):
                visit(child)
        elif stat.S_ISREG(mode):
            macho = inspect_macho(path)
            with path.open("rb") as stream:
                shebang = stream.read(2) == b"#!"
            # A shebang identifies the interpreter even when Windows has lost POSIX execute bits.
            executable_script = shebang or bool(mode & 0o111) and path.suffix.lower() in {
                ".sh", ".command", ".bash", ".zsh", ".py", ".pl", ".rb"}
            permissions = 0o755 if macho is not None or executable_script else 0o644
            entries.append(Entry(path, relative, "file", stat.S_IFREG | permissions,
                                 source_stat.st_mtime, source_stat.st_size, macho=macho))
        else:
            raise ValueError("Unsupported special file: " + relative)

    visit(app)
    return entries


def validate_bundle(app: Path, entries: list[Entry]) -> dict:
    info_path = app / "Contents" / "Info.plist"
    require(info_path.is_file(), "Missing Contents/Info.plist")
    with info_path.open("rb") as stream:
        info = plistlib.load(stream)
    require(isinstance(info, dict), "Info.plist root must be a dictionary")
    executable_name = info.get("CFBundleExecutable")
    require(isinstance(executable_name, str) and bool(executable_name) and
            executable_name not in (".", "..") and not any(c in executable_name for c in "/\\\x00:"),
            "CFBundleExecutable must be a single non-empty file name")
    require(info.get("CFBundlePackageType", "APPL") == "APPL", "Info.plist does not describe an application")
    main = app / "Contents" / "MacOS" / executable_name
    require(main.is_file() and inside(main.resolve(), app), "CFBundleExecutable is missing or outside app")
    by_path = {entry.path: entry for entry in entries}

    def universal_binary(path: Path, description: str, file_type: int = 6) -> dict:
        require(path.is_file(), "Missing " + description + ": " + str(path))
        entry = by_path.get(path.resolve())
        require(entry is not None and entry.macho is not None, description + " is not a validated Mach-O")
        data = entry.macho
        require(data["container"].startswith("fat") and REQUIRED_CPUS.issubset(
            {item["cpuType"] for item in data["slices"]}), description + " is not x86_64 + arm64 Universal")
        require(all(item["bits"] == 64 for item in data["slices"] if item["cpuType"] in REQUIRED_CPUS),
                description + " contains a non-64-bit header for a 64-bit CPU")
        require(all(item["fileType"] == file_type for item in data["slices"]),
                description + " has incorrect Mach-O file type (expected " + str(file_type) + ")")
        return {"path": path.relative_to(app).as_posix(), **data}

    executable = universal_binary(main, "main executable", 2)
    framework_root = app / "Contents" / "Frameworks"
    engine = universal_binary(framework_root / "UnityPlayer.dylib", "UnityPlayer library")
    mono_native = universal_binary(framework_root / "libmono-native.dylib", "Mono native support library")
    mono_posix = universal_binary(framework_root / "libMonoPosixHelper.dylib", "Mono POSIX support library")
    mono_paths = sorted(entry.path for entry in entries if entry.kind == "file" and
                        inside(entry.path, framework_root) and entry.path.name == "libmonobdwgc-2.0.dylib")
    require(bool(mono_paths), "Missing Mono runtime libmonobdwgc-2.0.dylib under Contents/Frameworks")
    mono = [universal_binary(path, "Mono runtime") for path in mono_paths]
    mono_config = app / "Contents" / "MonoBleedingEdge" / "etc" / "mono" / "config"
    require(mono_config.is_file() and mono_config.stat().st_size > 0,
            "Missing non-empty Contents/MonoBleedingEdge/etc/mono/config")
    data_root = app / "Contents" / "Resources" / "Data"
    require(data_root.is_dir(), "Missing Unity Contents/Resources/Data")
    require(any((data_root / name).is_file() for name in ("globalgamemanagers", "data.unity3d")),
            "Missing Unity player scene/data archive")
    managed = data_root / "Managed"
    require(managed.is_dir() and any(path.suffix.lower() == ".dll" for path in managed.iterdir()),
            "Missing Mono managed assemblies")
    return {"bundleName": info.get("CFBundleName"), "displayName": info.get("CFBundleDisplayName"),
            "bundleIdentifier": info.get("CFBundleIdentifier"), "bundleVersion": info.get("CFBundleVersion"),
            "shortVersion": info.get("CFBundleShortVersionString"), "minimumSystemVersion": info.get("LSMinimumSystemVersion"),
            "executable": executable, "unityPlayer": engine, "monoRuntime": mono,
            "monoNative": mono_native, "monoPosix": mono_posix,
            "monoConfigurationPath": mono_config.relative_to(app).as_posix(),
            "codeResourcesPresent": (app / "Contents" / "_CodeSignature" / "CodeResources").is_file(),
            "signatureVerification": "NOT_PERFORMED; load commands/blob hashes only, not a signing assertion",
            "notarizationVerification": "NOT_PERFORMED", "runtimeLaunchVerification": "NOT_PERFORMED"}


def zip_info(entry: Entry) -> Utf8ZipInfo:
    date = time.localtime(entry.modified)[:6]
    date = (1980, 1, 1, 0, 0, 0) if date[0] < 1980 else ((2107, 12, 31, 23, 59, 58) if date[0] > 2107 else date)
    info = Utf8ZipInfo(entry.name, date_time=date)
    info.create_system = 3  # Unix, including when Python is hosted on Windows.
    info.external_attr = (entry.mode << 16) | (0x10 if entry.kind == "directory" else 0)
    info.compress_type = zipfile.ZIP_DEFLATED
    info.file_size = entry.size
    return info


def package(app_directory: str | Path, zip_path: str | Path) -> dict:
    requested_app = Path(app_directory).absolute()
    require(not requested_app.is_symlink(), "App root must not be a symlink")
    app = requested_app.resolve(strict=True)
    require(app.is_dir() and app.suffix.lower() == ".app", "App directory must be an existing .app bundle")
    output = Path(zip_path).absolute()
    require(output.suffix.lower() == ".zip", "Output must have a .zip suffix")
    require(not output.exists() and not output.is_symlink(), "Refusing to overwrite existing ZIP")
    require(not inside(output.resolve(), app), "ZIP must be outside the app bundle")
    require(output.parent.is_dir(), "ZIP parent directory must already exist")
    entries = collect_entries(app)
    bundle = validate_bundle(app, entries)
    owned_output = False
    try:
        with output.open("xb") as destination:
            owned_output = True
            with zipfile.ZipFile(destination, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6,
                                 allowZip64=True) as archive:
                for entry in entries:
                    info = zip_info(entry)
                    if entry.kind == "file":
                        digest = hashlib.sha256()
                        copied = 0
                        with entry.path.open("rb") as source, archive.open(info, "w", force_zip64=entry.size >= 2**31) as target:
                            while block := source.read(BLOCK_SIZE):
                                target.write(block)
                                digest.update(block)
                                copied += len(block)
                        require(copied == entry.size, "Source size changed during packaging: " + entry.name)
                        entry.digest = digest.hexdigest()
                    else:
                        payload = entry.link or b""
                        archive.writestr(info, payload)
                        entry.digest = hashlib.sha256(payload).hexdigest()
            destination.flush()
            os.fsync(destination.fileno())

        with zipfile.ZipFile(output, "r") as archive:
            actual_entries = archive.infolist()
            require(len(actual_entries) == len(entries), "ZIP entry count differs from source")
            require(len({item.filename for item in actual_entries}) == len(entries), "Duplicate ZIP paths")
            for actual, expected in zip(actual_entries, entries):
                require(actual.filename == expected.name and actual.create_system == 3 and actual.flag_bits & 0x800,
                        "ZIP path, creator or UTF-8 metadata mismatch: " + expected.name)
                require(actual.external_attr >> 16 == expected.mode and actual.file_size == expected.size,
                        "ZIP Unix type/permission or size mismatch: " + expected.name)
                with archive.open(actual) as stream:
                    require(stream_hash(stream) == expected.digest, "ZIP SHA-256 mismatch: " + expected.name)
                if expected.kind == "file":
                    require(file_hash(expected.path) == expected.digest, "Source changed after packaging: " + expected.name)
                elif expected.kind == "symlink":
                    current = os.readlink(expected.path)
                    if os.name == "nt":
                        current = current.replace("\\", "/")
                    require(current.encode("utf-8") == expected.link, "Source symlink changed during packaging")
        # Detect additions/removals and permission classification changes, too.
        final_entries = collect_entries(app)
        require([(item.name, item.kind, item.mode, item.macho) for item in final_entries] ==
                [(item.name, item.kind, item.mode, item.macho) for item in entries],
                "Bundle tree or Mach-O metadata changed during packaging")
        return {"result": "PASS", "appDirectory": str(app), "zipPath": str(output),
                "zipBytes": output.stat().st_size, "zipSHA256": file_hash(output),
                "entriesVerified": len(entries), "filesVerified": sum(item.kind == "file" for item in entries),
                "directoriesVerified": sum(item.kind == "directory" for item in entries),
                "symlinksVerified": sum(item.kind == "symlink" for item in entries),
                "uncompressedBytes": sum(item.size for item in entries), "bundle": bundle,
                "machOFiles": [{"path": item.name, "unixMode": "0755", **item.macho}
                               for item in entries if item.macho is not None],
                "scope": "Archive integrity, dependency layout, Mach-O architectures and extraction permissions; not macOS runtime validation"}
    except BaseException:
        if owned_output:
            output.unlink(missing_ok=True)
        raise


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--app-directory", required=True, help="Existing Unity Mono Universal .app")
    parser.add_argument("--zip-path", required=True, help="New ZIP outside the app (parent must exist)")
    args = parser.parse_args()
    try:
        report = package(args.app_directory, args.zip_path)
    except (OSError, ValueError, plistlib.InvalidFileException, zipfile.BadZipFile) as error:
        print(json.dumps({"result": "FAIL", "error": str(error)}, ensure_ascii=False), file=sys.stderr)
        return 1
    print(json.dumps(report, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")
    raise SystemExit(main())
