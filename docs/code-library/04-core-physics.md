# 4. Core state and physics

Core keeps 380 channels with twelve positions each. Per-node arrays are channel-major, with fast before thermal for two-group values. Channel and position indices are zero-based. A position identifies a physical slot, while the bundle ID follows the fuel. Adjacency, boundaries and flow direction come from the validated topology.

## Fuel moves and exposure

```annotated-source
path: src/ReactorSim.Core/Domain/GameRefuellingContracts.cs
title: One authoritative refuelling position map
language: csharp
start: public GameRefuellingPlanV1(GameRefuellingDirectionV1 direction, ushort shiftCount)
end: 'RetainedToPositions = Array.AsReadOnly(RetainedFromPositions.Select(i => towardB ? i + shiftCount : i - shiftCount).ToArray());

  }'
steps:
- through: throw new ArgumentOutOfRangeException(nameof(shiftCount));
  explanation: '**Validate the generic contract.** Core accepts only known directions and shifts of four or eight bundles. The public Game path further requires eight. Throwing here prevents constructing an internally inconsistent map.'
- through: 'OutgoingEnd = towardB ? "B" : "A";'
  explanation: '**Name the movement and its ends.** The enum becomes a stable wire/display ID. `towardB` controls the ternary expressions: movement toward B enters at A and exits at B; movement toward A does the reverse.'
- through: 'DischargedPositions = Array.AsReadOnly(Enumerable.Range(towardB ? 12 - shiftCount : 0, shiftCount).Select(i => (uint)i).ToArray());'
  explanation: '**Choose fresh and outgoing slots.** `Enumerable.Range(start, count)` constructs consecutive positions. Insertion uses the incoming end, discharge uses the outgoing end. The integer values become unsigned positions and read-only arrays so consumers share a fixed map.'
- through: "        }"
  explanation: '**Map surviving bundle positions.** The remaining `12 - shiftCount` bundles move by exactly the shift length. Adding shifts toward B; subtracting shifts toward A. Identity follows the retained bundle even though its slot changes.'
```

Core applies the same move map that it publishes for previews. A shift toward End B inserts eight bundles in positions 0–7, shifts four retained bundles to positions 8–11 and discharges positions 4–11 from the old inventory. End A reverses the mapping.

```annotated-source
path: src/ReactorSim.Core/Domain/GameRefuellingContracts.cs
title: Create fresh bundles and move retained identities
language: csharp
start: BundleState[][] nextChannels = (BundleState[][])_channels.Clone();
end: shiftCount, UnlimitedFreshFuel);
steps:
- through: var discharged = new BundleState[shiftCount];
  explanation: '**Allocate replacement containers.** Clone the outer channel array, then allocate a new array for the changed channel and separate movement reports. Other channel arrays can be shared because this operation replaces bundle state instead of mutating accepted arrays.'
- through: uint position = plan.InsertedPositions[(int)index];
  explanation: '**Use the shared plan.** Each loop iteration takes the next insertion slot from the authoritative map. The same map can be projected to the player before committing the move.'
- through: simulationTimeSeconds);
  explanation: '**Construct a fresh bundle.** Its stable ID comes from the next fresh-bundle sequence, followed by channel and physical position, trimmed material ID, zero initial burnup and cumulative energy, heavy-metal mass, and insertion time. Fresh identity is distinct from the slot identity.'
- through: 'inserted[index] = fresh;

    }'
  explanation: '**Place and report each insertion.** The target channel receives the new bundle at its physical position, and the inserted list retains the same value for the movement report.'
- through: 'target[to] = Move(source[from], channelIndex, to);

    }'
  explanation: '**Move retained bundles without replacing their identities.** Parallel from/to maps specify the new slot of each surviving old bundle. `Move` updates location while carrying that bundle''s existing fuel history.'
- through: discharged[index] = source[plan.DischargedPositions[index]];
  explanation: '**Capture the outgoing old bundles.** Discharge records come from the original channel, so their identities and exit burnup remain available after replacement.'
- through: shiftCount, UnlimitedFreshFuel);
  explanation: '**Assemble the new core inventory.** Replace only the selected channel. Finite stock decreases by the inserted count; unlimited practice retains stock policy. Checked counters advance operation count and fresh-ID sequence, rejecting arithmetic overflow. The returned core is still a candidate until Game completes its equilibrium transaction.'
```

The next exposure update divides each bundle's accepted thermal fission energy by its heavy-metal mass. The updated burnup selects an interpolated row from the material table. Retained bundles keep their iodine and xenon. Fresh bundles start with zero poison.

```annotated-source
path: src/ReactorSim.Core/Domain/GameRefuellingContracts.cs
title: Validate and apply per-bundle thermal energy
language: csharp
start: BundleState[][] nextChannels = new BundleState[ChannelCount][];
end: 'nextChannels[channelIndex] = target;

  }'
steps:
- through: double delta = deltaFissionEnergyJ[index++];
  explanation: '**Walk channel-major input.** Allocate the replacement core, then visit every channel and position. `index++` reads the current energy increment before advancing the flat index. Thus channel 0 consumes the first twelve entries, channel 1 the next twelve, and so on.'
- through: '"Burnup energy increments must be finite, canonical, and nonnegative SI joules.");

    }'
  explanation: '**Reject invalid input energy.** Every increment must satisfy the canonical nonnegative-number contract. The diagnostic path includes the flat input index, identifying the bad value. Early return leaves the accepted core unchanged.'
- through: '"Burnup integration would make cumulative fission energy non-finite.");

    }'
  explanation: '**Check accumulated energy as well.** Add the increment to the bundle''s prior lifetime energy, then validate the sum. Two individually valid numbers can still overflow when added.'
- through: ': source[position].WithEnergy(cumulative);

    }'
  explanation: '**Retain or replace each bundle state.** A zero increment reuses the old value. Otherwise `WithEnergy` creates its updated energy/burnup state while preserving bundle identity. The conditional avoids unnecessary replacement for unchanged fuel.'
- through: "            }"
  explanation: '**Store the completed replacement channel.** Once its twelve positions are valid, place the channel array in the new outer array. The following return wraps this inventory with unchanged stock and refuelling metadata.'
```

## Solve the spatial balance

The active pack supplies material coefficients, volume, leakage conductances, explicit radial and axial boundaries and convergence tolerances. Core binds fuel coefficients by burnup and combines device absorption with the difference between live and reference xenon. An outer eigenvalue iteration uses deterministic inner group solves and normalizes fission power.

```annotated-source
path: src/ReactorSim.Core/Domain/SpatialEigenIterationContracts.cs
title: Correct flux and test the numerical residual
language: csharp
start: for (int innerIteration = 0;
end: 'Array.Copy(candidate, solution, solution.Length);

  }'
steps:
- through: double next = solution[nodeIndex] + correction;
  explanation: '**Form a Jacobi correction.** The outer loop is bounded by numerical policy. For each node, subtract the current
    operator result from the required source, divide by the diagonal coefficient and add that correction to the current flux.
    Each node reads the previous iteration''s `applied` values.'
- through: '"The deterministic Jacobi update produced a non-finite flux.");

    return false;

    }'
  explanation: '**Reject numerical overflow or invalid arithmetic.** Both the correction and proposed flux must be finite.
    The diagnostic identifies the affected node; returning `false` prevents this inner result from being treated as a converged
    solution.'
- through: 'candidate[nodeIndex] = next;

    }'
  explanation: '**Enforce nonnegative neutron flux.** A negative proposal is invalid. Valid proposals go into a separate `candidate`
    buffer, so later nodes do not accidentally use partially updated values from this same iteration.'
- through: 'diagnostic = operatorDiagnostic;

    return false;

    }'
  explanation: '**Evaluate the candidate with the spatial operator.** `TryApply` writes `A × candidate` into `applied`. If
    that calculation fails, its diagnostic is propagated. This reevaluation is needed to measure how well the proposed flux
    satisfies the equation.'
- through: double rowScale = Math.Abs(applied[nodeIndex]) + Math.Abs(source[nodeIndex]);
  explanation: '**Measure equation mismatch.** The residual at a node is `applied - source`. Absolute difference measures
    error magnitude; row scale measures the magnitudes of the compared terms. Both are evaluated over every spatial node.'
- through: 'scale = Math.Max(scale, rowScale);

    }'
  explanation: '**Validate and collect the worst mismatch.** Reject non-finite residual calculations. Taking maxima yields
    an infinity-norm error and a scale over all nodes, preventing a small average from hiding a large local mismatch.'
- through: '"The deterministic inner relative residual became non-finite.");

    return false;

    }'
  explanation: '**Normalize the residual.** If both terms have zero scale, relative error is defined as zero. Otherwise divide
    worst absolute residual by the scale and require a finite result. This makes a relative tolerance meaningful across different
    source magnitudes.'
- through: "Array.Copy(candidate, solution, solution.Length);\n            }"
  explanation: '**Accept convergence or continue.** Meeting either the absolute or relative policy tolerance copies the candidate
    into the output, clears the diagnostic and returns success. Otherwise copy it as the next starting guess and repeat. If
    the bounded loop exhausts, the following code reports nonconvergence rather than accepting an unconverged answer.'
```

Game tests the liquid-zone response using the shared solver and commits accepted results. Custom runs validate overrides and compute their own time-average reference at startup.

See the [active solver equations](../physics/active-two-group-solver.md), [calibration provenance](../physics/calibration-provenance.md), and [reactor theory](theory/index.md).
