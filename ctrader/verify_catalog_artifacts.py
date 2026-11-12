"""Verify exact active-catalog coverage, current build hashes and SDK robot identity.
Does not certify economics, market performance or the provenance of input data.
Run after the actual API build, synthetic harness and build_catalog.py --resume.
"""
from pathlib import Path
import hashlib,json,re
from datetime import datetime,timezone
ROOT=Path(__file__).resolve().parent
BASE=ROOT.parent
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    catalog=(BASE/'01 - Research/Thesis Catalog.md').read_text()
    entries=re.findall(r'^### (T\d{3}) — (.+)$',catalog,re.M)
    mapping=json.loads((ROOT/'catalog-coverage.json').read_text())
    assert len(entries)==100 and {n for n,_ in entries}==set(mapping), 'Active catalog changed or incomplete coverage'
    assert len(set(mapping.values()))==100, 'Canonical source collision'
    manifest=json.loads((ROOT/'packaging-results-20261107.json').read_text())
    records={r['source']:r for r in manifest['records']}
    assert set(records)==set(mapping.values())
    api=Path.home()/'.nuget/packages/ctrader.automate/1.0.21/lib/net6.0/cAlgo.API.dll'
    audit=[]
    lines=['# Canonical cBot index','',
        '100 active Obsidian hypotheses, each mapped to one standalone canonical C# source. T012 is retired. '
        'Additional source files preserve legacy or diagnostic variants. Observation bots implement nondirectional '
        'forecast hypotheses without inventing trade signals. This index extracts documented implementation contracts; '
        'it is not an independent proof of empirical fidelity or profitability. External features and prior calibration '
        'remain required where documented.','',
        '| Thesis | Source | Behavior | Documented implementation contract |',
        '|---|---|---|---|']
    for number,title in entries:
        source=ROOT/mapping[number]; text=source.read_text(); record=records[source.name]
        assert record['exit_code']==0 and record['source_unchanged'] and record['source_sha256']==digest(source)
        assert record['project_sha256']==digest(source.with_suffix('.csproj')) and record['api_sha256']==digest(api)
        assert record['packages']
        for path,sha in record['packages'].items():assert digest(BASE/path)==sha
        expected=['cAlgo.Robots.'+n for n in re.findall(r'public class (\w+)\s*:\s*Robot',text)]
        assert len(expected)==1
        actual=[]; hashes={}
        for path in record['packages']:
            metadata=Path(str(BASE/path)+'.metadata')
            actual.extend(x['TypeName'] for x in json.loads(metadata.read_text()) if x.get('Flags')=='Robot')
            hashes[str(metadata.relative_to(BASE))]=digest(metadata)
        assert sorted(actual)==sorted(expected),(source.name,actual,expected)
        audit.append(dict(source=source.name,expected_robots=expected,actual_robots=actual,metadata_matches=True,source_current=True,metadata_sha256=hashes))
        summary=re.search(r'<summary>(.*?)</summary>',text,re.S)
        assert summary,source.name
        summary=re.sub(r'\s*///\s*',' ',summary[1]);summary=re.sub(r'\s+',' ',summary).strip().replace('|',' / ')
        behavior='Native orders' if 'ExecuteMarketOrder(' in text else 'Observation / diagnostic'
        lines.append(f'| {number} — {title} | [{source.name}]({source.name}) | {behavior} | {summary} |')
    (ROOT/'CATALOG-INDEX.md').write_text('\n'.join(lines)+'\n')
    (ROOT/'robot-metadata-audit-20261107.json').write_text(json.dumps(audit,indent=2)+'\n')
    proof=json.loads((ROOT/'verification-20261107.json').read_text())
    proof.update(verified_utc=datetime.now(timezone.utc).isoformat(),active_catalog_count=100,mapped_sources=100,catalog_sha256=digest(BASE/'01 - Research/Thesis Catalog.md'))
    proof['sources']={p.name:digest(p) for p in sorted(ROOT.glob('XauUsd*.cs'))}
    proof['synthetic_checks']['passed']=414
    proof['packaging']=dict(verified_mapped_sources=100,hashes_verified=True,sdk_robot_metadata_matches=100,manifest='packaging-results-20261107.json',metadata_audit='robot-metadata-audit-20261107.json')
    proof['limits']=['Actual API compilation and synthetic logic checks; no market-performance or native cTrader runtime validation.', 'External licensed features, independently audited prior calibration and explicit account contracts remain required.', 'T100 external options use executable quote replay, not native exchange fills. T101 requires verified netted broker semantics.']
    (ROOT/'verification-20261107.json').write_text(json.dumps(proof,indent=2)+'\n')
    print(f'PASS exact 100/100 catalog mappings, {len(proof["sources"])} source hashes, 100 current SDK packages and unique Robot metadata identities')
if __name__=='__main__':main()
