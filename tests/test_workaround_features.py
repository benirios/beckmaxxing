"""Synthetic input-contract checks; never market evidence."""
from datetime import datetime,timezone
from pathlib import Path
import json
import numpy as np
import pandas as pd
from research import prepare_workaround_features as f


def quote_fixture(tmp_path,monkeypatch):
    times=pd.date_range('2025-06-24T08:00:00Z',periods=60,freq='min')
    rows=[]
    for i,t in enumerate(times):
        rows.append({'timestamp':t.isoformat(),'first_utc':(t+pd.Timedelta(milliseconds=10)).isoformat(),
                     'last_utc':(t+pd.Timedelta(seconds=59)).isoformat(),'close':100+i/100,
                     'high':101+i/100,'low':99+i/100,'bid_close':99.9+i/100,
                     'ask_close':100.1+i/100,'ticks':10,'complete':1})
    pd.DataFrame(rows).to_csv(tmp_path/'gold_m1.csv',index=False)
    (tmp_path/'invalid_quotes.csv').write_text('timestamp,bid,ask\n2025-06-24T08:30:05Z,0,100\n')
    (tmp_path/'calendars.json').write_text(json.dumps({'nyse_full_dates':['2025-06-24']}))
    monkeypatch.setattr(f,'REF',tmp_path)
    return f.Quotes(tmp_path)


def test_missing_or_invalid_minutes_cannot_become_complete_features(tmp_path,monkeypatch):
    q=quote_fixture(tmp_path,monkeypatch)
    assert q.window('2025-06-24T08:00Z','2025-06-24T08:30Z') is not None
    assert q.window('2025-06-24T08:00Z','2025-06-24T09:00Z') is None
    assert q.boundary('2025-06-24T08:31Z') is None
    q.m=q.m.drop(pd.Timestamp('2025-06-24T08:10Z'))
    assert q.window('2025-06-24T08:00Z','2025-06-24T08:30Z') is None


def test_boundary_samples_must_predate_decision_and_be_fresh(tmp_path,monkeypatch):
    q=quote_fixture(tmp_path,monkeypatch)
    assert q.boundary('2025-06-24T08:20Z') is not None
    q.m.loc[pd.Timestamp('2025-06-24T08:19Z'),'last_utc']=pd.Timestamp('2025-06-24T08:19:50Z')
    assert q.boundary('2025-06-24T08:20Z') is None
    assert f.stamp('2026-10-24').tzinfo is not None


def test_frozen_estimators_handle_constant_features_without_future_labels():
    x=np.column_stack([np.arange(60),np.ones(60)])
    y=2*np.arange(60)+1
    pred,*_=f.ridge(x,y,[60,1])
    assert abs(pred-121)<.001
    p=f.logistic(x,(np.arange(60)>30).astype(float),[40,1])
    assert 0<=p<=1
    assert f.entropy(np.arange(20))==0
