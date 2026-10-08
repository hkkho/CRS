"""Reproduce the before/after literature-fit assessment from versioned packs."""
from pathlib import Path
import json, hashlib, csv
import numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.patches import Circle, Rectangle
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, Image, PageBreak
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib import colors

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'output/pdf'
TMP = ROOT / 'tmp/pdfs'
OUT.mkdir(parents=True, exist_ok=True)
TMP.mkdir(parents=True, exist_ok=True)
PACK_PATH = ROOT / 'src/ReactorSim.Core/EmbeddedData/candu6-two-group-diffusion-pack-v1.json'
raw = PACK_PATH.read_bytes()
pack = json.loads(raw)
CAL = ROOT / 'data/calibration/literature-geometry-v4'
baseline = json.loads((CAL/'source-pack.json').read_text())
reference = json.loads((CAL/'reference.json').read_text())
core = json.loads((ROOT/'benchmarks/literature-geometry-v4-reactivity.json').read_text())
fit = json.loads((CAL/'fit/fit.json').read_text())
assert core['dataPack'] == pack['data_pack_version'], 'Core audit is for another pack'
assert (CAL/'fit/proposal.json').read_bytes() == raw, 'Active pack differs from the audited proposal'
rows = pack['coefficient_tables'][0]['rows']
xs = np.array([r['burnup_j_per_kg_hm']/8.64e10 for r in rows])
def values(b):
    if b < xs[0] or b > xs[-1]:
        raise ValueError('Outside active pack domain')
    return {k: float(np.interp(b, xs, [r[k] for r in rows])) for k in rows[0]}
def kinf(b):
    v = values(b)
    assert v['chi_group1'] == 1
    return (v['nu_fission_group1_per_m'] + v['nu_fission_group2_per_m'] * v['downscatter_group1_to_2_per_m']/v['absorption_group2_per_m'])/(v['absorption_group1_per_m'] + v['downscatter_group1_to_2_per_m'])
ks = np.array([kinf(b) for b in xs])
cross = next(xs[i] + (1-ks[i])*(xs[i+1]-xs[i])/(ks[i+1]-ks[i]) for i in range(len(xs)-1) if ks[i] > 1 and ks[i+1] < 1)
def mean_k(b):
    knots = np.r_[xs[xs < b], b]
    return float(np.trapezoid([kinf(x) for x in knots], knots)/b)

# Fig. 3, blue UO2-Zr curve, visually read on a 2000px page rendering.
# Fresh point is exact from Table 5; all later values are approximate +/-0.003.
days = np.array(reference['days'])
public_k = np.array(reference['k'])
public_b = days * .0319713
oldrows = baseline['coefficient_tables'][0]['rows']
oldb = [r['burnup_j_per_kg_hm']/8.64e10 for r in oldrows]
oldk = [(r['nu_fission_group1_per_m']+r['nu_fission_group2_per_m']*r['downscatter_group1_to_2_per_m']/r['absorption_group2_per_m'])/(r['absorption_group1_per_m']+r['downscatter_group1_to_2_per_m']) for r in oldrows]
old_cross = next(oldb[i]+(1-oldk[i])*(oldb[i+1]-oldb[i])/(oldk[i+1]-oldk[i]) for i in range(len(oldb)-1) if oldk[i]>1>oldk[i+1])
oldmean = float(np.trapezoid(np.interp(np.r_[np.array(oldb)[np.array(oldb)<7.1],7.1],oldb,oldk),np.r_[np.array(oldb)[np.array(oldb)<7.1],7.1])/7.1)
before_rmse = float(np.sqrt(np.mean((np.interp(public_b,oldb,oldk)-public_k)**2)))
after_rmse = float(np.sqrt(np.mean((np.array([kinf(b) for b in public_b])-public_k)**2)))
metrics = {'baseline_sha256':hashlib.sha256((CAL/'source-pack.json').read_bytes()).hexdigest(),
    'active_sha256':hashlib.sha256(raw).hexdigest(),'fresh_before':oldk[0],'fresh_after':kinf(0),
    'unity_crossing_before':old_cross,'unity_crossing_after':cross,
    'integral_at_7_1_before':oldmean,'integral_at_7_1_after':mean_k(7.1),
    'fit_target_rmse_before':before_rmse,'fit_target_rmse_after':after_rmse,
    'warning':'Fit residuals are in-sample agreement, not independent validation.', 'core_audit':core}
(OUT/'candu6-comparison.json').write_text(json.dumps(metrics,indent=2)+'\n')
with (OUT/'candu6-kinf-curve.csv').open('w',newline='') as f:
    w=csv.writer(f); w.writerow(['burnup_MWd_per_kg_HM','k_infinity','nuSigma_f1_per_m','nuSigma_f2_per_m','Sigma_f1_per_m','Sigma_f2_per_m'])
    for b in xs:
        v=values(b); w.writerow([b,kinf(b),v['nu_fission_group1_per_m'],v['nu_fission_group2_per_m'],v['fission_group1_per_m'],v['fission_group2_per_m']])

plt.rcParams.update({'font.family':'DejaVu Sans','font.size':10,'axes.spines.top':False,'axes.spines.right':False,'axes.titleweight':'bold','axes.labelcolor':'#243b53','text.color':'#243b53'})
navy='#17324d'; teal='#087f8c'; orange='#ca6a25'
def save(fig,name):
    fig.savefig(TMP/name,dpi=180,bbox_inches='tight',facecolor='white');plt.close(fig)

fig,axs=plt.subplots(1,2,figsize=(10.8,4.3),gridspec_kw={'width_ratios':[1,1.15]})
lengths=[6,12,14,16,18,18,20,20,22,22,22,22,22,22,20,20,18,18,16,14,12,6]
assert sum(lengths)==380
for row,n in enumerate(lengths):
    for col in range((22-n)//2,(22+n)//2):
        axs[0].add_patch(Rectangle((col-.42,row-.42),.84,.84,facecolor=teal,linewidth=0))
axs[0].set(xlim=(-1,22),ylim=(22,-1),aspect='equal',xlabel='Lattice column',ylabel='Display row',title='Runtime: 380 channel locations')
axs[0].set_xticks([0,5,10,15,21]);axs[0].set_yticks([0,5,10,15,21])
for i in range(12):
    axs[1].add_patch(Rectangle((i,0),1,.6,facecolor=teal if i<8 else '#b4d6d9',edgecolor='white'))
    axs[1].text(i+.5,.3,str(i+1),ha='center',va='center',color='white' if i<8 else navy)
axs[1].annotate('',xy=(12,-.3),xytext=(0,-.3),arrowprops={'arrowstyle':'<->','color':navy})
axs[1].text(6,-.55,'12 x 49.53 cm = 5.9436 m',ha='center')
axs[1].annotate('',xy=(8,.95),xytext=(0,.95),arrowprops={'arrowstyle':'->','color':orange,'lw':2})
axs[1].text(4,1.2,'Eight-bundle shift (illustrative direction)',ha='center',fontsize=9)
axs[1].text(6,-1.2,'One diffusion node per bundle position\nAdjacent channels have opposite flow',ha='center',fontsize=10)
axs[1].set(xlim=(-.5,12.5),ylim=(-1.8,1.8),title='Axial topology: 12 positions per channel');axs[1].axis('off')
fig.tight_layout();save(fig,'geometry-runtime.png')

fig,ax=plt.subplots(figsize=(6.5,5.7))
ax.add_patch(Rectangle((-14.2875,-14.2875),28.575,28.575,facecolor='#e7f3f5',edgecolor=navy,lw=1.5))
for radius,color in [(6.5875,'#9ea8b4'),(6.4478,'#faf4dc'),(5.6032,'#8894a3'),(5.1689,'#c9e6f0')]:
    ax.add_patch(Circle((0,0),radius,facecolor=color,edgecolor=navy,lw=.6))
for count,r,phase in [(1,0,0),(6,1.4885,0),(12,2.8755,np.pi/12),(18,4.3305,0)]:
    for a in phase+np.arange(count)*2*np.pi/count:
        xy=(r*np.cos(a),r*np.sin(a));ax.add_patch(Circle(xy,.654,facecolor='#818b98',edgecolor=navy,lw=.5));ax.add_patch(Circle(xy,.6122,facecolor='#d3a15c',lw=0))
ax.text(0,11,'D2O moderator',ha='center');ax.text(0,-11,'37 pins: 1 + 6 + 12 + 18',ha='center')
ax.set(xlim=(-15,15),ylim=(-15,15),aspect='equal',xlabel='cm',ylabel='cm',title='Public CANDU 6 lattice cell [2]')
fig.tight_layout();save(fig,'geometry-lattice.png')

fig,ax=plt.subplots(figsize=(10.4,4.4))
b=np.linspace(0,12,600);ax.plot(b,[kinf(x) for x in b],color=teal,lw=2.3,label='Active pack: bare infinite medium')
mask=xs<=12;ax.scatter(xs[mask],ks[mask],color=teal,s=25,zorder=3,label='Stored coefficient knots')
ax.axhline(1,color='#8894a3',ls='--');ax.axvline(cross,color=orange,ls=':',label=f'k = 1 at {cross:.3f} MWd/kg')
ax.set(xlim=(0,12),ylim=(.80,1.15),xlabel='Burnup (MWd/kg HM)',ylabel='k infinity',title='Updated literature-guided surrogate')
ax.grid(alpha=.15);ax.legend(loc='upper right',frameon=False,fontsize=9)
fig.tight_layout();save(fig,'kinf-model.png')

fig,ax=plt.subplots(figsize=(10.4,4.5))
b=np.linspace(0,10,500);ax.plot(b,[kinf(x) for x in b],lw=2.3,color=teal,label='Active pack: k infinity')
ax.plot(b,np.interp(b,oldb,oldk),color='#7b8794',lw=1.8,label='Previous v3 pack')
ax.plot(public_b,public_k,color=orange,ls='--',marker='o',ms=4,label='Published reflective lattice [2], graph-read')
ax.fill_between(public_b[1:],public_k[1:]-.003,public_k[1:]+.003,color=orange,alpha=.16,label='Graph reading allowance: +/-0.003')
ax.axhline(1,color='#8894a3',ls=':');ax.set(xlim=(0,10),ylim=(.85,1.15),xlabel='Burnup (MWd/kg HM; kg uranium in [2])',ylabel='Multiplication factor',title='Comparison with public CANDU 6 lattice calculation')
ax.grid(alpha=.15);ax.legend(frameon=False,fontsize=9)
fig.tight_layout();save(fig,'kinf-public.png')

styles=getSampleStyleSheet()
styles.add(ParagraphStyle(name='TitleReport',fontName='Helvetica-Bold',fontSize=26,leading=30,textColor=colors.HexColor(navy),spaceAfter=18))
styles.add(ParagraphStyle(name='Deck',fontName='Helvetica',fontSize=12,leading=17,textColor=colors.HexColor(teal),spaceAfter=16))
styles['Heading1'].textColor=colors.HexColor(navy);styles['Heading1'].fontSize=18;styles['Heading1'].leading=22
styles['Heading2'].textColor=colors.HexColor(teal);styles['Heading2'].fontSize=12;styles['Heading2'].spaceBefore=12
styles['BodyText'].fontSize=10;styles['BodyText'].leading=14;styles['BodyText'].spaceAfter=9
styles.add(ParagraphStyle(name='SmallReport',fontName='Helvetica',fontSize=8,leading=11,textColor=colors.HexColor('#526475'),spaceAfter=7))
story=[]
def p(text,style='BodyText'):story.append(Paragraph(text,styles[style]))
def h(text):p(text,'Heading1')
def sub(text):p(text,'Heading2')
def table(data,widths=None,font=9):
    wrapped=[[Paragraph(str(c),styles['SmallReport'] if font<=8 else styles['BodyText']) for c in row] for row in data]
    t=Table(wrapped,colWidths=widths,repeatRows=1,hAlign='LEFT')
    t.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),colors.HexColor('#dfedf1')),('VALIGN',(0,0),(-1,-1),'TOP'),('LEFTPADDING',(0,0),(-1,-1),7),('RIGHTPADDING',(0,0),(-1,-1),7),('TOPPADDING',(0,0),(-1,-1),5),('BOTTOMPADDING',(0,0),(-1,-1),3),('ROWBACKGROUNDS',(0,1),(-1,-1),[colors.white,colors.HexColor('#f4f7fa')]),('LINEBELOW',(0,0),(-1,0),.8,colors.HexColor(teal))]));story.append(t);story.append(Spacer(1,10))
def img(name,width=492):
    from PIL import Image as PILImage
    with PILImage.open(TMP/name) as im:w,hh=im.size
    story.append(Image(str(TMP/name),width=width,height=width*hh/w));story.append(Spacer(1,6))
def page():story.append(PageBreak())

p('CANDU 6<br/>Geometry and cross sections','TitleReport')
p('Updated data and repeated literature comparison | 7 October 2026','Deck')
h('What changed')
p('The active shared-simulation pack now uses the published lattice-cell volume and consistent metric interior coupling. Its thermal fission and neutron-production table is fitted to the reflective UO2-Zr lattice curve cited in the supplied report. Finite-core leakage and zone absorption were calibrated separately; the lattice curve was not renormalized to force core criticality.')
table([['Metric','Previous v3','Updated v4'],
 ['Node volume (m3)',f"{baseline['geometry']['node_volume_m3']:.8f}",f"{pack['geometry']['node_volume_m3']:.8f}"],
 ['Fresh k infinity',f'{oldk[0]:.6f}',f'{kinf(0):.6f}'],
 ['Descending k = 1 crossing (MWd/kg)',f'{old_cross:.4f}',f'{cross:.4f}'],
 ['Mean k, 0 to 7.1 MWd/kg',f'{oldmean:.6f}',f'{mean_k(7.1):.6f}'],
 ['RMSE at 15 curve-fit targets (k)',f'{before_rmse:.6f}',f'{after_rmse:.2e}']], [256,118,118],8)
sub('How to read the improved agreement')
p('The curve points are calibration targets. Near-zero residual at those points shows that the fit was implemented correctly; it is not independent validation. The source is a depletion calculation, not plant measurements. Its graph-read values carry an estimated +/-0.003 reading allowance. Absolute absorption, scattering and effective diffusion coefficients remain authored approximations.')
sub('Finite-core result')
p(f"Independent tight solves give k = {core['halfFillK']:.8f} at half-filled zones, {core['totalFourteenZoneWorthMk']:.4f} mk total zone worth, and {core['burnupLossMkPerFullPowerDay']:.4f} mk burnup-only loss per full-power day. The calibration solve peaks at {fit['trials'][-1]['peakChannel']/1e6:.3f} MW/channel and {fit['trials'][-1]['peakBundle']/1000:.1f} kW/bundle at 2,064 MW thermal.")
p(pack['data_pack_version'],'SmallReport')
p('Evidence class: synthetic-calibrated. Fast then thermal group order, SI units, 380 channels, 12 positions per channel and 30 MWd/kg closed lookup domain are retained.','SmallReport')

page();h('2 | Geometry and spatial coupling')
img('geometry-runtime.png')
p('Rouben [1, section 1.1] gives a 28.575 cm square lattice and 49.53 cm bundle length. The cell volume is therefore 0.28575 squared x 0.4953 = 0.04044276185625 m3. The previous 0.05 m3 value was 23.63% larger. The 4,560 lattice prisms now total 184.419 m3; this is not a calandria vessel volume.')
p('Interior conductances now obey C = D x face area / centre spacing in both directions. The effective D values of 0.04 m fast and 0.02 m thermal are authored coupling choices, not extracted from the cited paper. The direction ratio is fixed by the metric geometry. The stepped channel layout and bundle movement remain unchanged.')
g=pack['geometry']
table([['Conductance (m2)','Fast','Thermal'],
 *[[label,f"{g[key]['group1_m2']:.9f}",f"{g[key]['group2_m2']:.9f}"] for label,key in [('Axial interior','axial_edge_conductance_m2'),('Transverse interior','transverse_edge_conductance_m2'),('Effective outer face','vacuum_boundary_conductance_m2')]]], [240,126,126],8)
p('The outer-face value is fitted to seed-1001 half-fill criticality. It represents effective leakage/reflector loss; it is not a physical vacuum boundary or an explicit reflector mesh. Channel count agreement and corrected cell volume do not establish whole-core geometric fidelity.','SmallReport')

page();h('3 | Cross-section fit and limitations')
img('geometry-lattice.png',width=220)
p('Reference lattice [2, Tables 1-2]: 37 pins in rings of 1, 6, 12 and 18. Fuel/clad radii are 0.6122/0.6540 cm. Pressure-tube radii are 5.1689/5.6032 cm; calandria-tube radii 6.4478/6.5875 cm. The runtime continues to use homogenized nodes, not pin or tube meshes.','SmallReport')
table([['Quantity (m^-1)','Fresh','6.262136 MWd/kg'],
 *[[label,f'{values(0)[key]:.6f}',f'{values(6.2621359223300965)[key]:.6f}'] for label,key in [('Fast absorption','absorption_group1_per_m'),('Thermal absorption','absorption_group2_per_m'),('Fast-to-thermal scatter','downscatter_group1_to_2_per_m'),('Fast fission','fission_group1_per_m'),('Thermal fission','fission_group2_per_m'),('Fast neutron production','nu_fission_group1_per_m'),('Thermal neutron production','nu_fission_group2_per_m')]]],[260,116,116],8)
p('The fit retains absorption and downscatter and assumes an effective nu of 2.45 in both groups. Thermal nuSigma_f is solved from the analytic k equation; Sigma_f = nuSigma_f / 2.45. This is an underdetermined surrogate fit: k alone cannot identify all group constants. Energy per fission stays 200 MeV; chi_fast = 1.','SmallReport')

page();h('4 | Repeated burnup comparison')
img('kinf-public.png')
comparison=[['Burnup (MWd/kg)','Previous','Updated','Reference [2]']]
for t in [0,50,100,150,200,250,300]:
    bu=t*.0319713
    comparison.append([f'{bu:.4f}',f'{np.interp(bu,oldb,oldk):.6f}',f'{kinf(bu):.6f}',f'{np.interp(t,days,public_k):.6f}'])
table(comparison,[135,119,119,119],8)
p('Reference [2] uses DRAGON5 with JEFF-3.1, reflective boundaries and natural-uranium UO2-Zr at 31.9713 kW/kg. Fresh k = 1.118047 comes from Table 5; subsequent points were graph-read from Fig. 3 in the supplied report and visually checked against the original. Time converts as B = 0.0319713 x days. The updated curve interpolates those targets.','SmallReport')
p(f'The descending unity crossing moves from {old_cross:.4f} to {cross:.4f} MWd/kg. The updated integral average at 7.1 MWd/kg is {mean_k(7.1):.6f}, versus the separate IAEA value of 1.045 [3]. The residual {mean_k(7.1)-1.045:+.6f} is a cross-source scoping difference, not a matched-condition validation error.')

page();h('5 | What the model does and does not resolve')
sub('Analytic balance and interpolation')
p('<b>k infinity = [nuSigma_f1 + nuSigma_f2 x Sigma_s12 / Sigma_a2] / [Sigma_a1 + Sigma_s12]</b>')
p('The thermal-to-fast flux ratio in this bare homogeneous calculation is 1.25. Every cross section is interpolated linearly in burnup, so k is also piecewise linear here. The builder checks the formula independently using the two-group next-generation matrix eigenvalue at every knot. The companion CSV contains all 21 stored knots. Values beyond 9.59139 MWd/kg use a positive authored exponential tail sampled at knots; no literature accuracy is claimed there.')
sub('Poison basis is still a limitation')
p('The reference depletion curve includes changing nuclides and poisons. The gameplay xenon system retains its fixed startup rebase and dynamic perturbation. Consequently the fitted burnup curve plus live xenon is not a condition-matched depletion calculation: fresh-fuel poison effects can be counted in both the burnup fit and the dynamic perturbation. The bare-table overlay on page 4 excludes that perturbation. A poison-separated group library is needed before claiming realistic transient fuel response.')
sub('Finite-core calibration')
p('A bounded offline search changes effective boundary leakage to make the aged seed-1001 core critical at 50% zone fill. A separate zone-slope adjustment retains approximately 7 mk total worth. Interior geometry is fixed during the accepted fit. No post-fit production multiplier changes the reference lattice curve.')
table([['Independent core measurement','Updated value'],
 ['Half-fill k',f"{core['halfFillK']:.9f}"],
 ['Empty / full k',f"{core['emptyK']:.9f} / {core['fullK']:.9f}"],
 ['Burnup-only loss per full-power day',f"{core['burnupLossMkPerFullPowerDay']:.6f} mk"],
 ['Total zone worth',f"{core['totalFourteenZoneWorthMk']:.6f} mk"]],[278,214],8)
p('These measurements use a fixed initial power shape to deposit one day of thermal energy, fixed zones and no evolving xenon or refuelling. They do not establish a 100-day fuelling capability. The fuel mass, 190-day channel cycle and 6.262136 MWd/kg nominal mean exit target remain authored game settings.','SmallReport')

page();h('6 | Sources and reproduction')
sources=[
 ('1','B. Rouben, AECL, CANDU Fuel Management Course (2003), section 1.1, printed p. 1; fuel-management context in section 5.4.',reference['geometry_source']),
 ('2','A. Naceur and G. Marleau, Annals of Nuclear Energy 113 (2018), 147-161. Tables 1-2 and 5, Fig. 3, sections 2.3-2.4. DOI 10.1016/j.anucene.2017.11.016.',reference['curve_source']),
 ('3','IAEA-TECDOC-1319 (2002), printed p. 107 (PDF page 113): integral-average k = 1.045 at 7.1 MWd/kg for standard 37-element fuel. Separate cross-source comparator.','https://www-pub.iaea.org/MTCD/Publications/PDF/te_1319_web.pdf')]
for number,title,url in sources:p(f'[{number}] {title}<br/><link href="{url}" color="#087f8c">Open source</link>','SmallReport')
p('The supplied seven-page PDF is the baseline assessment. Its website required login; this revision used the attached PDF and its original public references. Source pages and the reference curve were checked on 6 October 2026.','SmallReport')
sub('Versioned inputs')
for line in ['data/calibration/literature-geometry-v4/source-pack.json: immutable previous pack.',
 'data/calibration/literature-geometry-v4/reference.json: source URLs, geometry and graph-read targets.',
 'data/calibration/literature-geometry-v4/fit: core-fit trials and accepted proposal.',
 'data/packs/candu6-two-group-diffusion-pack-v1.json: canonical runtime data; embedded mirror must match.',
 'benchmarks/literature-geometry-v4-reactivity.json: independent tight core measurement.']:
    p(line,'SmallReport')
sub('Rebuild sequence')
for line in ['python tools/tune_literature_pack.py',
 'dotnet run --project tools/AgedCoreBenchmark -c Release -- --fit-literature data/calibration/literature-geometry-v4/candidate.json data/calibration/literature-geometry-v4/fit',
 'After reviewed staging of the proposal and matching zone slopes: tools/Sync-PhysicsPacks.ps1 -Stage',
 'dotnet run --project tools/AgedCoreBenchmark -c Release -- --reactivity-scale benchmarks/literature-geometry-v4-reactivity.json',
 'python tools/build_candu6_summary_pdf.py']:
    p(line,'SmallReport')
p('Outputs: this PDF, candu6-kinf-curve.csv and candu6-comparison.json. Core/Game/Browser verification uses tools/Test-DotNet.ps1 and tools/Test-Browser.ps1; the local Pages-shaped build exercises the shared WASM path. See docs/physics/literature-geometry-v4.md for the recorded execution results.','SmallReport')
p('Active pack SHA-256:<br/>'+hashlib.sha256(raw).hexdigest(),'SmallReport')
p('Baseline pack SHA-256:<br/>'+metrics['baseline_sha256'],'SmallReport')

for row in rows:
    a1=row['absorption_group1_per_m']; a2=row['absorption_group2_per_m']; s=row['downscatter_group1_to_2_per_m']
    loss=np.array([[a1+s,0],[-s,a2]])
    fission=np.array([[row['nu_fission_group1_per_m'],row['nu_fission_group2_per_m']],[0,0]])
    eig=max(np.linalg.eigvals(np.linalg.solve(loss,fission)).real)
    assert abs(eig-kinf(row['burnup_j_per_kg_hm']/8.64e10))<1e-12
    assert a1>=row['fission_group1_per_m']>0 and a2>=row['fission_group2_per_m']>0
assert np.all(np.diff(xs)>0) and xs[-1]==30
assert abs(g['node_volume_m3']-.28575**2*.4953)<1e-14
assert after_rmse<1e-12

def footer(c,doc):
    c.setStrokeColor(colors.HexColor('#dfedf1'));c.line(51,40,543,40)
    c.setFont('Helvetica',8);c.setFillColor(colors.HexColor('#526475'))
    c.drawString(51,28,'CANDU 6 | Literature-guided revision | 7 October 2026');c.drawRightString(543,28,str(doc.page))
pdf=OUT/'candu6-geometry-cross-sections-burnup-report.pdf'
doc=SimpleDocTemplate(str(pdf),pagesize=(595.28,841.89),rightMargin=51,leftMargin=51,topMargin=48,bottomMargin=53,title='CANDU 6 - updated geometry and cross-section comparison',author='Repository model assessment')
doc.build(story,onFirstPage=footer,onLaterPages=footer)
print(json.dumps(metrics,indent=2))
