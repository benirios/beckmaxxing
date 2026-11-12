"""Build the mapped cBots and record actual .algo artifacts without publishing.

Use --resume to reuse only unchanged source/project/API/package hashes.
Absolute intermediate paths are required: relative global MSBuild paths can
silently omit the NuGet cTrader packaging imports while C# compilation passes.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parent
MANIFEST = ROOT / "packaging-results-20261107.json"


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--resume", action="store_true")
    args = parser.parse_args()
    coverage = json.loads((ROOT / "catalog-coverage.json").read_text())
    sources = sorted(set(coverage.values()))
    prior = json.loads(MANIFEST.read_text())["records"] if MANIFEST.exists() else []
    prior = {r["source"]: r for r in prior}
    api = Path.home() / ".nuget/packages/ctrader.automate/1.0.21/lib/net6.0/cAlgo.API.dll"
    api_hash = digest(api)
    records = []
    failed = False
    for name in sources:
        source = ROOT / name
        project = source.with_suffix(".csproj")
        source_hash, project_hash = digest(source), digest(project)
        old = prior.get(name, {})
        reusable = (
            args.resume and old.get("exit_code") == 0
            and old.get("source_sha256") == source_hash
            and old.get("project_sha256") == project_hash
            and old.get("api_sha256") == api_hash
            and old.get("packages")
            and all((ROOT.parent / p).is_file() and digest(ROOT.parent / p) == h
                    for p, h in old["packages"].items())
        )
        if reusable:
            record = old
            print(f"REUSE {name}", flush=True)
        else:
            intermediate = ROOT / "obj/PackagingVerified" / source.stem
            output = ROOT / "bin/Packaged" / source.stem
            command = [
                "dotnet", "build", str(project), "--ignore-failed-sources",
                "-p:NuGetAudit=false", "-p:AlgoBuild=true", "-p:AlgoPublish=false",
                f"-p:BaseIntermediateOutputPath={intermediate}/",
                f"-p:MSBuildProjectExtensionsPath={intermediate}/",
                f"-p:BaseOutputPath={output}/", "-v", "quiet",
            ]
            result = subprocess.run(command, text=True, stdout=subprocess.PIPE,
                                    stderr=subprocess.STDOUT)
            packages = {str(p.relative_to(ROOT.parent)): digest(p)
                        for p in output.rglob("*.algo")}
            record = dict(
                source=name, source_sha256=source_hash, project_sha256=project_hash,
                api_sha256=api_hash, source_unchanged=source_hash == digest(source),
                exit_code=result.returncode, command=command, output=result.stdout,
                packages=packages,
            )
            print(f"BUILD {name}: exit={result.returncode}, packages={len(packages)}",
                  flush=True)
            if result.returncode:
                print(result.stdout, flush=True)
        failed |= (record["exit_code"] != 0 or not record["packages"]
                   or not record.get("source_unchanged", False))
        records.append(record)
        temporary = MANIFEST.with_suffix(".tmp")
        temporary.write_text(json.dumps(dict(
            verified_utc=datetime.now(timezone.utc).isoformat(), records=records,
        ), indent=2) + "\n")
        temporary.replace(MANIFEST)
    return int(failed)


if __name__ == "__main__":
    raise SystemExit(main())
