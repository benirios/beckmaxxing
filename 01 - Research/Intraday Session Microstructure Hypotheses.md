---
tags: [xauusd, intraday, hypotheses, microstructure]
status: research-candidates
---

# Intraday session microstructure hypotheses

These are research leads, not trading signals. The cited work studies gold spot
or exchange-traded futures, mostly before 2016. It establishes changing
volatility, liquidity, and price efficiency through the day; it does **not**
establish a profitable direction rule for a modern retail XAUUSD CFD. Earlier
fixed-session strategies in this vault have failed, so these ideas are framed
as conditional and deliberately unproven.

## Evidence reviewed

### Gold's intraday properties vary by session

Batten et al. use 5-minute wholesale precious-metal data from 2000–2015. They
report significant intraday periodicity in returns, volume, volatility, and
bid-ask spread. Gold volume rises into the European/U.S. overlap, while spreads
are lowest during European market hours. Their results also vary across
subperiods, so the old clock-time patterns may not persist. See
[the paper and full text](https://journals.plos.org/plosone/article?id=10.1371/journal.pone.0174232).

Iwatsubo, Watkins, and Xu use one-minute TOCOM and COMEX futures data from
September 2014 to March 2015. They report higher inefficiency at the Tokyo and
London opens, relatively higher informational efficiency at the New York open,
and evidence that Tokyo daytime gold trading is more uninformed while New York
daytime trading is more informed. Their sample is only 128 days and Tokyo was
not a major global gold price-setting venue. See the [RIETI paper summary](https://www.rieti.go.jp/en/publications/summary/17110007.html)
and [full paper](https://www.rieti.go.jp/jp/publications/dp/17e120.pdf).

These results justify testing whether **the same price move has different
follow-through by session and liquidity state**. They do not say which side to
trade.

## Candidate D — Tokyo-morning price-pressure reversal

**Thesis (inference):** if the Tokyo daytime session contains more uninformed
price pressure, unusually large short-window XAUUSD moves during the liquid
Tokyo open may partially reverse once that local flow subsides. This is a
mechanism-based inference from the futures microstructure study, not a finding
that Tokyo gold returns reverse. The signal would use XAUUSD alone, avoiding a
cross-feed latency strategy.

**Research design to preregister before any result is viewed:**

1. Map the historical TOCOM day session to `Asia/Tokyo` using the exchange
   calendar valid on each date; do not assume fixed UTC or copy the old
   pre-2016 session times into current dates.
2. At the end of the first 30 minutes, calculate the midpoint return from the
   first executable quote of the session. Define an extreme move using a
   trailing, prior-session-only percentile of same-session absolute returns.
   Do not fit a threshold on the candidate date.
3. Enter at the next executable bid/ask quote against the move. Exit after a
   fixed 30 minutes or at a protective stop fixed from the preceding sample;
   use the earlier exit. One trade per session.
4. Compare with same-time continuation, unconditional mean reversion, and
   time-matched random-direction controls. Report all three without choosing
   the best after inspection.
5. Reject unless the fade is positive after the actual bid/ask spread,
   commission, slippage, and weekend/rollover costs; beats continuation and
   random controls; and remains positive across later untouched years.

**Why confidence is low:** the source measures efficiency proxies, not
conditional return reversal; TOCOM was a secondary gold venue; its sample is
short and old; and EXP-015 already found no edge in a different Asian-session
long. This is a distinct conditional intraday mean-reversion claim, but its
prior should remain low until modern XAUUSD quote data support it.

**Data needed:** timestamped XAUUSD bid/ask ticks or validated one-second
quotes, historical exchange hours and holidays, and the exact prop firm's
commission, stop execution, and daily drawdown rules. M5 bars are insufficient
to price the first executable entry around a session open reliably.

## Candidate E — session-aware volatility and execution gate

**Thesis:** because intraday volatility, efficiency, and spread vary by
session, a fixed stop distance or entry rule may have materially different
execution quality across Tokyo, London, and New York. A volatility-scaled
position-risk rule or a predeclared session skip may reduce avoidable losses.
This is a risk-control hypothesis, not directional alpha.

**Research design:** on each existing frozen strategy, compare the original
rule with one calendar-only session gate and one trailing-volatility-scaled
risk rule, selected before seeing results. Measure expectancy on the same
eligible dates, missed trades, realized spread/slippage, and intraday equity
drawdown. A smaller loss caused only by taking fewer trades is not an edge;
the adjusted strategy must still show positive after-cost expectancy and
survive the firm's equity-based limits. Do not apply a filter to rescue one of
the already rejected strategies using its exposed sample.

**Evidence limitation:** Batten et al. report the European session's gold
spread was historically lower, but Iwatsubo et al. find spreads in New York
gold comparatively stable through their short sample. Session-specific
execution quality must therefore be measured on the target broker, not assumed
from exchange futures.

## Excluded interpretation — generic session breakout

The literature's higher volatility at market opens does not imply that
breakouts predict direction. This vault has already rejected multiple fixed
NY opening-range variants, a London failed-breakout reversal, and a London RSI
ORB. Re-labelling an opening-range strategy as a session-microstructure idea
would not make it a distinct hypothesis.

## Current ranking

| Candidate | Mechanism evidence | Prop-firm fit | Main unresolved risk |
|---|---|---|---|
| A — post-release continuation | Macro releases affect gold volatility and price discovery | Unclear until account-specific news rules are checked | News gap restrictions and missing broker tick history |
| D — Tokyo-morning price-pressure reversal | Tokyo gold futures show less efficient / more uninformed activity in a short old sample | Potentially compatible; must use one broker feed and avoid cross-feed arbitrage | The paper does not demonstrate return reversal |
| E — session-aware execution/risk gate | Session variation in volatility, liquidity and spread is documented | Generally compatible as a risk control | Risk management alone cannot create positive expectancy |
| B — GC lead-lag convergence | COMEX contributes strongly to gold price discovery | Poor for FTMO because delayed-feed strategies are prohibited | No validated retail-speed tradable lag |

No candidate here has been backtested in this note. The existing run register
remains authoritative for completed strategies; a later study must be frozen
and registered before testing.
