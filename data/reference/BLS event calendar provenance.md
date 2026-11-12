# BLS CPI and Employment Situation schedule, 2018–2023

`bls_cpi_empsit_2018_2023.csv` contains 144 **scheduled** release events:
12 Consumer Price Index and 12 Employment Situation events in each of six
calendar years. Each row carries its official annual calendar URL. Dates and
08:30 times are New York local time, including historical daylight-saving
changes. Source: [BLS prior-year schedule index](https://www.bls.gov/bls/archived_sched.htm)
and the linked 2018–2023 annual calendars.

The annual calendar pages were read through the browser retrieval tool on
2026-11-03 because the BLS site returned HTTP 403 to direct `curl`. The CSV
was parsed from the 08:30 CPI and Employment Situation rows. Checks found 144
unique date/release pairs, all on weekdays, and exactly 12 of each release
type per year. File SHA-256:
`4dd9d588c459388169d730755577c611308f37e61e5385163cf5cd0220c95969`.

This is a **schedule**, not proof of actual publication time. Before a news
strategy test, check the original archived release or official correction
notice for each event, and exclude dates with delayed, cancelled, or
off-schedule publication. Determine overlapping releases explicitly. BLS
calendar data alone does not provide surprise values or executable quotes.

The available third-party XAUUSD M5 bars cover 08:25–09:00 for 140 of the
144 scheduled events. The four incomplete windows are 2020-04-10 CPI,
2020-08-12 CPI, 2021-04-02 Employment Situation, and 2023-04-07 Employment
Situation. This is an **availability check only**; no event returns or P/L were
examined. M5 bars cannot reconstruct the 08:29:59 reference quote, spread
burst, first quote at or after 08:35, or tick-level stop execution required
by [[../../01 - Research/Intraday Thesis Scan - 2026-11-03#Candidate A — post-release gold continuation, event conditioned|Candidate A]].
