﻿pristine-listing-action
====

This action partially replaces another repository listing aggregator.

I (*Haï~*) made it for use with non-VRC projects after the other listing aggregator started throwing errors.

## Usage

This workflow repository provides NO GUARANTEES on the stability of any API, as I am using this for my personal listings.

You should not use this repository as a workflow action unless you fork it first and use your own fork.

### Example

Repositories are defined in the `input.json` file.

Look at the workflow of [hai-vr/vpm-listing](https://github.com/hai-vr/vpm-listing/blob/main/.github/workflows/build-listing.yml)
for an example of a repository that uses this.

### Workflow call inputs (`with:`)

The following [workflow call inputs](https://docs.github.com/en/actions/reference/workflow-syntax-for-github-actions#onworkflow_callinputs) are exposed:

- `excessiveMode` boolean, defaults to `false`:
  - If `true`, will to download the ZIP of each release in order to calculate the `zipSHA256`.
  - If `false`, will download the `package.json` asset from the release, and not calculate any `zipSHA256` value (see [Differences](#differences) section below).
- `includeDownloadCount` boolean, defaults to `false`:
  - When `true`, the description of each version is modified to include the number of downloads for that versions.

## Differences

- By default, we don't download the zip file of the package itself, unless workflow input `excessiveMode` is set to true.
  - The contents of the `package.json` file is read from the assets of the release.
  - Similarly to [bdunderscore/vpm-repo-list-generator](https://github.com/bdunderscore/vpm-repo-list-generator)
    we don't calculate the `zipSHA256` by default; however the code to do this is implemented.
- Information about `"samples"` in the package.json is not exposed to the repository listing.
- The listing correctly aggregates [UPM package manifests that define the `author` field as `string`](https://docs.unity3d.com/Manual/upm-manifestPkg.html#:~:text=author,Object%20or%20string).
- The description is modified with the number of downloads of the last version appended to it, if the workflow input `includeDownloadCount` is set to true.
- The generated web page is rudimentary and not meant for public browsing.
- Caching is not implemented, so this will cause all `package.json` to be downloaded every time this action is run.

#### Exclude packages

- If the body of GitHub release notes contains the substring `$\texttt{Hidden}$` then that release is ignored.
- If a given product in `input.json` has `includePrereleases` set to false, then pre-releases will not be included.
