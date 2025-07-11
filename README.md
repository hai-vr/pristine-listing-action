pristine-listing-action
====

This action partially replaces another repository listing aggregator.

I (*Haï~*) made it for use with non-VRC projects after the other listing aggregator started throwing errors.

## Usage

This workflow repository provides NO GUARANTEES on the stability of any API, as I am using this for my personal listings.

You should not use this repository as a workflow action unless you fork it first and use your own fork.

## Not implemented yet

- When querying GitHub releases, pagination is not implemented, so if a repository has more than 100 releases,
  the workflow action will fail.
- Information about `"samples"` in the package.json is not exposed to the repository listing.
- Caching is not implemented, so this will cause all packages to be downloaded every time this action is run.

## Non-goals

A user-browsable website is not implemented; this generates the listing JSON only.
