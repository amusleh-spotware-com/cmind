# cMind docs site

The **cMind** documentation & marketing site — a [Docusaurus](https://docusaurus.io/) app. It hosts
the landing page and all the docs, and deploys to GitHub Pages automatically.

- **Live site:** https://amusleh-spotware-com.github.io/cmind
- **Docs source:** [`docs/`](docs) (Markdown/MDX — edit here, open a PR)
- **Landing page:** [`src/pages/index.tsx`](src/pages/index.tsx)
- **Theme / brand tokens:** [`src/css/custom.css`](src/css/custom.css)
- **Sidebar:** [`sidebars.ts`](sidebars.ts)
- **Config / SEO:** [`docusaurus.config.ts`](docusaurus.config.ts)

## Prerequisites

- **Node.js 20+** (22 recommended) and npm. That's it — no .NET needed to work on the site.

## Run it locally

From the repo root:

```bash
cd website
npm install          # first time only
npm start            # dev server with hot reload → http://localhost:3000/cmind/
```

`npm start` opens a live-reloading dev server. Edit any file under `docs/` or `src/` and the page
updates instantly. (Because the site is served under the `/cmind/` base path, the local URL is
**http://localhost:3000/cmind/** — note the trailing path.)

## Build & preview the production site

```bash
npm run build        # outputs static files to build/
npm run serve        # serve the built site → http://localhost:3000/cmind/
```

`npm run build` is exactly what CI runs; if it passes locally, it'll pass in the pipeline. It also
reports broken links, so run it before opening a PR.

## Add or edit a doc

1. Add/edit a Markdown file under [`docs/`](docs) (folders map to sidebar categories).
2. If it's a brand-new page, add its id to [`sidebars.ts`](sidebars.ts) so it shows in the nav.
3. `npm run build` to check for broken links.
4. Open a PR. On merge to `main`, the [`Docs site` workflow](../.github/workflows/docs.yml) rebuilds
   and redeploys automatically.

## Refresh the app screenshots

The gallery under [`static/img/screenshots/`](static/img/screenshots) is captured from a live app
boot. To regenerate it, run the E2E capture test from the repo root:

```bash
CAPTURE_SCREENSHOTS=1 dotnet test tests/E2ETests --filter "FullyQualifiedName~ReadmeScreenshotsTests"
```

That writes PNGs into `docs/design/screenshots/`; copy the ones you want into
`website/static/img/screenshots/`.

## Deployment

Pushing to `main` (touching `website/**`) triggers `.github/workflows/docs.yml`, which builds the
site and publishes it to GitHub Pages. **GitHub Pages must be enabled** with source **"GitHub
Actions"** in the repo's *Settings → Pages*. Note: Pages on a **private** repo requires a paid GitHub
plan; on the free plan the repo must be **public**.
