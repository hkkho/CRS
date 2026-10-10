"""Plot exact fixed-state Core solves with all adjusters in and out."""
import csv
import json
from pathlib import Path

import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import numpy as np

root = Path(__file__).resolve().parent
report = json.loads((root / 'bundle-power-adjusters.json').read_text())
absorption = json.loads((root / 'bundle-absorption.json').read_text())
rows = report['bundles']
assert report['seed'] == absorption['seed']
assert report['channelIndex'] == absorption['channelIndex']
assert report['simulationTimeSeconds'] == 0
assert len(rows) == 12 and [r['bundlePosition'] for r in rows] == list(range(1, 13))
for row, original in zip(rows, absorption['bundles']):
    assert row['bundleId'] == original['bundleId']
    assert abs(row['burnupMwdPerKg'] - original['burnupMwdPerKg']) < 1e-12
    assert np.isfinite(row['adjustersInKw']) and row['adjustersInKw'] > 0
    assert np.isfinite(row['adjustersOutKw']) and row['adjustersOutKw'] > 0
for key in ['insertedCorePowerWatts', 'withdrawnCorePowerWatts']:
    assert abs(report[key] - 2_064_000_000) < .01
assert abs(sum(r['adjustersInKw'] for r in rows) - report['insertedChannelKw']) < 1e-9
assert abs(sum(r['adjustersOutKw'] for r in rows) - report['withdrawnChannelKw']) < 1e-9
with (root / 'bundle-power-adjusters.csv').open('w', newline='') as handle:
    writer = csv.DictWriter(handle, fieldnames=list(rows[0]))
    writer.writeheader()
    writer.writerows(rows)

plt.rcParams.update({'font.family': 'DejaVu Sans', 'font.size': 11,
                     'axes.spines.top': False, 'axes.spines.right': False,
                     'svg.fonttype': 'none'})
fig, (ax, change) = plt.subplots(2, 1, figsize=(12, 8.3), sharex=True,
                                gridspec_kw={'height_ratios': [2.4, 1]})
x = np.arange(1, 13)
power_in = np.array([r['adjustersInKw'] for r in rows])
power_out = np.array([r['adjustersOutKw'] for r in rows])
for panel in [ax, change]:
    panel.axvspan(4.5, 8.5, color='#e9b8b0', alpha=.27,
                  label='Adjuster-adjacent cells' if panel is ax else None)
    for start, end in [(2.5, 4.5), (8.5, 10.5)]:
        panel.axvspan(start, end, color='#9cdee2', alpha=.27,
                      label='LZC-adjacent cells' if panel is ax and start == 2.5 else None)
    panel.grid(axis='y', alpha=.2)
    panel.set_xlim(.6, 12.4)
    panel.set_xticks(x)
    panel.set_axisbelow(True)
ax.plot(x, power_in, '-o', color='#245ea4', linewidth=2.5, markersize=6,
        label=f"Adjusters IN · channel {report['insertedChannelKw'] / 1000:.3f} MW")
ax.plot(x, power_out, '-^', color='#cc562d', linewidth=2.5, markersize=6,
        label=f"Adjusters OUT · channel {report['withdrawnChannelKw'] / 1000:.3f} MW")
low, high = min(power_in.min(), power_out.min()), max(power_in.max(), power_out.max())
padding = (high - low) * .12
ax.set_ylim(max(0, low - padding), high + padding)
ax.set_ylabel('Bundle power (kW thermal)')
handles, labels = ax.get_legend_handles_labels()
fig.legend(handles[2:] + handles[:2], labels[2:] + labels[:2], loc='upper center',
           bbox_to_anchor=(.5, .89), ncol=2, frameon=False, fontsize=10)

depression = np.array([r['insertedDepressionPercent'] for r in rows])
change.bar(x, depression, width=.58, color='#647b99')
change.axhline(0, color='#64748b', linewidth=.9)
change.set_ylabel('Power reduction\nwith adjusters IN (%)')
change.set_xlabel('Bundle position · End A → End B')
change.set_ylim(min(0, depression.min() * 1.25), max(1, depression.max() * 1.25))
for position, value in zip(x, depression):
    change.annotate(f'{value:.1f}%', (position, value if value >= 0 else value / 2),
                    xytext=(0, 4 if value >= 0 else 0), textcoords='offset points',
                    ha='center', va='bottom' if value >= 0 else 'center',
                    color='#172a46' if value >= 0 else 'white', fontsize=9)

zones = ' / '.join(f"Z{t['zone']} {t['fill'] * 100:.2f}%" for t in report['lzcCompartments'])
fig.suptitle(f"Bundle power — channel {absorption['channelLabel']}\nAll 21 adjusters inserted versus removed · seed {report['seed']} · {zones}",
             fontsize=17, fontweight='bold', y=.98)
fig.text(.085, .025, 'Identical aged fuel, frozen xenon and LZC levels; both static solves normalized to 2,064 MW thermal.\n'
         'Points are bundle averages; lines connect them. Reduction = 100 × (1 − power IN / power OUT).',
         fontsize=10, color='#475569')
fig.subplots_adjust(left=.10, right=.975, top=.79, bottom=.13, hspace=.19)
fig.savefig(root / 'bundle-power-adjusters.png', dpi=180, facecolor='white')
fig.savefig(root / 'bundle-power-adjusters.svg', facecolor='white')
print(f"Validated matched fuel and equal core powers. Adjacent-bundle reduction: {report['directlyOverlappingBundleDepressionPercent']:.4f}%.")
