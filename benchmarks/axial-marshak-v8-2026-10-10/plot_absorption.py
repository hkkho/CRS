"""Plot authoritative bundle-averaged absorption; no reactor calculations here."""
import csv
import json
from pathlib import Path

import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import numpy as np

root = Path(__file__).resolve().parent
report = json.loads((root / 'bundle-absorption.json').read_text())
rows = report['bundles']
assert len(rows) == 12
assert [r['bundlePosition'] for r in rows] == list(range(1, 13))
assert len(report['coefficientBindingDigest']) == 64
for row in rows:
    assert 0 <= row['burnupMwdPerKg'] < 20
    for group in ['Fast', 'Thermal']:
        components = [row[f'{name}{group}PerM'] for name in ['fuelBackground', 'xenon', 'adjuster', 'lzc']]
        assert all(np.isfinite(v) and v >= 0 for v in components)
        assert abs(sum(components) - row[f'total{group}PerM']) < 1e-12
with (root / 'bundle-absorption.csv').open('w', newline='') as handle:
    writer = csv.DictWriter(handle, fieldnames=list(rows[0]))
    writer.writeheader()
    writer.writerows(rows)

plt.rcParams.update({'font.family': 'DejaVu Sans', 'font.size': 11,
                     'axes.spines.top': False, 'axes.spines.right': False,
                     'svg.fonttype': 'none'})
fig, axes = plt.subplots(2, 2, figsize=(14, 9), sharex=True)
x = np.arange(1, 13)
edges = np.arange(.5, 13.5)
colors = {'background': '#64748b', 'adjuster': '#d94b3d', 'lzc': '#008d9d', 'total': '#172a46'}
for col, (group, title) in enumerate([('Fast', 'Group 1 · fast neutrons'), ('Thermal', 'Group 2 · thermal neutrons')]):
    background = np.array([r[f'fuelBackground{group}PerM'] + r[f'xenon{group}PerM'] for r in rows])
    adjuster = np.array([r[f'adjuster{group}PerM'] for r in rows])
    lzc = np.array([r[f'lzc{group}PerM'] for r in rows])
    total = np.array([r[f'total{group}PerM'] for r in rows])
    ax = axes[0, col]
    ax.stairs(background, edges, baseline=None, color=colors['background'], linestyle='--', linewidth=2, label='Fuel background + actual xenon')
    ax.stairs(background + adjuster, edges, baseline=None, color=colors['adjuster'], linestyle=':', linewidth=2, label='Background + adjusters')
    ax.stairs(background + lzc, edges, baseline=None, color=colors['lzc'], linestyle='-.', linewidth=2, label='Background + LZC water')
    ax.stairs(total, edges, baseline=None, color=colors['total'], linewidth=2.3, label='Total solver absorption')
    ax.scatter(x, total, color=colors['total'], s=18, zorder=5)
    low, high = min(background), max(total)
    padding = max((high - low) * .15, .00015)
    ax.set_ylim(low - padding, high + padding)
    ax.set_title(title + '\nTotal macroscopic absorption', fontweight='bold', loc='left', pad=10)
    ax.set_ylabel(r'$\Sigma_a$ (m$^{-1}$) · zoomed scale')
    ax.grid(axis='y', alpha=.2)
    ax.ticklabel_format(axis='y', style='plain', useOffset=False)

    ax = axes[1, col]
    ax.bar(x - .18, adjuster, width=.34, color=colors['adjuster'], label='Adjuster contribution')
    ax.bar(x + .18, lzc, width=.34, color=colors['lzc'], label='LZC-water contribution')
    for rod in report['adjusters']:
        ax.axvline(rod['axialBundleCoordinate'], color=colors['adjuster'], linestyle=':', alpha=.3, zorder=0)
    for tube in report['lzcCompartments']:
        ax.axvline(tube['axialBundleCoordinate'], color=colors['lzc'], linestyle=':', alpha=.3, zorder=0)
    ax.set_ylim(0, max(max(adjuster), max(lzc)) * 1.25)
    ax.set_title('Device absorption only', fontweight='bold', loc='left', pad=10)
    ax.set_ylabel(r'Added $\Delta\Sigma_a$ (m$^{-1}$)')
    ax.set_xlabel('Bundle position · End A → End B')
    ax.grid(axis='y', alpha=.2)
    ax.set_axisbelow(True)
    ax.legend(fontsize=10, loc='upper center', ncol=2, frameon=False)
    for panel in axes[:, col]:
        panel.set_xlim(.4, 12.6)
        panel.set_xticks(x)

zones = ' / '.join(f"Z{t['zone']} {t['fill'] * 100:.2f}%" for t in report['lzcCompartments'])
fig.suptitle(f"Bundle absorption beside adjusters and liquid-zone controllers\nChannel {report['channelLabel']} · seed {report['seed']} · initial aged core · {zones}",
             fontsize=17, fontweight='bold', y=.98)
handles, labels = axes[0, 0].get_legend_handles_labels()
fig.legend(handles, labels, loc='upper center', bbox_to_anchor=(.5, .90), ncol=2, frameon=False, fontsize=10)
fig.text(.06, .022, 'Exact v8 solver coefficients averaged over fuel/moderator cells; interstitial devices do not replace fuel.\n'
         'Fuel background removes the included xenon reference; actual xenon is added once. Dotted vertical guides mark device centres.', fontsize=10, color='#475569')
fig.subplots_adjust(left=.075, right=.98, top=.78, bottom=.12, hspace=.42, wspace=.22)
fig.savefig(root / 'bundle-absorption.png', dpi=180, facecolor='white')
fig.savefig(root / 'bundle-absorption.svg', facecolor='white')
print(f"Validated 12 bundle component sums; plotted {report['channelLabel']}; max error {max(r['componentSumErrorPerM'] for r in rows):.3g} m^-1")
