pristine-listing-action
====

This action partially replaces another repository listing generator.

I (*Haï~*) made it for use with non-VRC projects after the other listing generator started throwing errors.

## Usage

This workflow repository provides NO GUARANTEES on the stability of any API, as I am using this for my personal listings.

You should not use this repository as a workflow action unless you fork it first and use your own fork.

### Example

Repositories are defined in a file called `input.json` at the root of the repository.

You can find an example in the [input.example.json](input.example.json) file, which needs to be renamed.

Look at the workflow of [hai-vr/vpm-listing](https://github.com/hai-vr/vpm-listing/blob/main/.github/workflows/build-listing.yml) for an example of a repository that uses this.

### Settings

- `defaultIncludePrereleases` bool: By default, include prereleases. 
- `defaultMode` string enum: By default:
  - `PackageJsonAssetOnly`: Only use the `package.json` asset of the release to extract information.
  - `ExcessiveWhenNeeded`: Use the `package.json` asset of the release to extract information; if there's none, try to download the zip and read the `package.json` file in that zip.
  - `ExcessiveAlways`: Always download the zip and read the `package.json` file in that zip.
  - Additional notes:
    - If the zip is downloaded, the value of `zipSHA256` will be calculated.
    - If the zip is not downloaded, it will not calculate any `zipSHA256` value (see [Differences](#differences) section below).
- `excessiveModeToleratesPackageJsonAssetMissing` bool: If true, when running excessive mode, download the zip even if there is no `package.json` asset in the release.
- `includeDownloadCount` bool: Append the number of downloads to the description of that version.

### Include or Exclude packages

- If the body of GitHub release notes contains the substring `$\texttt{Hidden}$` then that release is ignored.
- If a given product in `input.json` has `includePrereleases` set to false, then pre-releases will not be included.
- If a given product in `input.json` has `onlyPackageNames` set to a non-empty list of strings, then only package names that match it will be included.

## Differences

- By default, we don't download the zip file of the package itself, unless the `defaultMode` setting is changed.
  - The contents of the `package.json` file is read from the assets of the release.
  - Similarly to [bdunderscore/vpm-repo-list-generator](https://github.com/bdunderscore/vpm-repo-list-generator)
    we don't calculate the `zipSHA256` by default; however the code to do this is implemented.
- The listing correctly aggregates [UPM package manifests that define the `author` field as `string`](https://docs.unity3d.com/Manual/upm-manifestPkg.html#:~:text=author,Object%20or%20string).
- The generated web page is rudimentary and not meant for browsing by general users.
- Caching is not implemented, so this will cause all `package.json` to be downloaded every time this action is run.
- Versions of a package are ordered by descending precedence *(Newer Version, Prereleases of that newest version, Older version, Prereleases)*.
  - Other generators might have been ordering versions of a package differently.

## Other notes

- This supports GitHub releases that contain multiple different package assets within the same release.
- This does not currently handle versions of a given package spread across multiple repositories.
- This is not fault-tolerant: The repositories in `input.json` should point to repositories that you trust or have control of.
  If a release of some repository contains garbage in its `package.json` then it may fail to execute.
  - This is not injection-tolerant: If a `package.json` contains javascript then it may execute in the browser webpage.

## Local testing

To test this project in your local machine, set up the Run configuration in your IDE so that it sets the environment variable
called `IN__GITHUB_TOKEN` to a [GitHub Personal Access Token](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/managing-your-personal-access-tokens#creating-a-fine-grained-personal-access-token)
that you have generated.

You can use a fine-grained personal access token that has rights to nothing (not even that repository); that token will have enough
privileges to call GitHub's public APIs to get the paginated releases of a repository and download its Zip files.
