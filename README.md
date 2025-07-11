pristine-listing-action
====

This action partially replaces another repository listing aggregator.

I (*Haï~*) made it for use with non-VRC projects after the other listing aggregator started throwing errors.

## Usage

This workflow repository provides NO GUARANTEES on the stability of any API, as I am using this for my personal listings.

You should not use this repository as a workflow action unless you fork it first and use your own fork.

Look at the workflow of [hai-vr/vpm-listing](https://github.com/hai-vr/vpm-listing/blob/main/.github/workflows/build-listing.yml)
for an example of a repository that uses this.

## Differences

- By default, we don't download the zip file of the package itself.
  - The contents of the `package.json` file is read from the assets of the release.
  - Similarly to [bdunderscore/vpm-repo-list-generator](https://github.com/bdunderscore/vpm-repo-list-generator)
    we don't calculate the `zipSHA256` by default; however the code to do this is implemented.
- Information about `"samples"` in the package.json is not exposed to the repository listing.
- The listing correctly aggregates [UPM package manifests that define the `author` field as `string`](https://docs.unity3d.com/Manual/upm-manifestPkg.html#:~:text=author,Object%20or%20string).
- If the body of GitHub release notes contains the substring `$\texttt{Hidden}$` then that release is ignored.
- The description is modified, with the number of downloads of the last version appended to it.
- The generated web page is rudimentary and not meant for public browsing.
- Caching is not implemented, so this will cause all `package.json` to be downloaded every time this action is run.
