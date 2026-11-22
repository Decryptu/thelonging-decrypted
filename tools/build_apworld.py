"""Package the Archipelago world from tracked files and honor .apignore exclusions."""
import argparse
import subprocess
from fnmatch import fnmatchcase
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "apworld" / "the_longing"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True, type=Path)
    output = parser.parse_args().output

    if not SOURCE.exists():
        raise SystemExit(f"World package directory does not exist yet: {SOURCE}")

    apignore = SOURCE / ".apignore"
    patterns = (
        [
            line.strip().lstrip("/")
            for line in apignore.read_text(encoding="utf-8").splitlines()
            if line.strip().lstrip("/") and not line.lstrip().startswith("#")
        ]
        if apignore.exists()
        else []
    )
    files = subprocess.check_output(
        ["git", "-C", str(SOURCE), "ls-files", "-z"]
    ).decode("utf-8").split("\0")
    output.parent.mkdir(parents=True, exist_ok=True)
    with ZipFile(output, "w", ZIP_DEFLATED) as archive:
        for filename in filter(None, files):
            if filename == ".apignore" or any(
                fnmatchcase(filename, pattern) or (pattern.endswith("/") and filename.startswith(pattern))
                for pattern in patterns
            ):
                continue
            archive.write(SOURCE / filename, f"the_longing/{filename}")
    print(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
