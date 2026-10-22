# Tinkli branding

The shared asset source is `src/wros-web/public/branding/`. Logos live in `logo/`, icons and the web manifest in `fav/`. API builds and publishes copy this folder to their own `BrandingAssets/` (served at `/branding`); keep these files together when deploying. API branding is served with Swagger in Development.

Changed: web `src/styles.css`, `src/components/layout/AppShell.tsx`, `index.html`, branding manifest and Swagger stylesheet; API `Program.cs` and `Wros.Api.csproj`. Supplied logo/icon files are included unchanged.

Start the configured API with `dotnet run --project src/Wros.Api` and the web app with `npm --prefix src/wros-web run dev`. Existing database and authentication configuration still applies. No new settings, migrations or seed commands are required.

1. Open the web app signed out. Expect the Tinkli logo, purple sign-in button and lavender desktop introduction panel. At mobile width expect the logo above the form.
2. Sign in. Expect a purple top header, Tinkli sidebar logo, purple active navigation and pastel dashboard summary cards. Collapse the sidebar: expect the character mark without clipping. Open mobile navigation and check the logo and readable menu items.
3. Open a form and navigate with Tab. Expect visible purple focus indicators; validation and stock warning/success colors retain their meaning.
4. Open `/swagger` on the Development API. Expect the Tinkli logo, purple top bar, gradient accent, lavender information panel and Tinkli tab icon. Web tabs should also show the Tinkli icon. If cached, hard-refresh or reopen the tab.
5. Open the account menu and check text is readable on its white panel; check desktop and narrow tablet layouts.

Validation: frontend production build and .NET solution build passed; no automated tests added. Browser visual verification remains owner-operated. Product-card swatches use the pictured pastel fills where the supplied image's purple/green line and fill captions conflict. Status colors remain semantic. Swagger is still Development-only. Please verify these checks before approving the branding update.
