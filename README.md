# Fantasy NBA Advisor 🏀

**A personal, non-commercial Yahoo Fantasy Basketball analytics project.**

> **Project status:** Planning / early development. Yahoo Fantasy Sports API access has been requested and is subject to approval. This repository currently documents the proposed application; it does not yet provide a working integration.

## Overview

Fantasy NBA Advisor is a private tool designed to help a single user make informed roster decisions in a Yahoo Fantasy Basketball league. The initial use case is an **8-team, Head-to-Head 9-Category (9-CAT)** league.

The goal is to compare the user's roster against players available in the league and suggest potential adds and drops using statistical trends and category needs. All recommendations are advisory; the user makes any roster changes manually in Yahoo Fantasy.

## Planned features

- **League context:** Read authorized league settings and the user's team roster.
- **Player availability:** Identify free agents and waiver-wire players in the user's league.
- **Statistical analysis:** Review available player statistics and recent performance trends.
- **9-CAT evaluation:** Assess points (PTS), rebounds (REB), assists (AST), steals (STL), blocks (BLK), three-pointers made (3PM), field-goal percentage (FG%), free-throw percentage (FT%), and turnovers (TO).
- **Roster recommendations:** Suggest potential player adds and drops based on category fit and team needs.
- **Periodic reports:** Produce personal summaries of notable available players and roster improvement opportunities.

## Intended architecture

```text
Yahoo Fantasy Sports API
          |
          | OAuth 2.0 (authorized, read-only requests)
          v
   Private .NET backend
          |
          v
 Statistics & 9-CAT analysis
          |
          v
 AI-assisted recommendations
          |
          v
 Personal report (reviewed by the user)
```

The implementation and scheduling mechanism may evolve during development. No automatic Yahoo roster transactions are planned.

## Yahoo Fantasy Sports API usage

The application is intended to request only the data needed to:

1. Read league settings and the authorized user's roster.
2. Retrieve available free agents and waiver-wire players.
3. Retrieve player information and statistics needed for category comparisons.

API access will use **OAuth 2.0** and follow applicable Yahoo developer requirements and rate limits. Requests are expected to run approximately **once or twice per day**, with occasional user-initiated checks. Caching will be used where appropriate.

## Privacy and scope

- **Expected users:** 1 (the developer / account owner).
- **Distribution:** Private; not offered to the public.
- **Commercial use:** None. No subscriptions, advertising, or resale.
- **Access:** Only data authorized by the user's Yahoo account.
- **Permissions:** Read-only use is intended; no automated adds, drops, trades, or league-setting changes.
- **Data handling:** No sale or redistribution of Yahoo Fantasy data. OAuth credentials and tokens will not be committed to this repository.

## Development status

This repository is an initial public project description prepared for a Yahoo Fantasy Sports API access request. Source code and setup instructions may be added after access is approved and development begins.

## Disclaimer

Fantasy NBA Advisor is an independent personal project. It is **not affiliated with, endorsed by, or sponsored by Yahoo**. Yahoo and Yahoo Fantasy are trademarks of their respective owners.
