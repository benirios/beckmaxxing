"""Build verified, source-preserving observer packages for the local runner."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[1]


def sha(path): return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def instrument(source, source_hash):
    if "LAB_LIVE_V1" in source or "LAB_LIVE_V2" in source or re.search(r"override\s+void\s+OnTimer\s*\(", source):
        raise ValueError("Source already has telemetry or OnTimer; explicit integration required")
    match = re.search(r"protected\s+override\s+void\s+OnStart\s*\(\s*\)\s*\{", source)
    if not match: raise ValueError("No supported OnStart declaration")
    source, replacements = re.subn(r"(\[Robot\(.*?AccessRights\s*=\s*)AccessRights\.None", r"\1AccessRights.FullAccess", source, count=1, flags=re.S)
    if replacements != 1: raise ValueError("Could not constrain the observer build's file-write permission")
    match = re.search(r"protected\s+override\s+void\s+OnStart\s*\(\s*\)\s*\{", source)
    fields = '''
        // LAB_LIVE_V2: observer only; writes account snapshots to its own job artifact.
        [Parameter("Lab Observer Output", DefaultValue = "")]
        public string LabObserverOutput { get; set; }
        private double _labObservedInitialBalance;
        private int _labObservedClosedPositions;
        private bool _labObserverFileWarning;
        private void LabObserveAccount()
        {
            string line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0:O},{1:R},{2:R},{3:R},{4}",
                Server.TimeInUtc, Account.Balance, Account.Equity,
                Account.Balance - _labObservedInitialBalance, _labObservedClosedPositions);
            Print("LAB_LIVE_V2," + line);
            try
            {
                if (!System.String.IsNullOrEmpty(LabObserverOutput))
                    System.IO.File.AppendAllText(LabObserverOutput, line + System.Environment.NewLine);
            }
            catch (System.Exception error)
            {
                if (!_labObserverFileWarning) { _labObserverFileWarning = true; Print("LAB_LIVE_FILE_ERROR," + error.GetType().Name); }
            }
        }
        protected override void OnTimer() { LabObserveAccount(); }
'''
    injection = '''
            _labObservedInitialBalance = Account.Balance;
            Positions.Closed += args => { _labObservedClosedPositions++; LabObserveAccount(); };
            Timer.Start(TimeSpan.FromMinutes(60));
            try { if (!System.String.IsNullOrEmpty(LabObserverOutput)) System.IO.File.WriteAllText(LabObserverOutput, "utc,balance,equity,net_profit,closed_positions" + System.Environment.NewLine); }
            catch (System.Exception error) { Print("LAB_LIVE_FILE_ERROR," + error.GetType().Name); }
            Print("LAB_OBSERVER_V2,original_source_sha256=SOURCE_HASH");
            LabObserveAccount();
'''.replace("SOURCE_HASH", source_hash)
    # Original method bodies stay byte-for-byte intact after this insertion.
    declaration_start = source.rfind("\n", 0, match.start()) + 1
    return source[:declaration_start] + fields + source[declaration_start:match.end()] + injection + source[match.end():]


def build(name, telemetry=True):
    source = ROOT / "ctrader" / (name + ".cs")
    original_bytes = source.read_bytes()
    original = original_bytes.decode("utf-8")
    fingerprint = hashlib.sha256(original_bytes).hexdigest()
    directory = ROOT / "ctrader/obj/LabBuild" / name
    directory.mkdir(parents=True, exist_ok=True)
    generated = directory / source.name
    generated.write_text(instrument(original, fingerprint) if telemetry else original)
    output = ROOT / "ctrader/bin/Lab" / name
    project = directory / (name + ".csproj")
    project.write_text(f'''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
<TargetFramework>net6.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems>
<ImplicitUsings>disable</ImplicitUsings><Nullable>disable</Nullable><AlgoBuild>true</AlgoBuild>
<AlgoPublish>false</AlgoPublish><IncludeSource>true</IncludeSource><BaseOutputPath>{escape(str(output))}/</BaseOutputPath>
</PropertyGroup><ItemGroup><Compile Include="{escape(str(generated))}"/>
<PackageReference Include="cTrader.Automate" Version="1.0.21"/></ItemGroup></Project>''')
    result = subprocess.run(["dotnet", "build", str(project), "-p:NuGetAudit=false", "-v", "minimal"], capture_output=True, text=True)
    output.mkdir(parents=True, exist_ok=True)
    (output / "build.log").write_text(result.stdout + result.stderr)
    if result.returncode: raise RuntimeError(result.stdout + result.stderr)
    candidates = list((output / "Debug/net6.0").glob("*.algo"))
    if len(candidates) != 1: raise RuntimeError("Expected exactly one native package")
    metadata = json.loads(candidates[0].with_suffix(".algo.metadata").read_text())
    if len(metadata) != 1 or metadata[0]["TypeName"] != "cAlgo.Robots." + name:
        raise RuntimeError("Native robot identity mismatch")
    package = output / (name + ".algo")
    shutil.copy2(candidates[0], package)
    if sha(source) != fingerprint: raise RuntimeError("Canonical source changed during build")
    identity = {"robot": name, "source_sha256": fingerprint, "package_sha256": sha(package),
                "generated_source": str(generated.relative_to(ROOT)), "generated_source_sha256": sha(generated),
                "observer": "LAB_LIVE_V2" if telemetry else None, "built_utc": datetime.now(timezone.utc).isoformat(),
                "target_framework": "net6.0", "strategy_logic_changed": False}
    package.with_suffix(".identity.json").write_text(json.dumps(identity, indent=2) + "\n")
    print(json.dumps(identity), flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("robots", nargs="+")
    parser.add_argument("--no-telemetry", action="store_true")
    args = parser.parse_args()
    for name in args.robots:
        if not re.fullmatch(r"XauUsd[A-Za-z0-9]+", name): raise SystemExit("Invalid source name")
        build(name, not args.no_telemetry)


if __name__ == "__main__": main()
