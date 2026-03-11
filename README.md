Symmetry and Deterministic Expansion in a Rotor-Based Computation Model

Intro: This post wraps up the technical side of the project. It includes the abstract, the full test results, and the complete codebase in a ZIP file so anyone can reproduce the behavior on their own machine. Everything here is classical, deterministic computation — no claims, no speculation, just the system behaving according to its logic.

Abstract: This project summarizes observations from a personal programming experiment involving a small, symmetric input set (256 integers) processed through deterministic rotor logic to generate outputs in a vastly larger possibility space. The resulting distribution is uniform and reversible, enabling reliable symmetric encryption and decryption. Although some of the patterns resembled conceptual ideas like state selection or superposition, the system itself is entirely classical and deterministic.

The phrase “deterministic selection” is used here only as a descriptive framing for how ordered inputs expand into large permutation spaces while still preserving enough structure to recover the original state. This is not a claim about physics or quantum behavior—only a way to articulate the behavior observed in the code. Whether these patterns reflect the nature of large search spaces, the symmetry of the initial conditions, or simply the mechanics of the implementation, they offered a useful lens for thinking about structure and expansion in deterministic systems.

Included Materials

The project contains:

- Full test output in ZIP format containing
	- All rotor data
	- Collision search logs
	- Entropy results
- Source code for the testing app
- Instructions for reproducing the results:

	Required reading:
	https://github.com/mechmove/mechmove.github.io/discussions/38

	https://github.com/mechmove/mechmove.github.io/discussions/39
	
	Refer to comments about how each program is to be run

Everything is self-contained. If you have basic C# experience, you can run the tests exactly as I did.

Notes on Reproducibility
- All results come from deterministic logic.
- No calculated rotor matched any predestined value in the 2³¹ seed space.
- Entropy measurements were consistent across all samples.
- Collision searches behaved as expected given the size of the permutation space.

Closing

This project started as a curiosity-driven programming experiment and ended up being a surprisingly deep dive into symmetry, permutations, and deterministic expansion. I’m sharing the results because they were interesting to explore — not because they imply anything beyond classical computation.
Thanks for following along.
