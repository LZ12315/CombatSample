"""Archive the saved Unity workspace, including untracked source files.

Run from any directory: python Tools/Package_Submission.py
Only the Python standard library is required. No Unity/Git state is modified.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[1]
SOURCE_DIRS = ("Assets", "Packages", "ProjectSettings", "Docs", "Tools", "Bin")
ROOT_FILES = ("README.md", "AGENTS.md", ".gitignore", ".gitattributes", "LICENSE", "LICENSE.md", "LICENSE.txt")
EXCLUDED_DIRS = {".git", "__pycache__", ".pytest_cache", ".mypy_cache"}


def sources() -> list[Path]:
    result = []
    for name in SOURCE_DIRS:
        base = ROOT / name
        if not base.exists():
            continue
        for path in base.rglob("*"):
            relative = path.relative_to(ROOT)
            if any(part in EXCLUDED_DIRS for part in relative.parts):
                continue
            if path.is_symlink():
                raise RuntimeError(f"Source symlink needs explicit review: {relative}")
            if path.is_file():
                result.append(path)
    result.extend(ROOT / name for name in ROOT_FILES if (ROOT / name).is_file())
    return sorted(result, key=lambda path: path.relative_to(ROOT).as_posix())


def signature(path: Path) -> tuple[int, int]:
    stat = path.stat()
    return stat.st_size, stat.st_mtime_ns


def digest_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for data in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(data)
    return digest.hexdigest()


def ensure_snapshot(files: list[Path], initial: dict[Path, tuple[int, int]]) -> None:
    if sources() != files:
        raise RuntimeError("Source file list changed during packaging; save the project and rerun.")
    changed = [path.relative_to(ROOT).as_posix() for path in files if signature(path) != initial[path]]
    if changed:
        raise RuntimeError(f"Source files changed during packaging: {changed[:10]}")


def git_revision() -> str | None:
    try:
        return subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True,
                                       stderr=subprocess.DEVNULL).strip()
    except (OSError, subprocess.CalledProcessError):
        return None


def archive_project(output: Path, files: list[Path], initial: dict[Path, tuple[int, int]]) -> dict:
    partial = output / "04_Unity工程.zip.partial"
    target = output / "04_Unity工程.zip"
    records = []
    try:
        with zipfile.ZipFile(partial, "w", compression=zipfile.ZIP_DEFLATED,
                             compresslevel=4, allowZip64=True, strict_timestamps=False) as archive:
            for number, path in enumerate(files, 1):
                relative = path.relative_to(ROOT).as_posix()
                if signature(path) != initial[path]:
                    raise RuntimeError(f"Source changed before archival: {relative}")
                # One read: hash and compress the same stream instead of reading twice.
                digest = hashlib.sha256()
                written = 0
                info = zipfile.ZipInfo.from_file(path, "CombatSample/" + relative)
                info.compress_type = zipfile.ZIP_DEFLATED
                with path.open("rb") as source, archive.open(info, "w") as target_stream:
                    for data in iter(lambda: source.read(1024 * 1024), b""):
                        digest.update(data)
                        target_stream.write(data)
                        written += len(data)
                if written != initial[path][0] or signature(path) != initial[path]:
                    raise RuntimeError(f"Source changed while archiving: {relative}")
                records.append({"path": relative, "size": written, "sha256": digest.hexdigest()})
                if number % 1000 == 0:
                    print(f"Archived {number}/{len(files)} source files", flush=True)
            ensure_snapshot(files, initial)
            manifest = {
                "created_utc": datetime.now(timezone.utc).isoformat(),
                "source": "Saved filesystem workspace, including untracked files; not a Git commit export",
                "git_head_reference_only": git_revision(),
                "file_count": len(records),
                "source_bytes": sum(record["size"] for record in records),
                "files": records,
            }
            manifest_bytes = json.dumps(manifest, ensure_ascii=False, indent=2).encode("utf-8")
            archive.writestr("CombatSample/_SubmissionManifest.json", manifest_bytes)
        print("Verifying every archived source file (CRC + SHA-256)...", flush=True)
        with zipfile.ZipFile(partial) as archive:
            names = archive.namelist()
            expected = {"CombatSample/" + item["path"] for item in records}
            expected.add("CombatSample/_SubmissionManifest.json")
            if len(names) != len(expected) or set(names) != expected:
                raise RuntimeError("Archive entry set or duplicate-entry check failed.")
            if archive.read("CombatSample/_SubmissionManifest.json") != manifest_bytes:
                raise RuntimeError("Embedded manifest verification failed.")
            for number, item in enumerate(records, 1):
                digest = hashlib.sha256()
                count = 0
                with archive.open("CombatSample/" + item["path"]) as stream:
                    for data in iter(lambda: stream.read(1024 * 1024), b""):
                        digest.update(data)
                        count += len(data)
                if count != item["size"] or digest.hexdigest() != item["sha256"]:
                    raise RuntimeError(f"Archive content mismatch: {item['path']}")
                if number % 2000 == 0:
                    print(f"Verified {number}/{len(records)} source files", flush=True)
        ensure_snapshot(files, initial)
        partial.rename(target)
        (output / "工程文件清单.json").write_bytes(manifest_bytes)
        return {
            "archive": target.name,
            "archive_bytes": target.stat().st_size,
            "archive_sha256": digest_file(target),
            "source_files": len(records),
            "source_bytes": manifest["source_bytes"],
            "all_archive_entries_crc_and_sha256_verified": True,
            "source_file_list_sizes_and_mtimes_unchanged": True,
            "includes_untracked_source_files": True,
            "unity_cold_import_or_player_test_performed": False,
        }
    except BaseException:
        if partial.exists():
            partial.unlink()
        raise


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, help="New folder inside the project's Builds directory")
    args = parser.parse_args()
    docs = ROOT / "Docs/Submission"
    copies = {
        "Game_Design_zh-CN.pdf": "01_游戏设计说明.pdf",
        "Game_Design_zh-CN.md": "01_游戏设计说明_可编辑.md",
        "Play_and_Project_Guide_zh-CN.md": "02_运行与工程说明.txt",
        "Third_Party_Notice_zh-CN.md": "03_素材与第三方说明.txt",
    }
    for name in copies:
        if not (docs / name).is_file():
            raise FileNotFoundError(f"Missing submission document: {docs / name}")
    output = args.output or ROOT / "Builds" / ("Submission_" + datetime.now().strftime("%Y%m%d_%H%M%S"))
    output = output.resolve()
    if not output.is_relative_to((ROOT / "Builds").resolve()) or output == (ROOT / "Builds").resolve():
        raise ValueError("Output must be a new subdirectory of this project's Builds directory.")
    output.mkdir(parents=True, exist_ok=False)
    for source, destination in copies.items():
        if destination.endswith(".txt"):
            (output / destination).write_text((docs / source).read_text(encoding="utf-8"), encoding="utf-8-sig")
        else:
            shutil.copy2(docs / source, output / destination)
    shutil.copy2(ROOT / "Assets/Resources/UI/BattleDeathFont-License.txt", output / "字体许可证.txt")
    (output / "请先阅读.txt").write_text(
        "CombatSample 提交资料\n\n"
        "01_游戏设计说明.pdf：游戏设计文档，另附可编辑 Markdown。\n"
        "02_运行与工程说明.txt：操作、工程打开、依赖和构建说明。\n"
        "03_素材与第三方说明.txt：制作范围与第三方许可边界。\n"
        "04_Unity工程.zip：完整已保存源工程，解压后在 Unity Hub 添加 CombatSample 目录。\n"
        "工程文件清单.json、打包校验.json、SHA256SUMS.txt：归档内容与完整性校验。\n\n"
        "请另外放入完整 Windows 游戏文件夹及录制的 MP4 流程视频；本目录不自动生成或复制二者。\n"
        "不能只提交 exe；必须连同 Data 目录、DLL 等文件一起交付。\n"
        "工程包含尚未提交的源文件，但不包含缓存、Git 历史或 Editor 尚未保存的内容。\n"
        "如之后修改源码、场景或设置，请重新构建并重新打工程包。\n"
        "素材和插件的原始文件转交范围，请按实际取得的许可确认。\n", encoding="utf-8-sig")
    files = sources()
    initial = {path: signature(path) for path in files}
    total = sum(size for size, _ in initial.values())
    if shutil.disk_usage(output).free < total + 1024 ** 3:
        raise RuntimeError("Insufficient free space for conservative archive staging.")
    print(f"Output: {output}\nSource files: {len(files)}; bytes: {total}", flush=True)
    report = archive_project(output, files, initial)
    (output / "打包校验.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    checksum_lines = [f"{digest_file(path)}  {path.name}" for path in sorted(output.iterdir()) if path.is_file()]
    (output / "SHA256SUMS.txt").write_text("\n".join(checksum_lines) + "\n", encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2), flush=True)
    print("Complete: " + str(output), flush=True)


if __name__ == "__main__":
    main()
