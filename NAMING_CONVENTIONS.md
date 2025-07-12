﻿- The `?` nullable indicator should only be used on structures serialized and deserialized from JSON, nowhere else.
  - Use the `ourVariableNameNullable` naming convention as usual that I use in all of my other projects.
  - Mixing `ourVariableNameNullable` and `?` leads to confusion, so stay consistent in not using `?`.
- Otherwise, use the applicable conventions as in https://github.com/hai-vr/8f3b9a2cde14/blob/main/Packages/dev.hai-vr.8f3b9a2cde14/README.md