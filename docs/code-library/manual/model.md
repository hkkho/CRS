# What each day calculates

The browser presents one authoritative shared simulation. It does not calculate its own reactor rules. Daily planning freezes the state. After refuelling, one large twenty-four-hour step updates fuel burnup, energy, score and analytic poison using the post-refuelling power/flux. A final equilibrium and LZC solve produces the next day’s readings. Intermediate intraday states are not calculated.

The current axial model uses zero incoming partial current at both reactor ends (Marshak boundaries). End-bundle power falls sharply. Authored compact physics packs provide fuel coefficients and device geometry; external analysis programs are not runtime dependencies.

These are project-authored approximations for plausible gameplay, rather than plant-grade operating predictions. The simulator focuses on on-power refuelling; shutdown, scram, accidents and full plant operations are outside its scope.

Engineering edits mark a modified sandbox. Start a new standard run to restore its normal provenance.
