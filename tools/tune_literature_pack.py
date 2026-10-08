"""Build a source-constrained surrogate candidate; runtime promotion requires core checks.

The k curve does not identify a unique set of group constants. This is a fit,
not a transport/depletion calculation. See the adjacent calibration manifest.
"""
import copy
import hashlib
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'data/calibration/literature-geometry-v4'
DAYS = [0, 5, 10, 20, 30, 40, 50, 70, 90, 110, 130, 150, 200, 250, 300]
K = [1.118047, 1.073, 1.073, 1.077, 1.079, 1.079, 1.077,
     1.069, 1.061, 1.051, 1.041, 1.030, 1.003, .978, .954]

def build():
    source_bytes = (OUT / 'source-pack.json').read_bytes()
    if hashlib.sha256(source_bytes).hexdigest() != '5af72ffd047c91238772d33a22aa7d123d1613895db74431ed70614834a23043':
        raise ValueError('The archived baseline differs from the supplied PDF baseline')
    source = json.loads(source_bytes)
    pack = copy.deepcopy(source)
    provenance = ('synthetic-calibrated; literature-guided surrogate v4; geometry from Rouben 2003 section 1.1: '
        'pitch 0.28575 m, bundle length 0.4953 m; volume=pitch^2*length; isotropic interior C=D*A/d; '
        'Naceur and Marleau 2018 DOI https://doi.org/10.1016/j.anucene.2017.11.016 Table 5 and Fig 3 UO2-Zr '
        'reflective lattice k fit, 31.9713 kW/kg; post-fresh points graph-read +/-0.003; '
        'not measured or condition-matched group constants; effective nu=2.45 authored; '
        'absorption and downscatter retained; poison-inclusive depletion reference, runtime xenon '
        'remains a rebased gameplay perturbation, not matched depletion; positive authored tail to 30 MWd/kg')
    pack['data_pack_version'] = 'candu6-two-group-diffusion-v1-literature-geometry-v4'
    pack['source_provenance'] = provenance
    pack['source_toolchain'] = 'tools/tune_literature_pack.py; tools/AgedCoreBenchmark --fit-literature'
    table = pack['coefficient_tables'][0]
    table['data_version'] = 'synthetic-literature-guided-natural-uranium-v6'
    table['source_provenance'] = provenance
    rows = []
    points = [(t * .0319713, k) for t, k in zip(DAYS, K)]
    # Retain old tail coordinates beyond the reference domain, with a new positive extension.
    for old in source['coefficient_tables'][0]['rows']:
        b = old['burnup_j_per_kg_hm'] / 8.64e10
        if b > points[-1][0]:
            # Smooth positive exponential extension; no claim of literature coverage.
            points.append((b, .954 * math.exp(-.018 * (b - 300 * .0319713))))
    for b, k in points:
        row = copy.deepcopy(table['rows'][0])
        row['burnup_j_per_kg_hm'] = b * 8.64e10
        row['nu_fission_group1_per_m'] = 2.45 * row['fission_group1_per_m']
        row['nu_fission_group2_per_m'] = (k * (.3 + .2) - row['nu_fission_group1_per_m']) / (.2 / .16)
        row['fission_group2_per_m'] = row['nu_fission_group2_per_m'] / 2.45
        assert 0 < row['fission_group2_per_m'] < row['absorption_group2_per_m']
        rows.append(row)
    table['rows'] = rows
    table.pop('checksum')
    table['checksum'] = hashlib.sha256(json.dumps(table, sort_keys=True, separators=(',', ':'), ensure_ascii=False).encode()).hexdigest()
    p, length = .28575, .4953
    geometry = pack['geometry']
    geometry['node_volume_m3'] = p*p*length
    # Initial effective D values; the offline core fit records any coupling changes.
    for group, d in [(1, .04), (2, .02)]:
        geometry['axial_edge_conductance_m2'][f'group{group}_m2'] = d*p*p/length
        geometry['transverse_edge_conductance_m2'][f'group{group}_m2'] = d*length
    (OUT / 'candidate.json').write_text(json.dumps(pack, indent=2) + '\n', encoding='utf-8', newline='\r\n')
    (OUT / 'reference.json').write_text(json.dumps({
        'geometry_source': 'https://www.nuceng.ca/canteach-rev2/library/20031101.pdf',
        'curve_source': 'https://publications.polymtl.ca/5047/11/2018_Naceur_Neutronic_analysis_accident_tolerant_cladding.pdf',
        'pitch_m': p, 'bundle_length_m': length, 'days': DAYS, 'k': K,
        'specific_power_MW_per_kg': .0319713,
        'reading_allowance_k': .003,
        'basis': 'Table 5 fresh value exact; Fig 3 UO2-Zr points graph-read, reused from supplied PDF and visually checked. Fit targets, not validation data.'
    }, indent=2) + '\n', encoding='utf-8', newline='\r\n')

if __name__ == '__main__':
    build()
