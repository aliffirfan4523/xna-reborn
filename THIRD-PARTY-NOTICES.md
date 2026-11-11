# Third-party notices

## Flixel

The `src/XnaReborn.Flixel` project is a C# implementation of the public Flixel 2.x API (class and member names, behavior), written for XNA Reborn.
It is copied into every migrated Flash game so that converted code can call it. Flixel is by Adam Atomic and is distributed under the MIT License.
The code was written against the public API and documentation, not copied from the Flixel source, but the notice is kept because the API and behavior follow Flixel.

```
MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated
documentation files (the "Software"), to deal in the Software without restriction, including without limitation
the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and
to permit persons to whom the Software is furnished to do so, subject to the following conditions: the above
copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.
```

## Reference projects (read, not copied)

These were read to understand AS3 conversion. No code from them is in this repository.

- [ax3](https://github.com/innogames/ax3) (MIT): design reference for a typed AS3 conversion pipeline.
- [as3hx](https://github.com/HaxeFoundation/as3hx) (MIT): idea source for a compatibility library.
- Candidate Flixel games (Mode, FlxCollisions) are used only through a local checkout and the `XNAREBORN_FLASH_SAMPLES` variable. They are never added to the repository.

## MonoGame

Converted games use MonoGame (Microsoft Public License / MIT) from NuGet. It is not copied into this repository.
