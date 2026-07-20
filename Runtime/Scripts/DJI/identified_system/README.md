# Identified-system controllers

This folder holds controllers driven by discrete-time transfer functions identified
from real flight data (system identification / "Sysid"), plus the small IIR filter
(`DiscreteTf`) that evaluates them, instead of hand-tuned PID gains.

**FLU vs ENU:** all identified models (`SysidAltitudeController`, `SysidHorizontalController`)
were identified on **FLU** (Forward-Left-Up, body-frame) velocity signals — that's what the
real drone's onboard logs record. Unity/the sim works in **world-frame (ENU-like)** coordinates.
Every controller here is responsible for converting its FLU model output back into an ENU
force vector before calling `AddForceAtPosition`. 

**Payload caveat:** the identified models were fit **without** any payload attached. Any
mass-compensation feature (e.g. `ExtraMassToCompensate` on `SysidAltitudeController`) is a
manual bolt-on, not something the model itself accounts for. expect the simulated response
to diverge more from the real drone as added payload mass grows.

---

## `discreteTf.cs`

`DiscreteTf` evaluates a single-input single-output discrete transfer function

```
        b0 z^n + b1 z^(n-1) + ... + bn
G(z) = --------------------------------
        a0 z^n + a1 z^(n-1) + ... + an
```

given as two coefficient arrays in **descending powers of z**, matching what a system-ID
tool exports directly. Every controller in this folder identifies
its models at a fixed sample time of **T = 0.02 s (50 Hz)** and calls `Step()` once per
`FixedUpdate`, so `Time.fixedDeltaTime` in the project must be 0.02 s or the model will
run at the wrong rate relative to what it was identified against.

### Fields

| Field | Meaning |
|---|---|
| `b` | Numerator coefficients, zero-padded on the left to the same length as the denominator (`denLen`). Padding lets a lower-order numerator (e.g. a single gain) line up with the same lag indices as the denominator/`pastU` buffer. |
| `aTail` | Denominator coefficients **excluding** `a[0]` (i.e. `a[1..]`), the feedback coefficients multiplying past outputs. |
| `invA0` | `1 / a[0]`, precomputed so `Step()` doesn't divide every call. |
| `pastU` | Ring buffer (length `denLen`) of past inputs, `pastU[0]` = most recent input `u[k]`, `pastU[1]` = `u[k-1]`, etc. |
| `pastY` | Ring buffer (length `denLen - 1`) of past outputs, `pastY[0]` = most recent output `y[k-1]` at the time a new step is computed. |

### Constructor validation

- `a.Length >= 2` — the denominator must be at least first-order (a static gain isn't a "system").
- `a[0]` must be non-zero — needed to normalize the difference equation.
- `bRaw.Length <= a.Length` — the transfer function must be proper (no more zeros than poles).

### `Step(u)` — the difference equation

Each call:
1. Pushes the new input `u` onto the front of `pastU` (`AppendBottom`, shifting older samples down and dropping the oldest).
2. Computes `y[k] = (dot(b, pastU) - dot(aTail, pastY)) * invA0`, i.e. the standard direct-form difference equation
   `a0*y[k] = b0*u[k] + b1*u[k-1] + ... - (a1*y[k-1] + a2*y[k-2] + ...)`.
3. Pushes `y[k]` onto the front of `pastY`.
4. Returns `y[k]`.

### Worked example — the x/forward channel

`SysidHorizontalController` identifies the forward-velocity response with:

```csharp
b_Gxd = { 0.03685537260331673 };
a_Gxd = { 1.0, -0.9662635006011508 };
```

`denLen = a.Length = 2`. The numerator gets padded: `d = 2 - 1 = 1`, so
`b = [0, 0.03685537260331673]` — index 0 lines up with `pastU[0] = u[k]` (contributes 0),
index 1 lines up with `pastU[1] = u[k-1]`.

`aTail = [-0.9662635006011508]`, `invA0 = 1` (since `a[0] = 1.0`).

So `Step(u)` reduces to the first-order (single-pole) difference equation:

```
y[k] = 0.9662635006011508 * y[k-1] + 0.03685537260331673 * u[k-1]
```

i.e. a discrete low-pass: commanded forward velocity `u` (the target) lags into modeled
forward velocity `y` with a pole at `z = 0.9663` (time constant ≈ `-T / ln(0.9663) ≈ 0.58 s`
at `T = 0.02 s`).
