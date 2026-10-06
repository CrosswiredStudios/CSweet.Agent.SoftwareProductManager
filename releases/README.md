# Agent release notes

C-Sweet displays versioned release notes next to the update icon on Installed Agents.

For every new agent version, follow this order:

1. Bump the version FIRST in `csweet-plugin.json` and synchronize the implementation, project/package, and tests.
2. Only AFTER bumping, read the final manifest version and write `releases/<version>.md` for that exact version (without a `v` prefix). Never use the old version for new release notes.
3. Describe user-visible changes, fixes, configuration changes, and migration steps. State breaking changes explicitly.
4. If the version changes again, retarget the unpublished notes to the final version. Preserve published historical notes.
5. Before handoff or publishing, verify the manifest, implementation/package version, note filename, and note heading match. Commit the note with the matching version changes.
6. Use UTF-8 Markdown, at most 64 KiB. Simple headings and bullet lists also read well in the plain-text popup. Do not embed HTML or secrets.

C-Sweet reads this file at the same resolved commit as the update manifest, never from a moving branch. Missing notes do not block an update. Prerelease and build metadata remain part of the filename, for example `releases/1.3.0-beta.1.md`.

The first file added for an existing version is a tracking baseline; it is not a reconstructed changelog.
