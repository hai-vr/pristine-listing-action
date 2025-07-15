Tests
===

This test suite is incomplete.

TODO:
- Gatherer test needs to be proper, it's currently testing an empty case.
  - Http client could be abstracted using a thin layer so that it's not a pain to mock.
  - We need to test that http clients have the correct headers--including a lack of headers.
  - A case could be made that gatherer doesn't do the requests itself, but instead a thin layer does the requests,
    and the Gatherer either parses the JSON of the responses or is provided with JObjects directly.
- Outputter is not tested.
- Webpage outputter needs to be separate from JSON outputter.
