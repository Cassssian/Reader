Drop the two font files used by the app here:

  OpenSans-Regular.ttf
  OpenSans-Semibold.ttf

They ship with the standard .NET MAUI template, or grab them from
https://fonts.google.com/specimen/Open+Sans (Apache 2.0).

Registered in MauiProgram.cs via ConfigureFonts(...). If absent, the app
falls back to the system font — it still builds and runs.
