# P6-T04 owner approval

APPROVED: P6-T04-SYNTHETIC-BULK-POISON-MAP-V1
OWNER: Kevin Ho
SCOPE: Synthetic test-only engine-neutral Core / ReducedModel P6-T04 state,
       moderator volume, derived concentration, map binding, separate
       rate-limited add and slow withdraw/cleanup actions, setup limits,
       exact mass accounting, and deterministic local overlay/rollback
       evidence; no production CANDU, external reference, or golden-data claim.
SIGN: Approved as written: positive synthetic absorption overlay for
      concentration above reference; mass 0.5 kg, moderator volume 5.0 m^3,
      reference concentration 0.0 kg/m^3, add rate 0.1 kg/s, withdraw rate
      0.01 kg/s, setup maximum mass 1.0 kg, six explicit target nodes, two
      groups, and the 12 proposed map entries.
IDENTITY: Approved as written: FixtureId P6-T04-SYNTHETIC-BULK-POISON-MAP-V1,
          DataVersion p6-t04-synthetic-bulk-poison-map-v1, MapId
          00000000-0000-0000-0000-00000000b401, PoisonSourceId
          00000000-0000-0000-0000-00000000b405, TopologyId
          00000000-0000-0000-0000-00000000b404, and deterministic digest
          generation/review after approval.
BOUNDARY: P6-T04 may validate poison-owned state from the complete supplied
          state and local state/map scratch rollback. It may not allocate,
          enqueue, consume, transition, or roll back shared queue state;
          queue allocation/transition/rollback remains reserved for P6-T06,
          and scenario packages remain reserved for P6-T07. No hidden
          chemistry, decay, transport, cleanup, shutdown, trip, safety,
          production, external-reference, or golden-data behavior.
DATE: 2026-08-20
