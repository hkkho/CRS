# XSEC-DECK-01 owner decision - official JEFF lattice deck basis

**Decision:** APPROVED

The project owner has authorized autonomous selection and execution of eligible
XSEC work. For the exact-deck conflict recorded in `XSEC-03.md`, the selected
candidate basis is the official Version5 JEFF-3.1/XMAS procedure `TCWUX11`, not
a project modification of the WLUP `TCWU11` procedure. The source procedure's
own numerical assertions remain intact and are regression guards; no assertion
value, tolerance, geometry, material, tracking, depletion, group handling, or
EDI lexeme may be edited by this decision.

The only allowed project deck deltas are omission of the two exact
`EDITION := SPH: EDITION VOLMATF INTLINF ;` lines and external decoding of the
source-created `res := EDITION` ASCII object. This authorizes no raw-data
retention, redistribution, runtime pack, Core/CLI/Unity integration, release,
publication, or golden claim. All raw JEFF/WLUP, source deck, executable, LCM,
and listing artifacts remain external under XSEC-RIGHTS-01.

This is a bounded technical owner decision, not an external legal opinion or a
numerical approval. XSEC-DECK-01 requires independent code review (high) before
XSEC-03 may run the v2 candidate.
