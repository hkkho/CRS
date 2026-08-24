# Offline DRAGON5/DONJON5 reference environment

This is the operating reference for the frozen Phase 1 offline evidence
pipeline. Phase 1 and G1 are complete; the source pins, parsers, compact
export workflow, and literature digest are evidence infrastructure, not
runtime dependencies or approved golden data.

No Version5 source, binary, container, nuclear-data library, private input
deck, raw output, compact export, or golden dataset is committed here. DRAGON5
and DONJON5 remain offline reference tools; the game never calls, ships, or
ports them. Current delivery status and any next reference task come from
[`docs/PROJECT_SCOPE.md`](../docs/PROJECT_SCOPE.md), not this guide.

The machine-readable source of truth for the Version5 pin is
[`manifests/version5-v5.1.0-provenance.json`](manifests/version5-v5.1.0-provenance.json).
It deliberately distinguishes upstream claims from a redistribution permission.

## Exact upstream acquisition

Use a workspace outside this repository. The Version5 tag is lightweight, so
verify both its commit and Git tree rather than treating the tag name alone as
the identity.

```bash
git clone --branch v5.1.0 --single-branch \
  https://git.oecd-nea.org/dragon/5.1.git version5
test "$(git -C version5 rev-parse HEAD)" = \
  eee582f8594d3b7d01b65660ca1e8bef00eb6b2a
test "$(git -C version5 rev-parse 'HEAD^{tree}')" = \
  0d3e8f721c84b3c3b2d03e9898a8a0a9c29287d8

GIT_LFS_SKIP_SMUDGE=1 git clone \
  https://git.oecd-nea.org/dragon/libraries.git libraries
GIT_LFS_SKIP_SMUDGE=1 git -C libraries checkout --detach \
  d4456d42fdcdeb8147ce6e79d3a8ba0e775d403f
test "$(git -C libraries rev-parse HEAD)" = \
  d4456d42fdcdeb8147ce6e79d3a8ba0e775d403f
test "$(git -C libraries rev-parse 'HEAD^{tree}')" = \
  ed92fc1c60c373f07276516142a0aa28ba4c2433
git -C libraries lfs ls-files -l -s
```

The official repository's `README.md` requires Git LFS and recommends skipped
smudge for the initial clone. Do not fetch all LFS objects. A later approved
case task may pull one selected asset only after it records the exact path,
full LFS OID, SHA-256, upstream URL, retrieval date, and legal status.

## Licensing and data boundary

Polytechnique Montréal's official [Version5 page](https://merlin.polymtl.ca/version5.htm)
and [FAQ](https://merlin.polymtl.ca/faq5.htm) describe Version5 components as
LGPL. The pinned source also has representative LGPL-2.1-or-later headers, but
it contains GPL-2.0-or-later third-party Utilib/freesteam files. Its repository
license classification is therefore `NOASSERTION / mixed notices`, not a
blanket SPDX assertion.

The official Version5 page identifies the LFS repository as the 5.1
cross-section-library location and calls its XMAS/SHEM Draglibs open-source.
At the pinned libraries revision, however, there is no standalone `LICENSE`,
`COPYING`, or per-asset license notice. Public access and that description do
not establish redistribution rights for every file or for derived exports.

Accordingly, this project does not vendor, mirror, publish, or redistribute
Version5 source, executables, container images, nuclear-data libraries, or
derived reference outputs. Preserve all upstream notices. Obtain explicit
upstream permission or a legal determination before any such redistribution.

## Linux source-build baseline

The pinned upstream CI's Ubuntu 22.04 build installs the following package set,
exports the listed variables, and builds from `Donjon`. It enables both HDF5
and OpenMP; this is a documented prerequisite baseline, not a P1-T01 build or
smoke run.

```bash
sudo apt-get update
sudo apt-get install -y --no-install-recommends \
  make gfortran python3 python3-dev python3-numpy python3-distutils \
  libhdf5-serial-dev libomp-dev

export HDF5_INC=/usr/include/hdf5/serial
export HDF5_API=/usr/lib/x86_64-linux-gnu/hdf5/serial
export FORTRANPATH=/usr/lib/gcc/x86_64-linux-gnu/11

cd version5/Donjon
make hdf5=1 openmp=1
```

Acquire `git` and `git-lfs` separately for the clone steps above. The upstream
README requires a Fortran 2003-or-later compiler and Python 3; the Linux
makefiles select `gfortran` and `gcc` by default. Record exact OS, package,
compiler, command, and executable hashes in the later case manifest because
upstream does not pin a package snapshot or compiler version.

An official prebuilt image is also recorded by digest in the provenance
manifest. If it is used later, pull
`docker.oecd-nea.org/dragon/5.1@sha256:eb8ddff7d788f563f958ada277a2c8304d2dcf829ef59a35db62438397188d79`,
not a mutable tag. The pinned README describes the tagged image as Ubuntu 22.04
while the pinned `Dockerfile.slim` uses the project's Ubuntu 24.04 base; the
resolved image is `linux/amd64`, reports OCI version label `24.04`, and sets
`FORTRANPATH=/usr/lib/gcc/x86_64-linux-gnu/13`. The digest, rather than either
prose label, is the image identity.

## P1-T07 compact-export workflow

After each private P1-T02 and P1-T03 smoke run succeeds, the two run-record
paths can be passed to the P1-T07 workflow. The raw listings and program
`stderr` files are read from those records; they are never copied into this
repository or into the workflow output.

Run PowerShell from the repository root and choose a new output directory
outside the repository:

```powershell
powershell -NoProfile -File .\tools\PhysicsData\Run-P1-T07-ReferenceWorkflow.ps1 `
  -DragonRunRecordPath 'C:\private\p1-t02\metadata\P1-T02-lumpSS-run-record.json' `
  -DonjonRunRecordPath 'C:\private\p1-t03\metadata\P1-T03-AFA_180_310_type1_dual-run-record.json' `
  -ArtifactsPath 'C:\private\p1-t07-reference-workflow'
```

The workflow invokes the fixed P1-T05/P1-T06 parsers twice for each listing,
requires byte-identical compact exports, validates the P1-T04 compact and
raw-run schemas, and writes only external/private evidence:

- `compact/` contains the first and repeated compact exports;
- `manifests/` contains one `reactorsim.reference-raw-run/v1` manifest per
  smoke case, including listing, stderr, and compact-export hashes; and
- `metadata/P1-T07-compact-repeat-check.json` records the paired hashes and
  byte-equality result.

The compact documents remain `candidate_reference` and all workflow artifacts
remain `external-private`/`not-approved`. This workflow does not rerun
DRAGON5 or DONJON5, approve a reference baseline, or replace the T6/G1
provenance review.

## Future reference-baseline work

No Phase 1 task is active. A later authorized reference-baseline task must use
the P1-T08 digest, preserve the acquisition and redistribution restrictions
above, and establish the exact case, tool/build, nuclear-data identity,
geometry, units, normalization, output schema, reproducibility, and licensing
before it can claim a comparison or golden result. T6 is required only when
that baseline itself changes.
