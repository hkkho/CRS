"""Compare computed M11 profiles; no display taper or altered runtime pack."""
import csv
import json
from pathlib import Path

import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import numpy as np

root = Path(__file__).resolve().parent
r = json.loads((root / 'axial-boundary-audit.json').read_text())
assert len(r['results']) == 8
styles = {
    'current': ('Current fitted boundary', '#bd4b31', '-'),
    'zero-incoming-current': ('Zero incoming current (Marshak)', '#275da8', '-'),
    'extrapolation-0.25m': ('Extrapolation 0.25 m (illustrative)', '#238875', '--'),
    'extrapolation-0.50m': ('Extrapolation 0.50 m (illustrative)', '#8a65b1', '-.')
}
plt.rcParams.update({'font.family':'DejaVu Sans', 'font.size':11,
                     'axes.spines.top':False, 'axes.spines.right':False, 'svg.fonttype':'none'})
fig, axes = plt.subplots(1, 2, figsize=(14, 6), sharey=True)
for ax, mode in zip(axes, ['in', 'out']):
    for row in r['results']:
        assert np.isfinite(row['bundleKw']).all() and min(row['bundleKw']) > 0
        assert row['residual'] <= 2e-7
        assert abs(row['totalCoreWatts'] - 2_064_000_000) < .01
        if row['adjusters'] != mode: continue
        values = np.array(row['bundleKw']) / max(row['bundleKw']) * 100
        label, color, line = styles[row['variant']]
        ax.plot(range(1,13), values, marker='o', markersize=4, color=color, linestyle=line, linewidth=2, label=label)
    ax.set_title(f'All 21 adjusters {mode.upper()}', fontweight='bold', loc='left')
    ax.set_xlim(.6,12.4); ax.set_ylim(0,108); ax.set_xticks(range(1,13))
    ax.set_xlabel('Bundle position · End A → End B'); ax.grid(axis='y', alpha=.2)
axes[0].set_ylabel('Bundle power / that profile’s peak (%)')
handles, labels = axes[0].get_legend_handles_labels()
fig.legend(handles, labels, loc='upper center', bbox_to_anchor=(.5,.89), ncol=2, frameon=False, fontsize=10)
fig.suptitle('Axial end leakage sensitivity — M11, seed 1001', fontsize=17, fontweight='bold', y=.985)
fig.text(.07,.022,'Actual fixed-fuel/Xe/LZC full-core solves, each at 2,064 MW thermal. Radial boundaries unchanged.\n'
         'Stronger end leakage changes criticality; these are offline shape diagnostics, not a refitted playable pack.', fontsize=10, color='#475569')
fig.subplots_adjust(left=.075,right=.98,top=.74,bottom=.16,wspace=.10)
fig.savefig(root/'axial-boundary-comparison.png',dpi=180,facecolor='white')
fig.savefig(root/'axial-boundary-comparison.svg',facecolor='white')
with (root/'axial-boundary-bundles.csv').open('w',newline='') as f:
    writer=csv.writer(f);writer.writerow(['variant','adjusters','bundlePosition','powerKw','powerPercentOfPeak'])
    for row in r['results']:
        for i,value in enumerate(row['bundleKw']): writer.writerow([row['variant'],row['adjusters'],i+1,value,100*value/max(row['bundleKw'])])
print('Validated eight converged equal-power solves and plotted their actual end falloff.')
