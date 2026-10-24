# Eight in, eight out

Each channel holds twelve axial bundle positions, numbered from End A to End B. A move inserts eight fresh bundles with the channel’s flow and discharges eight at the opposite end. The other four shift along the channel. Adjacent channels have opposite flow directions.

The preview marks outgoing positions **OUT** in gold and retained positions **KEEP** in teal. Numbers show position and burnup in MWd/kg HM. Hover a position for its destination. Confirmed reports retain bundle identities, old and new positions, and discharge burnup.

Adding a channel uses no fuel or time. Remove it with the same button, remove an individual plan entry, or clear the whole plan. Moves execute in displayed channel order before the simulation advances twenty-four hours.

The paper fuelling sheet fills as you add channels and removes rows as you deselect
them. Its numbered order matches execution, rather than selection order. Flow
comes from each channel; exit burnup comes from its eight published outgoing
positions. The sheet is a draft until you advance. The channel locator and
confirmed-movement ring are visual feedback, not additional machine controls.

A channel containing nonfuel cells cannot be refuelled. A finite-stock plan must have eight fresh bundles available per channel.

## Choosing fuel

Burnup measures thermal energy extracted per kilogram of heavy metal. Older fuel generally contributes less reactivity. Fresh fuel can raise local power and alter the core shape; inspect both the channel and hottest bundle. Average channel burnup alone does not describe the eight outgoing bundles.
