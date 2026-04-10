# Confidence Scoring Rubric

Each review agent scores every finding on a 0-100 scale. This rubric is embedded
in each agent's prompt to ensure consistent scoring.

## Score Definitions

| Score | Level | Description |
|-------|-------|-------------|
| 0 | Not confident | False positive. Does not stand up to light scrutiny, or is a pre-existing issue not introduced by this PR. |
| 25 | Somewhat confident | Might be a real issue, but could also be a false positive. Agent was unable to verify. If stylistic, it is not explicitly called out in project guidelines. |
| 50 | Moderately confident | Verified as a real issue, but it might be a nitpick or rarely triggered in practice. Relative to the rest of the PR, not very important. |
| 75 | Highly confident | Agent double-checked the issue and verified it is very likely real and will be hit in practice. The existing approach in the PR is insufficient. Important and directly impacts functionality, OR is an issue directly mentioned in project guidelines. |
| 100 | Absolutely certain | Agent double-checked and confirmed this is definitely a real issue that will happen frequently. Direct evidence confirms it. |

## Threshold

**Default threshold: 80**

Only issues with confidence >= 80 are presented to the user. This filters out most false positives while retaining genuinely important findings.

## Examples of False Positives (score 0-50)

These should be scored LOW and will be filtered out:

- **Pre-existing issues**: Problems that existed before this PR and are not introduced or worsened by the changes
- **Linter/compiler catchable**: Missing imports, type errors, formatting issues, broken tests -- tools catch these
- **Pedantic nitpicks**: Issues a senior engineer would not bother calling out in a review
- **General quality**: Lack of test coverage, general security concerns, poor documentation -- unless explicitly required by project guidelines
- **Silenced issues**: Code flagged by guidelines BUT explicitly suppressed via lint-ignore, pragma, or similar comments
- **Intentional changes**: Functionality changes that are clearly deliberate and directly related to the PR's purpose
- **Unmodified lines**: Issues on lines the PR author did not change
- **Style-only**: Naming conventions, whitespace, formatting -- unless explicitly mandated in guidelines

## Scoring Tips for Agents

1. **For guideline issues**: Verify the guideline EXPLICITLY mentions the concern. If it does not, score <= 50.
2. **For bugs**: Consider whether the bug will actually be triggered in practice. Theoretical bugs score lower than practical ones.
3. **For historical issues**: Strong evidence (a reverted fix, a known recurring problem) scores higher than vague pattern matching.
4. **When in doubt, score lower.** It is better to miss a minor issue than to waste the reviewer's time with noise.
