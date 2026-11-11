# device-wp-silverlight

Original synthetic code written for this repository. It contains no third-party code or assets and is released under the repository license.

## Purpose

A Windows Phone project that is a Silverlight application with XNA graphics: it sets `SilverlightApplication` to `true`, targets the Silverlight framework identifier with the Windows Phone profile, and has `XnaPlatform` `Windows Phone`. It is never built.

## What the analyzer should find

- XNA007 (Windows Phone device project) and XNA051 (Silverlight application, Unsupported).

## Sources and limits

The property set is written from the Microsoft documentation of the Windows Phone project system, not from a real project. The shape is unconfirmed against a real project (design.md, "Not verified"): the rule XNA051 stays provisional until one is checked.
