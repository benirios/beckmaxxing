"""Observer generation changes observability, not original strategy statements."""
import difflib
import importlib.util
from pathlib import Path
import re
import pytest

SPEC=importlib.util.spec_from_file_location('lab_build',Path(__file__).resolve().parents[1]/'backtest_lab/build.py')
build=importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(build)


def test_observer_only_changes_permission_and_inserts_observation_without_order_calls():
    source='''using System;
[Robot(AccessRights = AccessRights.None)]
class Example {
    protected override void OnStart()
    {
        if (Guard()) return;
        ExecuteMarketOrder();
    }
}
'''
    observed=build.instrument(source,'0'*64)
    assert observed.count('AccessRights.FullAccess') == 1
    assert 'AccessRights.None' not in observed
    # File output needs this explicit permission change; strategy statements stay intact.
    permitted_source=source.replace('AccessRights.None','AccessRights.FullAccess')
    a,b=permitted_source.splitlines(True),observed.splitlines(True)
    changes=[op for op in difflib.SequenceMatcher(a=a,b=b,autojunk=False).get_opcodes() if op[0]!='equal']
    assert changes and all(op[0]=='insert' for op in changes)
    added=''.join(''.join(b[j:k]) for _,_,_,j,k in changes)
    assert 'LAB_LIVE_V2' in added and 'Account.Equity' in added
    assert 'File.AppendAllText(LabObserverOutput' in added
    assert 'File.WriteAllText(LabObserverOutput' in added
    assert not re.search(r'ExecuteMarketOrder|Place\w*Order|ClosePosition|ModifyPosition|CancelPendingOrder',added)


def test_existing_timer_cannot_be_silently_replaced():
    with pytest.raises(ValueError):
        build.instrument('protected override void OnTimer() {} protected override void OnStart() {}','0'*64)


def test_missing_robot_permission_is_rejected():
    with pytest.raises(ValueError, match="file-write permission"):
        build.instrument('protected override void OnStart() {}','0'*64)


def test_existing_v2_observer_cannot_be_instrumented_twice():
    with pytest.raises(ValueError, match="already has telemetry"):
        build.instrument('LAB_LIVE_V2 protected override void OnStart() {}','0'*64)
