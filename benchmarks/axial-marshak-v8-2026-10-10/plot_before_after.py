import json
from pathlib import Path
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt

root=Path(__file__).resolve().parent
before=json.loads((root.parent/'bundle-absorption-2026-10-10'/'bundle-power-adjusters.json').read_text())
after=json.loads((root/'bundle-power-adjusters.json').read_text())
old=[r['adjustersInKw'] for r in before['bundles']]
new=[r['adjustersInKw'] for r in after['bundles']]
assert [r['bundleId'] for r in before['bundles']]==[r['bundleId'] for r in after['bundles']]
fig,ax=plt.subplots(figsize=(11,5.8))
ax.plot(range(1,13),old,'o-',color='#a65b4a',linewidth=2,label=f'Previous v7 · ends {100*old[0]/max(old):.1f}–{100*old[-1]/max(old):.1f}% of peak')
ax.plot(range(1,13),new,'o-',color='#1a7b6f',linewidth=2.6,label=f'Implemented v8 · ends {100*new[0]/max(new):.1f}–{100*new[-1]/max(new):.1f}% of peak')
ax.set(title='Implemented axial zero incoming current — M11, adjusters inserted',xlabel='Bundle position · End A → End B',ylabel='Bundle power (kW thermal)',xlim=(.7,12.3),ylim=(0,800))
ax.set_xticks(range(1,13));ax.grid(axis='y',alpha=.2);ax.spines[['top','right']].set_visible(False)
ax.legend(loc='upper left',frameon=False)
fig.text(.08,.02,'Same seed-1001 aged fuel and 2,064 MW thermal. Each pack uses its fitted devices and equilibrium xenon.\n'
         'This compares the playable packs after refitting, rather than changing the boundary alone.',fontsize=9,color='#475569')
fig.subplots_adjust(left=.09,right=.98,top=.91,bottom=.18)
fig.savefig(root/'m11-before-after.png',dpi=180,facecolor='white')
fig.savefig(root/'m11-before-after.svg',facecolor='white')
print('M11 v8 end fractions',new[0]/max(new),new[-1]/max(new))
