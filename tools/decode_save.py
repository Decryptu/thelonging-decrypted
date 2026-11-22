"""Decode or re-encode FileBasedPrefs save files from The Longing."""
import argparse
import json
import sys
from pathlib import Path

CODEWORD = "longingLALSD9034jokdsfmm"


def scramble(text: str) -> str:
    key_len = len(CODEWORD)
    return "".join(chr(ord(ch) ^ ord(CODEWORD[i % key_len])) for i, ch in enumerate(text))


def decode_file(path: Path) -> dict:
    raw = path.read_text(encoding="utf-8", newline="")
    return json.loads(scramble(raw))


def encode_data(data: dict) -> str:
    compact = json.dumps(data, separators=(",", ":"), ensure_ascii=False)
    return scramble(compact)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", type=Path, help="Path to a saveData*.txt or decoded .json file")
    parser.add_argument("--encode", action="store_true", help="Encode JSON back to scrambled saveData format")
    parser.add_argument("--output", "-o", type=Path, help="Output file path (default: stdout)")
    parser.add_argument("--summary", action="store_true", help="Print section counts and sample keys")
    parser.add_argument("--filter", help="Only print keys matching this case-insensitive substring")
    args = parser.parse_args()

    if args.encode:
        data = json.loads(args.input.read_text(encoding="utf-8"))
        encoded = encode_data(data)
        if args.output:
            args.output.parent.mkdir(parents=True, exist_ok=True)
            args.output.write_text(encoded, encoding="utf-8", newline="")
        else:
            sys.stdout.write(encoded)
        return 0

    data = decode_file(args.input)
    if args.summary:
        for section in ("IntData", "BoolData", "FloatData", "StringData"):
            entries = data.get(section, [])
            print(f"{section}: {len(entries)} entries")
        return 0

    if args.filter:
        needle = args.filter.lower()
        for section in ("IntData", "BoolData", "FloatData", "StringData"):
            matches = [
                entry for entry in data.get(section, [])
                if needle in str(entry.get("Key", "")).lower()
            ]
            if matches:
                print(f"[{section}] ({len(matches)} matches)")
                for entry in matches:
                    print(f"  {entry['Key']} = {entry['Value']}")
        return 0

    rendered = json.dumps(data, indent=2, ensure_ascii=False) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(rendered, encoding="utf-8", newline="\n")
    else:
        sys.stdout.write(rendered)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
