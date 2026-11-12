#!/bin/zsh
cd -- "${0:A:h}"
exec python3 -u -m backtest_lab.server --port 8765
