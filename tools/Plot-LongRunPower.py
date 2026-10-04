"""Render and independently validate authoritative LongRunPlaytest measurements.

Usage: python tools/Plot-LongRunPower.py tmp/longrun-maps
Requires matplotlib and numpy; never supplies reactor rules to the runtime.
"""
import csv
import json
import sys
from pathlib import Path

import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
INPUT = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / 'tmp/longrun-maps'
OUT = ROOT / 'docs/gameplay/figures'
OUT.mkdir(parents=True, exist_ok=True)
ROWS = list('ABCDEFGHJKLMNOPQRSTUVW')
CAMPAIGNS = [
    ('idle', 'No refuelling · seed 1001'),
    ('oldest', 'Highest burnup · seed 1001'),
    ('reserve', 'Reserve policy · seed 1001'),
    ('low-power', 'Reserve policy · 80% power'),
    ('seed1002', 'Reserve policy · seed 1002'),
    ('seed1013', 'Reserve policy · seed 1013'),
    ('extended-fuel', 'Historical extended fuel · 101 days'),
    ('endless', 'Endless main game · 101 days'),
]
reports = []
all_channels = []
for key, label in CAMPAIGNS:
    data = json.loads((INPUT / (key + '.json')).read_text())
    analysis = data['powerAnalysis']
    channels = analysis['channels']
    assert len(channels) == 380
    assert len({(c['gridRow'], c['gridColumn']) for c in channels}) == 380
    timeline = analysis['timeline']
    times = np.array([p['day'] * 86400 for p in timeline])
    seconds = np.diff(times)
    assert np.all(seconds >= 0)
    assert abs(seconds.sum() - data['final']['SimulationTimeSeconds']) < 1e-6
    for field, total in [
        ('maxAbsoluteRipplePercent', 'timeAverageMaximumAbsoluteRipplePercent'),
        ('maxPositiveRipplePercent', 'timeAverageMaximumPositiveRipplePercent'),
        ('maxPowerReferencePercent', 'timeAverageMaximumPowerReferencePercent'),
        ('rmsRipplePercent', 'timeAverageRmsRipplePercent')]:
        weighted = np.dot(seconds, [p[field] for p in timeline[:-1]]) / seconds.sum()
        assert abs(weighted - analysis[total]) < 1e-8, (key, field, weighted, analysis[total])
    mean_max = np.mean([c['maximumAbsoluteRipplePercent'] for c in channels])
    assert abs(mean_max - analysis['meanOfChannelTemporalMaximumAbsoluteRipplePercent']) < 1e-9
    peak = max(c['maximumPowerWatts'] for c in channels) / 1000
    assert abs(peak - max(p['maximumChannelPowerKw'] for p in timeline)) < 1e-8
    # For every constant-target campaign, sampled RMS must reproduce Game's
    # nonlinear operating-point integral, rather than just match our averages.
    rms = np.array([p['rmsRipplePercent'] / 100 for p in timeline[:-1]])
    score = np.dot(seconds / 3600, 1 / (1 + 100 * rms * rms))
    assert abs(score - data['final']['ScoreTotal']) < 1e-6, (key, score)
    summary = dict(campaign=key, title=label, seed=data['seed'], powerTarget=data['power'],
        finalDay=data['final']['day'], runStatus=data['final']['RunStatus'],
        refuellingOperations=data['final']['RefuellingOperationCount'],
        maximumChannelPowerKw=peak,
        **{k: v for k, v in analysis.items() if k not in ('timeline', 'channels')}, channels=channels)
    reports.append(summary)
    all_channels.extend(dict(campaign=key, channelLabel=f"{ROWS[c['gridRow']]}{c['gridColumn']+1:02}", **c) for c in channels)
    print(f"{key}: day {summary['finalDay']:.6f}; peak {peak:.3f} kW; mean maximum absolute ripple {summary['timeAverageMaximumAbsoluteRipplePercent']:.6f}%")

plt.rcParams.update({'font.family': 'DejaVu Sans', 'font.size': 9, 'axes.titleweight': 'bold'})
def draw_map(ax, channels, field, vmax, cmap):
    matrix = np.full((22, 22), np.nan)
    for channel in channels:
        matrix[channel['gridRow'], channel['gridColumn']] = channel[field]
    if field == 'maximumPowerWatts':
        matrix /= 1000
    picture = ax.imshow(matrix, origin='upper', vmin=0, vmax=vmax, cmap=cmap, interpolation='nearest')
    ax.set_xticks(range(0, 22, 3), range(1, 23, 3))
    ax.set_yticks(range(0, 22, 3), ROWS[::3])
    ax.set_xticks(np.arange(-.5, 22, 1), minor=True)
    ax.set_yticks(np.arange(-.5, 22, 1), minor=True)
    ax.grid(which='minor', color='white', linewidth=.25)
    ax.tick_params(which='minor', bottom=False, left=False)
    ax.set_xlabel('Channel column'); ax.set_ylabel('Channel row')
    return picture

fig, axes = plt.subplots(2, 4, figsize=(16, 9), layout='constrained')
for ax, report in zip(axes.flat, reports):
    picture = draw_map(ax, report['channels'], 'maximumPowerWatts', 7300, 'turbo')
    ax.set_title(f"{report['title']}\n{report['finalDay']:.2f} days · peak {report['maximumChannelPowerKw']:.0f} kW", fontsize=10)
fig.colorbar(picture, ax=axes, label='Maximum observed channel thermal power (kW)', shrink=.75)
fig.suptitle('Channel power maxima throughout each campaign', fontsize=20)
fig.supxlabel('Each cell is its own temporal maximum; these powers do not form a simultaneous core state. Shared scale: 0–7,300 kW.')
for suffix in ('png', 'svg'):
    fig.savefig(OUT / ('maximum-channel-power-maps-v1.' + suffix), dpi=180)
plt.close(fig)

main = reports[-1]
timeline = json.loads((INPUT / 'endless.json').read_text())['powerAnalysis']['timeline']
fig, axes = plt.subplots(1, 3, figsize=(16, 5), layout='constrained')
p = draw_map(axes[0], main['channels'], 'maximumPowerWatts', 7300, 'turbo')
axes[0].set_title('101-day endless run · channel maxima')
fig.colorbar(p, ax=axes[0], label='Thermal power (kW)', shrink=.75)
ripple_max = max(c['maximumAbsoluteRipplePercent'] for c in main['channels'])
p = draw_map(axes[1], main['channels'], 'maximumAbsoluteRipplePercent', ripple_max, 'magma')
axes[1].set_title('Each channel’s maximum absolute ripple')
fig.colorbar(p, ax=axes[1], label='Absolute deviation from reference (%)', shrink=.75)
axes[2].plot([p['day'] for p in timeline], [p['maxAbsoluteRipplePercent'] for p in timeline], lw=.8, label='Maximum absolute ripple')
axes[2].plot([p['day'] for p in timeline], [p['rmsRipplePercent'] for p in timeline], lw=.9, label='RMS ripple')
axes[2].axhline(main['timeAverageMaximumAbsoluteRipplePercent'], ls='--', color='black', lw=1,
    label=f"Time-average maximum: {main['timeAverageMaximumAbsoluteRipplePercent']:.3f}%")
axes[2].set(xlabel='Simulated day', ylabel='Deviation from reference (%)', title='Ripple throughout the run', xlim=(0, 101))
axes[2].grid(alpha=.2); axes[2].legend(fontsize=8)
for suffix in ('png', 'svg'):
    fig.savefig(OUT / ('endless-channel-power-ripple-v1.' + suffix), dpi=180)
plt.close(fig)

benchmark = ROOT / 'benchmarks/channel-power-maps-v1.json'
benchmark.write_text(json.dumps(dict(schema='candu-channel-power-maps-v1',
    measuredOn='2026-10-04', sourceBaseCommit='5a0e1da6fd092252192cfc821169e51a8338c60d',
    sourceConfiguration='Base physics unchanged; working-tree endless main-game configuration for the endless campaign.',
    diffusionDataPackVersion='candu6-two-group-diffusion-v1-cycle190-650mwe-powerlimits-v2',
    method='Authoritative Game snapshots every half hour and after each refuel; time-weighted accepted powers; zero-time moves do not bias averages.',
    campaigns=reports), indent=2) + '\n')
with benchmark.with_suffix('.csv').open('w', newline='') as handle:
    writer = csv.DictWriter(handle, fieldnames=list(all_channels[0]))
    writer.writeheader(); writer.writerows(all_channels)
print('Validated timeline integrals, all 380-channel maps, and every Game score integral. Wrote maps, JSON and CSV.')
