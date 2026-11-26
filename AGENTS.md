# AGENTS.md

Instructions for AI coding agents that work in this repository.

Write all text (this file, `CHANGELOG.md`, commit messages, pull requests) in ASD-STE100 Simplified Technical English: short sentences, active voice, one term for one thing.

## CHANGELOG.md

`CHANGELOG.md` is user-facing. The NuGet package shows it as release notes, and the release workflow copies the section of the released version into the GitHub release.

### When you change the tool

- Add one entry for each change that a user can see. Put it in the same pull request as the change.
- Put the entry in the `## [Unreleased]` section. This section is directly below the legend and above the newest version section.
- If there is no `## [Unreleased]` section, add the heading. A release pull request removes it (see below).
- Do not add an entry for a change that a user cannot see: tests, CI, refactoring, or this file.

### Entry format

- One line for each entry. Start with an emoji from the legend at the top of `CHANGELOG.md`:

  | Emoji | Use for |
  |---|---|
  | 🚨 | Breaking change |
  | ✨ | Feature or improvement |
  | 💥 | Critical bug |
  | 🐛 | Bug |
  | 📦 | Dependency update |

- Add ❗ after 📦 when the update fixes a significant bug or a security vulnerability. Link the advisory.
- Describe the effect for the user, not the implementation. Use the past tense: "Fixed …", "Added …".
- For a bug, state what was wrong and when the user saw it.
- Put code, option names and values in backticks.
- Do not use a new emoji that is not in the legend. If a new category is necessary, add it to the legend in the same pull request.

### Release pull request

A release has its own pull request, separate from the changes that it releases. Stack it on the last change if that change is not merged.

1. Find the version. Versions follow `YYYY.RELEASECOUNT.PATCH` (see `README.md`):
   - Only bug fixes since the last release: increase `PATCH`.
   - A feature or a breaking change: increase `RELEASECOUNT` and set `PATCH` to 0.
   - First release of a year: `YYYY.1.0`.
2. Replace the heading `## [Unreleased]` with `## [<version>] - <YYYY-MM-DD>`. Do not keep an empty `## [Unreleased]` section.
3. Use the planned date of the release tag. Ask the user if the date is not known.
4. Make no other change in this pull request.
5. Use the title `📝 Prepare <version> changelog section`.

The heading must contain the exact version of the tag. The release workflow fails if `CHANGELOG.md` has no section for the tag.

### Do not

- Do not create or push tags. The user pushes the version tag after the merge, and the tag starts the release.
- Do not change the text of a released section, except to correct an error.
